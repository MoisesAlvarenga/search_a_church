using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;
using SearchAChurch.Api.Features.Profile.Services;

namespace SearchAChurch.UnitTests.Features.Profile;

[Trait("Category", "Unit")]
public class ChurchProfileServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static ILogger<ChurchProfileService> CreateLogger()
    {
        return new Mock<ILogger<ChurchProfileService>>().Object;
    }

    private static ITagCatalogService CreateTagCatalogService(AppDbContext context)
    {
        var logger = new Mock<ILogger<TagCatalogService>>().Object;
        return new TagCatalogService(context, logger, null);
    }

    [Fact]
    public async Task GetChurchProfileAsync_WhenChurchNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.GetChurchProfileAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task GetChurchProfileAsync_WhenChurchExists_ShouldReturnFullProfileWithTagsAndSchedules()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Primeira Igreja Batista da Cidade",
            FormattedAddress = "Rua das Flores, 100",
            Latitude = -23.5505,
            Longitude = -46.6333,
            PlaceId = "ChIJ_SamplePlace123",
            Denomination = "Batista",
            WorshipStyle = "Contemporâneo",
            Languages = new List<string> { "pt", "en" },
            Phone = "(11) 98765-4321",
            Email = "contato@pibcidade.org",
            Website = "https://pibcidade.org",
            SocialInstagram = "@pibcidade",
            SocialFacebook = "pibcidadeoficial",
            IsActive = true,
            ClaimStatus = ChurchClaimState.Verified
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tag1 = await context.TagCatalogs.FirstAsync(t => t.Code == "rampa_acesso");
        var tag2 = await context.TagCatalogs.FirstAsync(t => t.Code == "ar_condicionado");
        context.ChurchTags.AddRange(
            new ChurchTag { ChurchId = church.Id, TagId = tag1.Id },
            new ChurchTag { ChurchId = church.Id, TagId = tag2.Id }
        );

        context.ChurchMeetingSchedules.AddRange(
            new ChurchMeetingSchedule
            {
                ChurchId = church.Id,
                DayOfWeek = DayOfWeek.Sunday,
                StartTime = "10:00",
                Description = "Culto Matutino & EBD",
                Language = "pt"
            },
            new ChurchMeetingSchedule
            {
                ChurchId = church.Id,
                DayOfWeek = DayOfWeek.Wednesday,
                StartTime = "19:30",
                Description = "Culto de Oração",
                Language = "pt"
            }
        );
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.GetChurchProfileAsync(church.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(church.Id);
        result.Value.Name.Should().Be("Primeira Igreja Batista da Cidade");
        result.Value.PlaceId.Should().Be("ChIJ_SamplePlace123");
        result.Value.Denomination.Should().Be("Batista");
        result.Value.WorshipStyle.Should().Be("Contemporâneo");
        result.Value.Languages.Should().BeEquivalentTo(new[] { "pt", "en" });
        result.Value.Phone.Should().Be("(11) 98765-4321");
        result.Value.Email.Should().Be("contato@pibcidade.org");
        result.Value.IsActive.Should().BeTrue();
        result.Value.ClaimState.Should().Be("Verified");

        // Tags
        result.Value.Tags.Should().HaveCount(2);
        result.Value.Tags.Should().Contain(new[] { "rampa_acesso", "ar_condicionado" });

        // Schedules
        result.Value.Schedules.Should().HaveCount(2);
        result.Value.Schedules[0].DayOfWeek.Should().Be(DayOfWeek.Sunday);
        result.Value.Schedules[0].StartTime.Should().Be("10:00");
        result.Value.Schedules[1].DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        result.Value.Schedules[1].StartTime.Should().Be("19:30");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenChurchNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest("Nome", "Endereço", -23.5, -46.6);

        // Act
        var result = await service.UpdateChurchProfileAsync(Guid.NewGuid(), request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenConcurrencyConflictViaHeader_ShouldReturnConflictFailure()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Central",
            FormattedAddress = "Av Paulista, 1000",
            ConcurrencyStamp = "stamp_corrente_123"
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest("Igreja Central Alterada", "Av Paulista, 1000", -23.5, -46.6);

        // Act - Envia cabeçalho If-Match defasado
        var result = await service.UpdateChurchProfileAsync(church.Id, request, ifMatchHeader: "\"stamp_defasado_999\"");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CONFLITO_CONCORRENCIA");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenConcurrencyConflictViaPayload_ShouldReturnConflictFailure()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Central",
            FormattedAddress = "Av Paulista, 1000",
            ConcurrencyStamp = "stamp_atual_abc"
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest(
            Name: "Igreja Central Alterada",
            Address: "Av Paulista, 1000",
            Latitude: -23.5,
            Longitude: -46.6,
            ConcurrencyStamp: "stamp_antigo_xyz"
        );

        // Act
        var result = await service.UpdateChurchProfileAsync(church.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CONFLITO_CONCORRENCIA");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenPlaceIdCollisionDetected_ShouldReturnConflictFailure()
    {
        // Arrange
        using var context = CreateContext();
        var churchA = new Church { Name = "Igreja A", FormattedAddress = "End A", PlaceId = "ChIJ_A", IsActive = true };
        var churchB = new Church { Name = "Igreja B", FormattedAddress = "End B", PlaceId = "ChIJ_B_Existente", IsActive = true };
        context.Churches.AddRange(churchA, churchB);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest(
            Name: "Igreja A Atualizada",
            Address: "End A",
            Latitude: -23.5,
            Longitude: -46.6,
            PlaceId: "ChIJ_B_Existente" // Tenta roubar o place_id da igreja B
        );

        // Act
        var result = await service.UpdateChurchProfileAsync(churchA.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PLACE_ID_JA_VINCULADO");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenSamePlaceIdMaintained_ShouldNotTriggerCollision()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church { Name = "Igreja Teste", FormattedAddress = "End Teste", PlaceId = "ChIJ_ProprioPlaceId", IsActive = true };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest(
            Name: "Igreja Teste Modificada",
            Address: "End Teste",
            Latitude: -23.5,
            Longitude: -46.6,
            PlaceId: "ChIJ_ProprioPlaceId" // Mantém o próprio place_id
        );

        // Act
        var result = await service.UpdateChurchProfileAsync(church.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PlaceId.Should().Be("ChIJ_ProprioPlaceId");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenTagCodesContainInvalidTags_ShouldReturnTagError()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church { Name = "Igreja", FormattedAddress = "End" };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest(
            Name: "Igreja",
            Address: "End",
            Latitude: -23.5,
            Longitude: -46.6,
            TagCodes: new List<string> { "rampa_acesso", "tag_inexistente_123" }
        );

        // Act
        var result = await service.UpdateChurchProfileAsync(church.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TAG_INVALIDA");
        result.ErrorMessage.Should().Contain("tag_inexistente_123");
    }

    [Fact]
    public async Task UpdateChurchProfileAsync_WhenValid_ShouldUpdateDataSynchronizeTagsAndSchedulesAndRotateConcurrencyStamp()
    {
        // Arrange
        using var context = CreateContext();
        var initialStamp = "initial_stamp_001";
        var church = new Church
        {
            Name = "Igreja Antiga",
            FormattedAddress = "Rua Antiga, 10",
            Latitude = -23.0,
            Longitude = -46.0,
            ConcurrencyStamp = initialStamp
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        var request = new UpdateChurchProfileRequest(
            Name: "Igreja Renovada",
            Address: "Av Nova, 500",
            Latitude: -23.55,
            Longitude: -46.63,
            PlaceId: "ChIJ_Nova123",
            Denomination: "Presbiteriana",
            WorshipStyle: "Tradicional",
            Languages: new List<string> { "pt", "en" },
            Phone: "(11) 3333-2222",
            Email: "contato@renovada.org",
            Website: "https://renovada.org",
            SocialInstagram: "@renovada",
            SocialFacebook: "renovadaoficial",
            ConcurrencyStamp: initialStamp,
            TagCodes: new List<string> { "rampa_acesso", "estacionamento_proprio" },
            Schedules: new List<MeetingScheduleDto>
            {
                new MeetingScheduleDto(null, DayOfWeek.Sunday, "18:00", "Culto Noturno", "pt")
            }
        );

        // Act
        var result = await service.UpdateChurchProfileAsync(church.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Igreja Renovada");
        result.Value.Address.Should().Be("Av Nova, 500");
        result.Value.PlaceId.Should().Be("ChIJ_Nova123");
        result.Value.Denomination.Should().Be("Presbiteriana");
        result.Value.ConcurrencyStamp.Should().NotBe(initialStamp); // ConcurrencyStamp rotacionado!
        result.Value.Tags.Should().BeEquivalentTo(new[] { "rampa_acesso", "estacionamento_proprio" });
        result.Value.Schedules.Should().ContainSingle();
        result.Value.Schedules[0].Description.Should().Be("Culto Noturno");

        // Verifica no banco de dados
        var persisted = await context.Churches
            .Include(c => c.ChurchTags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.MeetingSchedules)
            .FirstAsync(c => c.Id == church.Id);

        persisted.Name.Should().Be("Igreja Renovada");
        persisted.ChurchTags.Should().HaveCount(2);
        persisted.MeetingSchedules.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetChurchStatusAsync_WhenChurchNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.SetChurchStatusAsync(Guid.NewGuid(), false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task SetChurchStatusAsync_WhenInactivated_ShouldSetIsActiveFalseAndRotateConcurrencyStamp()
    {
        // Arrange
        using var context = CreateContext();
        var initialStamp = "initial_stamp_active";
        var church = new Church
        {
            Name = "Igreja Operante",
            FormattedAddress = "Rua Teste",
            IsActive = true,
            ConcurrencyStamp = initialStamp
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        // Act - Inativa a igreja
        var result = await service.SetChurchStatusAsync(church.Id, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ChurchId.Should().Be(church.Id);
        result.Value.IsActive.Should().BeFalse();
        result.Value.Message.Should().Contain("inativada");

        // Verifica no banco: IsActive deve ser false, DeletedAt deve permanecer null (não foi excluída!), stamp rotacionado
        var inDb = await context.Churches.IgnoreQueryFilters().FirstAsync(c => c.Id == church.Id);
        inDb.IsActive.Should().BeFalse();
        inDb.DeletedAt.Should().BeNull();
        inDb.ConcurrencyStamp.Should().NotBe(initialStamp);
    }

    [Fact]
    public async Task SetChurchStatusAsync_WhenReactivated_ShouldSetIsActiveTrue()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Inativa",
            FormattedAddress = "Rua Teste",
            IsActive = false
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new ChurchProfileService(context, tagService, CreateLogger());

        // Act - Reativa a igreja
        var result = await service.SetChurchStatusAsync(church.Id, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
        result.Value.Message.Should().Contain("ativada");

        var inDb = await context.Churches.FirstAsync(c => c.Id == church.Id);
        inDb.IsActive.Should().BeTrue();
    }
}
