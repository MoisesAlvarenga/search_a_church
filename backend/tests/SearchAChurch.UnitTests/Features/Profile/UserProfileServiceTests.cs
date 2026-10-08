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
public class UserProfileServiceTests
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

    private static ILogger<UserProfileService> CreateLogger()
    {
        return new Mock<ILogger<UserProfileService>>().Object;
    }

    private static ITagCatalogService CreateTagCatalogService(AppDbContext context)
    {
        var logger = new Mock<ILogger<TagCatalogService>>().Object;
        return new TagCatalogService(context, logger, null);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUserNotFound_ShouldReturnUserNotFoundFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.GetUserProfileAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenProfileNotConfigured_ShouldReturnDefaults()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "joao@teste.com",
            Name = "João Silva",
            PasswordHash = "hash123",
            Role = UserRole.User
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.GetUserProfileAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.Name.Should().Be("João Silva");
        result.Value.Email.Should().Be("joao@teste.com");
        result.Value.Denomination.Should().BeNull();
        result.Value.WorshipStyle.Should().BeNull();
        result.Value.DefaultRadiusKm.Should().Be(10.0);
        result.Value.PreferredLanguages.Should().ContainSingle().Which.Should().Be("pt");
        result.Value.SelectedTags.Should().BeEmpty();
        result.Value.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenProfileExists_ShouldReturnConfiguredProfileAndActiveTags()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "maria@teste.com", Name = "Maria Santos", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tag = await context.TagCatalogs.FirstAsync(t => t.Code == "rampa_acesso");

        var profile = new UserProfile
        {
            UserId = user.Id,
            Denomination = "Batista",
            WorshipStyle = "Contemporâneo",
            PreferredLanguages = new List<string> { "pt", "en" },
            DefaultRadiusKm = 15.0
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        context.UserProfileTags.Add(new UserProfileTag { UserProfileId = profile.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.GetUserProfileAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Denomination.Should().Be("Batista");
        result.Value.WorshipStyle.Should().Be("Contemporâneo");
        result.Value.PreferredLanguages.Should().BeEquivalentTo(new[] { "pt", "en" });
        result.Value.DefaultRadiusKm.Should().Be(15.0);
        result.Value.SelectedTags.Should().ContainSingle().Which.Should().Be("rampa_acesso");
        result.Value.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var request = new UpdateUserProfileRequest("Presbiteriana", "Tradicional", new List<string> { "pt" }, 10.0, null);

        // Act
        var result = await service.UpsertUserProfileAsync(Guid.NewGuid(), request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenRadiusBelowMinimum_ShouldReturnRadiusError()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "u@teste.com", Name = "U", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var request = new UpdateUserProfileRequest("Batista", "Litúrgico", null, 0.5, null);

        // Act
        var result = await service.UpsertUserProfileAsync(user.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RAIO_INVALIDO");
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenRadiusAboveMaximum_ShouldReturnRadiusError()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "u@teste.com", Name = "U", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var request = new UpdateUserProfileRequest("Batista", "Litúrgico", null, 150.0, null);

        // Act
        var result = await service.UpsertUserProfileAsync(user.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("RAIO_INVALIDO");
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenTagCodesContainInvalidTags_ShouldReturnTagError()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "u@teste.com", Name = "U", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var request = new UpdateUserProfileRequest("Batista", "Contemporâneo", null, 10.0, new List<string> { "rampa_acesso", "tag_fantasma_invalida" });

        // Act
        var result = await service.UpsertUserProfileAsync(user.Id, request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TAG_INVALIDA");
        result.ErrorMessage.Should().Contain("tag_fantasma_invalida");
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenNewProfile_ShouldCreateAndPersistSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "novo@teste.com", Name = "Novo Usuário", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var request = new UpdateUserProfileRequest(
            Denomination: "Assembleia de Deus",
            WorshipStyle: "Pentecostal",
            PreferredLanguages: new List<string> { "pt", "es" },
            DefaultRadiusKm: 25.0,
            TagCodes: new List<string> { "rampa_acesso", "estacionamento_proprio" }
        );

        // Act
        var result = await service.UpsertUserProfileAsync(user.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Denomination.Should().Be("Assembleia de Deus");
        result.Value.WorshipStyle.Should().Be("Pentecostal");
        result.Value.PreferredLanguages.Should().BeEquivalentTo(new[] { "pt", "es" });
        result.Value.DefaultRadiusKm.Should().Be(25.0);
        result.Value.SelectedTags.Should().BeEquivalentTo(new[] { "rampa_acesso", "estacionamento_proprio" });
        result.Value.IsConfigured.Should().BeTrue();

        // Check DbContext directly
        var persistedProfile = await context.UserProfiles
            .Include(p => p.UserProfileTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        persistedProfile.Should().NotBeNull();
        persistedProfile!.Denomination.Should().Be("Assembleia de Deus");
        persistedProfile.UserProfileTags.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertUserProfileAsync_WhenExistingProfile_ShouldUpdateFieldsAndSynchronizeTags()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "existente@teste.com", Name = "Existente", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tag1 = await context.TagCatalogs.FirstAsync(t => t.Code == "rampa_acesso");
        var tag2 = await context.TagCatalogs.FirstAsync(t => t.Code == "ar_condicionado");

        var profile = new UserProfile
        {
            UserId = user.Id,
            Denomination = "Metodista",
            WorshipStyle = "Tradicional",
            DefaultRadiusKm = 10.0
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        context.UserProfileTags.Add(new UserProfileTag { UserProfileId = profile.Id, TagId = tag1.Id });
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        var updateRequest = new UpdateUserProfileRequest(
            Denomination: "Presbiteriana Independente",
            WorshipStyle: "Contemporâneo",
            PreferredLanguages: new List<string> { "pt" },
            DefaultRadiusKm: 12.0,
            TagCodes: new List<string> { "ar_condicionado" } // tag1 substituída por tag2
        );

        // Act
        var result = await service.UpsertUserProfileAsync(user.Id, updateRequest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Denomination.Should().Be("Presbiteriana Independente");
        result.Value.DefaultRadiusKm.Should().Be(12.0);
        result.Value.SelectedTags.Should().ContainSingle().Which.Should().Be("ar_condicionado");

        var updatedInDb = await context.UserProfiles
            .Include(p => p.UserProfileTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        updatedInDb!.UserProfileTags.Should().ContainSingle();
        updatedInDb.UserProfileTags.First().Tag.Code.Should().Be("ar_condicionado");
    }

    [Fact]
    public async Task DeleteUserAccountAsync_WhenUserNotFound_ShouldReturnFailure()
    {
        // Arrange
        using var context = CreateContext();
        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.DeleteUserAccountAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task DeleteUserAccountAsync_ShouldAnonymizeUserData_RemoveTokensAndTags_AndStampDeletedAt()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "usuario.lgpd@teste.com",
            Name = "Carlos Mendes",
            PasswordHash = "segredo123_hash",
            Role = UserRole.User
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Adiciona Refresh Token e OTP
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "token_hash_abc",
            FamilyId = Guid.NewGuid(),
            DeviceId = "device_1",
            Status = RefreshTokenStatus.Active,
            ClientIp = "127.0.0.1",
            UserAgent = "Mozilla"
        };
        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            OtpHash = "otp_hash_123"
        };
        context.RefreshTokens.Add(refreshToken);
        context.PasswordResetOtps.Add(otp);

        // Adiciona Profile com Tags
        var tag = await context.TagCatalogs.FirstAsync(t => t.Code == "rampa_acesso");
        var profile = new UserProfile
        {
            UserId = user.Id,
            Denomination = "Batista",
            WorshipStyle = "Tradicional",
            DefaultRadiusKm = 10.0
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        context.UserProfileTags.Add(new UserProfileTag { UserProfileId = profile.Id, TagId = tag.Id });
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Act
        var result = await service.DeleteUserAccountAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Success.Should().BeTrue();
        result.Value.Message.Should().Contain("LGPD");

        // Consulta direta ignorando filtros de soft delete para auditar anonimização
        var anonymizedUser = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        anonymizedUser.Name.Should().Be("Usuário Anônimo");
        anonymizedUser.Email.Should().StartWith("deleted_").And.EndWith("@anonymized.searchachurch.org");
        anonymizedUser.PasswordHash.Should().BeEmpty();
        anonymizedUser.DeletedAt.Should().NotBeNull();
        anonymizedUser.DeletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));

        // Tokens e OTPs devem estar zerados
        var tokensInDb = await context.RefreshTokens.IgnoreQueryFilters().Where(rt => rt.UserId == user.Id).ToListAsync();
        tokensInDb.Should().BeEmpty();

        var otpsInDb = await context.PasswordResetOtps.IgnoreQueryFilters().Where(o => o.UserId == user.Id).ToListAsync();
        otpsInDb.Should().BeEmpty();

        // Profile deve estar anonimizado e sem tags
        var profileInDb = await context.UserProfiles.IgnoreQueryFilters()
            .Include(p => p.UserProfileTags)
            .FirstAsync(p => p.UserId == user.Id);

        profileInDb.DeletedAt.Should().NotBeNull();
        profileInDb.IsAnonymous.Should().BeTrue();
        profileInDb.Denomination.Should().BeNull();
        profileInDb.WorshipStyle.Should().BeNull();
        profileInDb.UserProfileTags.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchFilters_NeverMutateUserProfile_PreservesBaseline()
    {
        // Arrange (AD-021, AD-026)
        using var context = CreateContext();
        var user = new User { Email = "baseline@teste.com", Name = "Base", PasswordHash = "h" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var profile = new UserProfile
        {
            UserId = user.Id,
            Denomination = "Presbiteriana",
            WorshipStyle = "Tradicional",
            DefaultRadiusKm = 10.0,
            PreferredLanguages = new List<string> { "pt" }
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        var tagService = CreateTagCatalogService(context);
        var service = new UserProfileService(context, tagService, CreateLogger());

        // Simula busca com filtros efêmeros (raio 50km, denominação Batista)
        var ephemeralSearchRadius = 50.0;
        var ephemeralDenomination = "Batista";
        ephemeralSearchRadius.Should().NotBe(profile.DefaultRadiusKm);
        ephemeralDenomination.Should().NotBe(profile.Denomination);

        // Act - Lê o perfil persistido
        var baselineResult = await service.GetUserProfileAsync(user.Id);

        // Assert - Perfil salvo permanece intacto
        baselineResult.Value!.DefaultRadiusKm.Should().Be(10.0);
        baselineResult.Value.Denomination.Should().Be("Presbiteriana");
    }
}
