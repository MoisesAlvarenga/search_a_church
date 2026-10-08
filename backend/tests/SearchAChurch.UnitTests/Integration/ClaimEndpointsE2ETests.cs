using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Claim.Endpoints;
using SearchAChurch.Api.Features.Claim.Gateways;
using SearchAChurch.Api.Features.Claim.Services;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Integration;

[Trait("Category", "E2E")]
public class ClaimEndpointsE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITokenService _tokenService;

    public ClaimEndpointsE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    private (User User, string Token) CreateTestUser(string? emailPrefix = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{emailPrefix ?? "claim_user"}_{Guid.NewGuid():N}@church.org",
            Name = "Claim Pastor Tester",
            PasswordHash = "hashed_pw",
            Role = UserRole.User,
            IsVerifiedRepresentative = false
        };

        db.Users.Add(user);
        db.SaveChanges();

        var token = _tokenService.GenerateAccessToken(user, Guid.NewGuid());
        return (user, token);
    }

    private Church CreateTestChurch(ChurchClaimState claimState = ChurchClaimState.Unclaimed, Guid? ownerUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = $"Igreja Batista Central {Guid.NewGuid():N}",
            FormattedAddress = "Av. Paulista, 1000 - Bela Vista, São Paulo - SP",
            Latitude = -23.561684,
            Longitude = -46.655981,
            ClaimStatus = claimState,
            VerifiedByUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Churches.Add(church);
        db.SaveChanges();
        return church;
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content != null)
        {
            request.Content = content;
        }
        return request;
    }

    #region 1. Unauthenticated 401 Unauthorized Checks

    [Theory]
    [InlineData("/claim/initiate", "POST")]
    [InlineData("/claim/verify/geofence", "POST")]
    [InlineData("/claim/verify/social-bio/generate", "POST")]
    [InlineData("/claim/verify/social-bio/confirm", "POST")]
    [InlineData("/claim/verify/domain/send-otp", "POST")]
    [InlineData("/claim/verify/domain/confirm-otp", "POST")]
    [InlineData("/claim/verify/document/rcpj", "POST")]
    [InlineData("/claim/verify/document/qsa", "POST")]
    [InlineData("/claim/dispute/contest", "POST")]
    public async Task ClaimEndpoints_Post_WithoutToken_Return401Unauthorized(string endpoint, string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint)
        {
            Content = JsonContent.Create(new { })
        };
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClaimStatus_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync($"/claim/status/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region 2. Initiate Claim Lifecycle & ToS Checks

    [Fact]
    public async Task InitiateClaim_WithoutArt299Acceptance_Returns400BadRequest()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var church = CreateTestChurch();

        var requestBody = new ApiInitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "v1.0-2026",
            Art299Accepted: false, // Refused
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(requestBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("TERMOS_NAO_ACEITOS");
    }

    [Fact]
    public async Task InitiateClaim_WithoutTechnicalIntermediaryAcceptanceForTier3_Returns400BadRequest()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var church = CreateTestChurch();

        var requestBody = new ApiInitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "v1.0-2026",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: false, // Refused
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(requestBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("CLAUSULA_INTERMEDIARIA_OBRIGATORIA");
    }

    [Fact]
    public async Task InitiateClaim_WhenChurchDoesNotExist_Returns400BadRequest()
    {
        // Arrange
        var (_, token) = CreateTestUser();

        var requestBody = new ApiInitiateClaimRequest(
            ChurchId: Guid.NewGuid(),
            TosVersion: "v1.0-2026",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(requestBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task InitiateClaim_WhenChurchAlreadyVerified_Returns409Conflict()
    {
        // Arrange
        var (existingOwner, _) = CreateTestUser("owner");
        var church = CreateTestChurch(ChurchClaimState.Verified, existingOwner.Id);
        var (_, claimantToken) = CreateTestUser("claimant");

        var requestBody = new ApiInitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "v1.0-2026",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", claimantToken, JsonContent.Create(requestBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("IGREJA_JA_REIVINDICADA");
    }

    [Fact]
    public async Task InitiateClaim_WithValidPayload_Returns200OkWithClaimDetails()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var church = CreateTestChurch();

        var requestBody = new ApiInitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "v1.0-2026",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(requestBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var claimResponse = await res.Content.ReadFromJsonAsync<InitiateClaimResponse>();
        claimResponse.Should().NotBeNull();
        claimResponse!.ClaimId.Should().NotBeEmpty();
        claimResponse.ChurchId.Should().Be(church.Id);
        claimResponse.Status.Should().Be(ClaimRecordStatus.Pending);
        claimResponse.TargetTier.Should().Be(VerificationTier.Tier3_SocialPresencial);
        claimResponse.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    #endregion

    #region 3. Geofence Verification Endpoint (/claim/verify/geofence)

    [Fact]
    public async Task VerifyGeofence_WhenDeviceIsTooFarFromChurch_Returns400BadRequest()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        // Initiate claim first
        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.Geofence, VerificationTier.Tier3_SocialPresencial);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        // Far away location (e.g. Rio de Janeiro vs SP church)
        var geofenceReq = new ApiGeofenceVerificationRequest(
            ClaimId: initData!.ClaimId,
            DeviceLatitude: -22.906847,
            DeviceLongitude: -43.172896,
            HorizontalAccuracyMeters: 10.0,
            IsMockLocation: false
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/geofence", token, JsonContent.Create(geofenceReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("FORA_DO_RAIO_PERMITIDO");
    }

    [Fact]
    public async Task VerifyGeofence_WhenMockLocationDetected_Returns400BadRequest()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.Geofence, VerificationTier.Tier3_SocialPresencial);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        var geofenceReq = new ApiGeofenceVerificationRequest(
            ClaimId: initData!.ClaimId,
            DeviceLatitude: church.Latitude,
            DeviceLongitude: church.Longitude,
            HorizontalAccuracyMeters: 5.0,
            IsMockLocation: true // Anti-mock trigger
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/geofence", token, JsonContent.Create(geofenceReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("LOCALIZACAO_SIMULADA_DETECTADA");
    }

    [Fact]
    public async Task VerifyGeofence_WhenDeviceAtChurchLocation_Returns200OkAndVerifiesClaim()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.Geofence, VerificationTier.Tier3_SocialPresencial);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        var geofenceReq = new ApiGeofenceVerificationRequest(
            ClaimId: initData!.ClaimId,
            DeviceLatitude: church.Latitude,
            DeviceLongitude: church.Longitude,
            HorizontalAccuracyMeters: 10.0,
            IsMockLocation: false,
            PhotoUrl: "https://storage.sac.org/photos/templo_fachada.jpg",
            PhotoHashSha256: new string('a', 64)
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/geofence", token, JsonContent.Create(geofenceReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await res.Content.ReadFromJsonAsync<VerificationResultResponse>();
        result.Should().NotBeNull();
        result!.IsVerified.Should().BeTrue();
        result.Tier.Should().Be(VerificationTier.Tier3_SocialPresencial);
    }

    #endregion

    #region 4. Social Bio Verification Endpoints (/claim/verify/social-bio/*)

    [Fact]
    public async Task SocialBio_GenerateAndConfirmLifecycle_Succeeds()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.SocialBio, VerificationTier.Tier3_SocialPresencial);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        // 1. Generate Token
        var generateReq = new ApiGenerateSocialTokenRequest(initData!.ClaimId, "Instagram", "igrejacentral");
        var genHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/social-bio/generate", token, JsonContent.Create(generateReq));
        var genRes = await _client.SendAsync(genHttp);

        genRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using var genJson = await JsonDocument.ParseAsync(await genRes.Content.ReadAsStreamAsync());
        var generatedToken = genJson.RootElement.GetProperty("token").GetString();
        generatedToken.Should().NotBeNullOrWhiteSpace();

        // 2. Confirm Token with mismatch -> 400
        var failConfirmReq = new ApiConfirmSocialBioRequest(
            ClaimId: initData.ClaimId,
            SocialNetwork: "Instagram",
            ProfileHandle: "igrejacentral",
            ExpectedToken: generatedToken!,
            BioContent: "Perfil oficial da igreja sem o token"
        );
        var failHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/social-bio/confirm", token, JsonContent.Create(failConfirmReq));
        var failRes = await _client.SendAsync(failHttp);
        failRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. Confirm Token with matching bio -> 200 OK
        var successConfirmReq = new ApiConfirmSocialBioRequest(
            ClaimId: initData.ClaimId,
            SocialNetwork: "Instagram",
            ProfileHandle: "igrejacentral",
            ExpectedToken: generatedToken!,
            BioContent: $"Igreja Batista Central. Código de verificação: {generatedToken}"
        );
        var successHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/social-bio/confirm", token, JsonContent.Create(successConfirmReq));
        var successRes = await _client.SendAsync(successHttp);

        successRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmResult = await successRes.Content.ReadFromJsonAsync<VerificationResultResponse>();
        confirmResult.Should().NotBeNull();
        confirmResult!.IsVerified.Should().BeTrue();
    }

    #endregion

    #region 5. Domain Email OTP Verification Endpoints (/claim/verify/domain/*)

    [Fact]
    public async Task DomainOtp_WhenUsingPublicEmailDomain_Returns400BadRequest()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var sendReq = new ApiSendDomainOtpRequest(
            ClaimId: Guid.NewGuid(),
            CorporateEmail: "pastor@gmail.com", // Public domain forbidden
            ExpectedDomain: "gmail.com"
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/domain/send-otp", token, JsonContent.Create(sendReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("DOMINIO_PUBLICO_INVALIDO");
    }

    [Fact]
    public async Task DomainOtp_WhenSendToInstitutionalEmail_SucceedsWith200Ok()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.InstitutionalEmail, VerificationTier.Tier2_Institucional);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        var sendReq = new ApiSendDomainOtpRequest(
            ClaimId: initData!.ClaimId,
            CorporateEmail: "contato@igrejabatistasp.org.br",
            ExpectedDomain: "igrejabatistasp.org.br"
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/domain/send-otp", token, JsonContent.Create(sendReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm with wrong OTP -> 400 Bad Request
        var confirmReq = new ApiConfirmDomainOtpRequest(
            ClaimId: initData.ClaimId,
            CorporateEmail: "contato@igrejabatistasp.org.br",
            OtpCode: "999999" // Wrong code
        );
        var confirmHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/domain/confirm-otp", token, JsonContent.Create(confirmReq));
        var confirmRes = await _client.SendAsync(confirmHttp);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var confirmContent = await confirmRes.Content.ReadAsStringAsync();
        confirmContent.Should().Contain("OTP_INVALIDO_OU_EXPIRADO");
    }

    #endregion

    #region 6. Cartorio RCPJ & QSA Document Verification Endpoints

    [Fact]
    public async Task SubmitCartorioDocument_WithMissingHash_Returns400BadRequest()
    {
        // Arrange
        var (_, token) = CreateTestUser();

        var reqBody = new ApiSubmitCartorioDocumentRequest(
            ClaimId: Guid.NewGuid(),
            DocumentFileName: "estatuto.pdf",
            DocumentFileHashSha256: "", // Empty hash
            AverbationDate: DateTimeOffset.UtcNow.AddYears(-1)
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/document/rcpj", token, JsonContent.Create(reqBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("HASH_DOCUMENTO_OBRIGATORIO");
    }

    [Fact]
    public async Task SubmitCartorioDocument_WithValidPayload_Returns200Ok()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, false, ValidationMethod.CartorioRcpj, VerificationTier.Tier1_Cartorio);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        var reqBody = new ApiSubmitCartorioDocumentRequest(
            ClaimId: initData!.ClaimId,
            DocumentFileName: "ata_posse_rcpj_2026.pdf",
            DocumentFileHashSha256: new string('d', 64),
            AverbationDate: DateTimeOffset.UtcNow.AddMonths(-3),
            DocumentUrl: "https://storage.sac.org/docs/ata_posse.pdf"
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/document/rcpj", token, JsonContent.Create(reqBody));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await res.Content.ReadFromJsonAsync<VerificationResultResponse>();
        result.Should().NotBeNull();
        result!.IsVerified.Should().BeTrue();
        result.Tier.Should().Be(VerificationTier.Tier1_Cartorio);
    }

    [Fact]
    public async Task VerifyQsa_WithInvalidCpfOrCnpj_Returns400BadRequest()
    {
        // Arrange
        var (_, token) = CreateTestUser();

        // Invalid CPF format
        var badCpfReq = new ApiVerifyQsaRequest(
            ClaimId: Guid.NewGuid(),
            ChurchCnpj: "04.252.011/0001-10",
            RepresentativeCpf: "123.456.789-00", // Invalid CPF
            RepresentativeName: "Pastor Teste"
        );

        // Act
        var req1 = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/document/qsa", token, JsonContent.Create(badCpfReq));
        var res1 = await _client.SendAsync(req1);

        // Assert
        res1.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content1 = await res1.Content.ReadAsStringAsync();
        content1.Should().Contain("CPF_INVALIDO");

        // Invalid CNPJ format
        var badCnpjReq = new ApiVerifyQsaRequest(
            ClaimId: Guid.NewGuid(),
            ChurchCnpj: "11.111.111/1111-11", // Invalid CNPJ
            RepresentativeCpf: "111.444.777-35",
            RepresentativeName: "Pastor Teste"
        );

        var req2 = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/document/qsa", token, JsonContent.Create(badCnpjReq));
        var res2 = await _client.SendAsync(req2);

        res2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content2 = await res2.Content.ReadAsStringAsync();
        content2.Should().Contain("CNPJ_INVALIDO");
    }

    [Fact]
    public async Task VerifyQsa_WithValidRepresentative_Returns200Ok()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.ReceitaQsa, VerificationTier.Tier2_Institucional);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        var initData = await initRes.Content.ReadFromJsonAsync<InitiateClaimResponse>();

        var validQsaReq = new ApiVerifyQsaRequest(
            ClaimId: initData!.ClaimId,
            ChurchCnpj: "04.252.011/0001-10",
            RepresentativeCpf: "111.444.777-35",
            RepresentativeName: "Representante Qualificado"
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/verify/document/qsa", token, JsonContent.Create(validQsaReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await res.Content.ReadFromJsonAsync<VerificationResultResponse>();
        result.Should().NotBeNull();
        result!.IsVerified.Should().BeTrue();
        result.Tier.Should().Be(VerificationTier.Tier2_Institucional);
    }

    #endregion

    #region 7. Dispute Endpoints (/claim/dispute/*)

    [Fact]
    public async Task DisputeContest_WhenChurchDoesNotExist_Returns404NotFound()
    {
        // Arrange
        var (_, token) = CreateTestUser();

        var contestReq = new ApiOpenDisputeRequest(
            ChurchId: Guid.NewGuid(),
            TosVersion: "v1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Novo",
            LegalRepresentativeCpf: "111.444.777-35",
            ChurchCnpj: "04.252.011/0001-10",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: new string('e', 64),
            DocumentAverbationDate: DateTimeOffset.UtcNow.AddMonths(-1)
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/dispute/contest", token, JsonContent.Create(contestReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DisputeContest_WithValidContest_FreezesChurchAndReturns200Ok()
    {
        // Arrange: Create verified church with an existing owner at Tier 2
        var (initialOwner, _) = CreateTestUser("initial_pastor");
        var church = CreateTestChurch(ChurchClaimState.Verified, initialOwner.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingClaim = new ChurchClaim
            {
                Id = Guid.NewGuid(),
                ChurchId = church.Id,
                UserId = initialOwner.Id,
                Status = ClaimRecordStatus.Approved,
                TargetTier = VerificationTier.Tier2_Institucional,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
            };
            db.ChurchClaims.Add(existingClaim);
            db.SaveChanges();
        }

        var (contenderUser, contenderToken) = CreateTestUser("contender_pastor");

        var contestReq = new ApiOpenDisputeRequest(
            ChurchId: church.Id,
            TosVersion: "v1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Titular Novo",
            LegalRepresentativeCpf: "111.444.777-35",
            ChurchCnpj: "04.252.011/0001-10",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: new string('f', 64),
            DocumentAverbationDate: DateTimeOffset.UtcNow.AddDays(-1),
            Justification: "Ata cartorial averbada recentemente comprova sucessão pastoral legítima."
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/dispute/contest", contenderToken, JsonContent.Create(contestReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var contestResult = await res.Content.ReadFromJsonAsync<DisputeContestResponse>();
        contestResult.Should().NotBeNull();
        contestResult!.ResolutionType.Should().Be(DisputeResolutionType.AutomaticOverrideN1);
        contestResult.ActiveRepresentativeUserId.Should().Be(contenderUser.Id);

        // Verify church ownership updated to contender
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedChurch = db.Churches.First(c => c.Id == church.Id);
            updatedChurch.VerifiedByUserId.Should().Be(contenderUser.Id);
            updatedChurch.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        }
    }

    [Fact]
    public async Task DisputeSubmitCertificate_WhenDisputeNotFound_Returns404NotFound()
    {
        // Arrange
        var (_, token) = CreateTestUser();

        var submitReq = new ApiSubmitDisputeCertificateRequest(
            DocumentFileHash: new string('9', 64),
            AverbationDate: DateTimeOffset.UtcNow.AddDays(-2)
        );

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Post, $"/claim/dispute/{Guid.NewGuid()}/submit-certificate", token, JsonContent.Create(submitReq));
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region 8. Get Claim Status Endpoint (/claim/status/{churchId})

    [Fact]
    public async Task GetClaimStatus_WhenClaimNotFound_Returns404NotFound()
    {
        // Arrange
        var (_, token) = CreateTestUser();
        var nonExistentChurchId = Guid.NewGuid();

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Get, $"/claim/status/{nonExistentChurchId}", token);
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetClaimStatus_WhenClaimExists_Returns200OkWithStatusDetails()
    {
        // Arrange
        var (user, token) = CreateTestUser();
        var church = CreateTestChurch();

        var initReq = new ApiInitiateClaimRequest(
            church.Id, "v1.0", true, true, ValidationMethod.Geofence, VerificationTier.Tier3_SocialPresencial);
        var initHttp = CreateAuthenticatedRequest(HttpMethod.Post, "/claim/initiate", token, JsonContent.Create(initReq));
        var initRes = await _client.SendAsync(initHttp);
        initRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var req = CreateAuthenticatedRequest(HttpMethod.Get, $"/claim/status/{church.Id}", token);
        var res = await _client.SendAsync(req);

        // Assert
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusData = await res.Content.ReadFromJsonAsync<ChurchClaimStatusResponse>();
        statusData.Should().NotBeNull();
        statusData!.ChurchId.Should().Be(church.Id);
        statusData.ClaimStatus.Should().Be(ChurchClaimState.Pending_Verification);
        statusData.IsCurrentUserRepresentative.Should().BeFalse();
    }

    #endregion
}
