using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.UnitTests.Integration;

[Trait("Category", "Integration")]
public class ProfileEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProfileEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string GenerateUserToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerateAccessToken(user, Guid.NewGuid());
    }

    private async Task<User> SeedUserAsync(bool isVerifiedRep = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user_{Guid.NewGuid():N}@searchachurch.org",
            Name = "Membro da Igreja",
            PasswordHash = "hashed_pw",
            Role = isVerifiedRep ? UserRole.ChurchRep : UserRole.User,
            IsVerifiedRepresentative = isVerifiedRep
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<Church> SeedChurchAsync(Guid? verifiedUserId = null, bool isActive = true, string? placeId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Primeira Igreja Batista do Teste",
            FormattedAddress = "Rua das Oliveiras, 100 - Centro",
            Latitude = -23.550520,
            Longitude = -46.633308,
            Denomination = "Batista",
            WorshipStyle = "Contemporâneo",
            Languages = new List<string> { "pt" },
            PlaceId = placeId ?? $"place_{Guid.NewGuid():N}",
            IsActive = isActive,
            ClaimStatus = verifiedUserId.HasValue ? ChurchClaimState.Verified : ChurchClaimState.Unclaimed,
            IsVerified = verifiedUserId.HasValue,
            VerifiedByUserId = verifiedUserId,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        db.Churches.Add(church);
        await db.SaveChangesAsync();
        return church;
    }

    private async Task SeedTagCatalogAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.TagCatalogs.Any())
        {
            db.TagCatalogs.AddRange(
                new TagCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = "rampa_acesso",
                    Name = "Rampa de Acesso",
                    Category = TagCategory.Accessibility,
                    Description = "Acesso pleno a cadeirantes",
                    IconName = "accessible",
                    IsActive = true,
                    DisplayOrder = 1
                },
                new TagCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = "estacionamento_proprio",
                    Name = "Estacionamento Próprio",
                    Category = TagCategory.Infrastructure,
                    Description = "Vagas gratuitas",
                    IconName = "local_parking",
                    IsActive = true,
                    DisplayOrder = 2
                },
                new TagCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = "ministerio_jovens",
                    Name = "Ministério de Jovens",
                    Category = TagCategory.Ministries,
                    Description = "Atividades para a juventude",
                    IconName = "groups",
                    IsActive = true,
                    DisplayOrder = 3
                }
            );
            await db.SaveChangesAsync();
        }
    }

    #region User Profile Tests

    [Fact]
    public async Task GetUserProfile_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/profile/user");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserProfile_WithValidToken_Returns200OkWithUserProfile()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.UserId.Should().Be(user.Id);
        content.Name.Should().Be(user.Name);
    }

    [Fact]
    public async Task UpdateUserProfile_WithValidPreferences_Returns200OkAndPersistsBaseline()
    {
        // Arrange
        await SeedTagCatalogAsync();
        var user = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var updatePayload = new UpdateUserProfileRequest(
            Denomination: "Presbiteriana",
            WorshipStyle: "Tradicional",
            PreferredLanguages: new List<string> { "pt", "en" },
            DefaultRadiusKm: 25.0,
            TagCodes: new List<string> { "rampa_acesso", "estacionamento_proprio" }
        );

        var request = new HttpRequestMessage(HttpMethod.Put, "/profile/user")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.Denomination.Should().Be("Presbiteriana");
        content.DefaultRadiusKm.Should().Be(25.0);
        content.SelectedTags.Should().Contain(new[] { "rampa_acesso", "estacionamento_proprio" });
    }

    [Fact]
    public async Task UpdateUserProfile_WithInvalidRadius_Returns400BadRequest()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var updatePayload = new UpdateUserProfileRequest(
            Denomination: "Batista",
            WorshipStyle: "Contemporâneo",
            PreferredLanguages: new List<string> { "pt" },
            DefaultRadiusKm: 150.0, // Acima do teto de 100km
            TagCodes: new List<string>()
        );

        var request = new HttpRequestMessage(HttpMethod.Put, "/profile/user")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("RAIO_INVALIDO");
    }

    [Fact]
    public async Task UpdateUserProfile_WithInvalidTagCode_Returns400BadRequest()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var updatePayload = new UpdateUserProfileRequest(
            Denomination: "Batista",
            WorshipStyle: "Contemporâneo",
            PreferredLanguages: new List<string> { "pt" },
            DefaultRadiusKm: 10.0,
            TagCodes: new List<string> { "tag_completamente_inexistente" }
        );

        var request = new HttpRequestMessage(HttpMethod.Put, "/profile/user")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TAG_INVALIDA");
    }

    [Fact]
    public async Task UpdateUserProfile_AttemptingToModifyAnotherUser_Returns403Forbidden()
    {
        // Arrange
        var user = await SeedUserAsync();
        var otherUser = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var updatePayload = new UpdateUserProfileRequest(
            Denomination: "Batista",
            WorshipStyle: "Contemporâneo",
            PreferredLanguages: new List<string> { "pt" },
            DefaultRadiusKm: 10.0,
            TagCodes: new List<string>(),
            TargetUserId: otherUser.Id
        );

        var request = new HttpRequestMessage(HttpMethod.Put, "/profile/user")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ACESSO_NEGADO_PROPRIEDADE");
    }

    [Fact]
    public async Task DeleteUserAccount_WithValidToken_Returns200OkAndAnonymizesDataUnderLgpd()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = GenerateUserToken(user);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/profile/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("LGPD");

        // Verify in DB that account was anonymized
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedUser = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.Name.Should().Be("Usuário Anônimo");
        updatedUser.Email.Should().Contain("@anonymized.searchachurch.org");
    }

    #endregion

    #region Church Profile Tests

    [Fact]
    public async Task GetChurchProfile_ForActiveChurch_Anonymous_Returns200Ok()
    {
        // Arrange
        var church = await SeedChurchAsync(isActive: true);

        // Act
        var response = await _client.GetAsync($"/profile/church/{church.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ChurchProfileResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.Id.Should().Be(church.Id);
        content.Name.Should().Be(church.Name);
    }

    [Fact]
    public async Task GetChurchProfile_ForNonExistentChurch_Returns404NotFound()
    {
        var response = await _client.GetAsync($"/profile/church/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task GetChurchProfile_ForInactiveChurch_Anonymous_Returns404NotFound()
    {
        // Arrange
        var church = await SeedChurchAsync(isActive: false);

        // Act
        var response = await _client.GetAsync($"/profile/church/{church.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChurchProfile_ForInactiveChurch_ByVerifiedRepresentative_Returns200Ok()
    {
        // Arrange
        var pastor = await SeedUserAsync(isVerifiedRep: true);
        var church = await SeedChurchAsync(verifiedUserId: pastor.Id, isActive: false);
        var token = GenerateUserToken(pastor);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/profile/church/{church.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ChurchProfileResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateChurchProfile_ByVerifiedRepresentative_Returns200Ok()
    {
        // Arrange
        await SeedTagCatalogAsync();
        var pastor = await SeedUserAsync(isVerifiedRep: true);
        var church = await SeedChurchAsync(verifiedUserId: pastor.Id, isActive: true);
        var token = GenerateUserToken(pastor);

        var updatePayload = new UpdateChurchProfileRequest(
            Name: "Primeira Igreja Reformada Atualizada",
            Address: "Av. Paulista, 2000",
            Latitude: -23.561414,
            Longitude: -46.655881,
            Denomination: "Presbiteriana",
            WorshipStyle: "Tradicional",
            Languages: new List<string> { "pt", "en" },
            TagCodes: new List<string> { "rampa_acesso" },
            Schedules: new List<MeetingScheduleDto>
            {
                new(Guid.Empty, 0, "10:00", "Culto Solene", "pt")
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Put, $"/profile/church/{church.Id}")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ChurchProfileResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.Name.Should().Be("Primeira Igreja Reformada Atualizada");
        content.Tags.Should().Contain("rampa_acesso");
        content.Schedules.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateChurchProfile_WithoutRepresentativePrivilege_Returns403Forbidden()
    {
        // Arrange
        var seekerUser = await SeedUserAsync(isVerifiedRep: false);
        var church = await SeedChurchAsync(verifiedUserId: Guid.NewGuid(), isActive: true);
        var token = GenerateUserToken(seekerUser);

        var updatePayload = new UpdateChurchProfileRequest(
            Name: "Igreja Hackeada",
            Address: "Rua Hacker",
            Latitude: 0,
            Longitude: 0
        );

        var request = new HttpRequestMessage(HttpMethod.Put, $"/profile/church/{church.Id}")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task UpdateChurchProfile_WithDuplicatePlaceId_Returns409Conflict()
    {
        // Arrange
        var pastor = await SeedUserAsync(isVerifiedRep: true);
        var existingChurch = await SeedChurchAsync(placeId: "place_existente_123");
        var churchToUpdate = await SeedChurchAsync(verifiedUserId: pastor.Id, isActive: true);
        var token = GenerateUserToken(pastor);

        var updatePayload = new UpdateChurchProfileRequest(
            Name: "Igreja com Conflito de PlaceId",
            Address: "Av. Brasil, 500",
            Latitude: -23.55,
            Longitude: -46.63,
            PlaceId: existingChurch.PlaceId // Conflito com igreja já ativa
        );

        var request = new HttpRequestMessage(HttpMethod.Put, $"/profile/church/{churchToUpdate.Id}")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("PLACE_ID_JA_VINCULADO");
    }

    [Fact]
    public async Task UpdateChurchProfile_WithOutdatedIfMatchHeader_Returns409Conflict()
    {
        // Arrange
        var pastor = await SeedUserAsync(isVerifiedRep: true);
        var church = await SeedChurchAsync(verifiedUserId: pastor.Id, isActive: true);
        var token = GenerateUserToken(pastor);

        var updatePayload = new UpdateChurchProfileRequest(
            Name: "Igreja com Concorrência Defasada",
            Address: "Av. Brasil, 500",
            Latitude: -23.55,
            Longitude: -46.63
        );

        var request = new HttpRequestMessage(HttpMethod.Put, $"/profile/church/{church.Id}")
        {
            Content = JsonContent.Create(updatePayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"stamp_totalmente_desatualizado\""));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("CONFLITO_CONCORRENCIA");
    }

    [Fact]
    public async Task UpdateChurchStatus_ByVerifiedRepresentative_Returns200OkAndTogglesStatus()
    {
        // Arrange
        var pastor = await SeedUserAsync(isVerifiedRep: true);
        var church = await SeedChurchAsync(verifiedUserId: pastor.Id, isActive: true);
        var token = GenerateUserToken(pastor);

        var statusPayload = new UpdateChurchStatusRequest(IsActive: false);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/profile/church/{church.Id}/status")
        {
            Content = JsonContent.Create(statusPayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ChurchStatusResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.IsActive.Should().BeFalse();
        content.Message.Should().Contain("inativada");
    }

    [Fact]
    public async Task UpdateChurchStatus_ByNonRepresentative_Returns403Forbidden()
    {
        // Arrange
        var nonRep = await SeedUserAsync(isVerifiedRep: false);
        var church = await SeedChurchAsync(verifiedUserId: Guid.NewGuid(), isActive: true);
        var token = GenerateUserToken(nonRep);

        var statusPayload = new UpdateChurchStatusRequest(IsActive: false);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/profile/church/{church.Id}/status")
        {
            Content = JsonContent.Create(statusPayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("REPRESENTANTE_NAO_VERIFICADO");
    }

    #endregion

    #region Tag Catalog Tests

    [Fact]
    public async Task GetTagCatalog_Anonymous_Returns200OkWithGroupedCategories()
    {
        // Arrange
        await SeedTagCatalogAsync();

        // Act
        var response = await _client.GetAsync("/tags/catalog");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<TagCatalogResponse>(JsonOptions);
        content.Should().NotBeNull();
        content!.Categories.Should().NotBeEmpty();
        content.Categories.Select(c => c.Name).Should().Contain(new[] { "Acessibilidade", "Infraestrutura", "Ministérios" });
    }

    #endregion
}
