using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Claim.Services;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class ClaimOrchestratorTests
{
    private class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public TestTimeProvider(DateTimeOffset initialUtcNow) => _utcNow = initialUtcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClaimOrchTestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static Church CreateTestChurch(
        AppDbContext context,
        Guid? id = null,
        string name = "Igreja Graça e Paz",
        ChurchClaimState state = ChurchClaimState.Unclaimed,
        VerificationTier tier = VerificationTier.None,
        Guid? verifiedByUserId = null)
    {
        var church = new Church
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            FormattedAddress = "Rua da Esperança, 123",
            Latitude = -23.55,
            Longitude = -46.63,
            ClaimStatus = state,
            VerificationTier = tier,
            VerifiedByUserId = verifiedByUserId,
            IsVerified = state == ChurchClaimState.Verified
        };
        context.Churches.Add(church);
        return church;
    }

    private static User CreateTestUser(AppDbContext context, Guid? id = null, string name = "Pastor Teste")
    {
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Email = $"{Guid.NewGuid():N}@igreja.com.br",
            PasswordHash = "hash123",
            Role = UserRole.ChurchRep
        };
        context.Users.Add(user);
        return user;
    }

    private readonly Mock<IClaimAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IClaimNotificationService> _notificationServiceMock = new();
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

    private readonly ClaimConnectionMetadata _testConnection = new(
        ClientIp: "200.180.10.5",
        ClientPort: 44321,
        UserAgent: "SearchAChurch.App/1.0",
        TimestampUtc: new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero)
    );

    private ClaimOrchestratorService CreateOrchestrator(AppDbContext dbContext)
    {
        return new ClaimOrchestratorService(
            dbContext,
            _auditLogServiceMock.Object,
            _notificationServiceMock.Object,
            NullLogger<ClaimOrchestratorService>.Instance,
            _timeProvider
        );
    }

    #region 1. InitiateClaimAsync Tests

    [Fact]
    public async Task InitiateClaimAsync_WhenArt299NotAccepted_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var orchestrator = CreateOrchestrator(context);

        var request = new InitiateClaimRequest(
            ChurchId: Guid.NewGuid(),
            TosVersion: "1.0",
            Art299Accepted: false,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TERMOS_NAO_ACEITOS");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenSimplifiedFlowAndIntermediaryClauseNotAccepted_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var orchestrator = CreateOrchestrator(context);

        var request = new InitiateClaimRequest(
            ChurchId: Guid.NewGuid(),
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: false, // Rejeitou cláusula de intermediária técnica
            ValidationMethod: ValidationMethod.SocialBio,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAUSULA_INTERMEDIARIA_OBRIGATORIA");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenChurchNotFound_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var orchestrator = CreateOrchestrator(context);

        var request = new InitiateClaimRequest(
            ChurchId: Guid.NewGuid(),
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenChurchAlreadyVerified_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Verified, tier: VerificationTier.Tier1_Cartorio);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.CartorioRcpj,
            TargetTier: VerificationTier.Tier1_Cartorio
        );

        var result = await orchestrator.InitiateClaimAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_JA_REIVINDICADA");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenChurchInDispute_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPUTA_EM_ANDAMENTO");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenUserHasThreeRecentRejections_ReturnsLockoutFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context);
        var user = CreateTestUser(context);

        // Adicionar 3 rejeições nas últimas 24h
        for (int i = 0; i < 3; i++)
        {
            context.ChurchClaims.Add(new ChurchClaim
            {
                Id = Guid.NewGuid(),
                ChurchId = church.Id,
                UserId = user.Id,
                Status = ClaimRecordStatus.Rejected,
                CreatedAt = _timeProvider.GetUtcNow().AddHours(-10)
            });
        }
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, user.Id, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("BLOQUEIO_TEMPORARIO_TENTATIVAS");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenSameUserAlreadyHasActivePendingClaim_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        context.ChurchClaims.Add(new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        });
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, user.Id, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAIM_JA_EM_ANDAMENTO");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenAnotherUserHasActivePendingClaimWithSameOrHigherTier_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user1 = CreateTestUser(context, name: "Usuário 1");
        var user2 = CreateTestUser(context, name: "Usuário 2");

        context.ChurchClaims.Add(new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user1.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier3_SocialPresencial,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        });
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.Geofence,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        var result = await orchestrator.InitiateClaimAsync(request, user2.Id, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAIM_PENDENTE_OUTRO_USUARIO");
    }

    [Fact]
    public async Task InitiateClaimAsync_WhenAnotherUserHasPendingTier3_AndNewUserSubmitsTier1_OverridesAndCancelsLowerTier()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user1 = CreateTestUser(context, name: "Usuário 1");
        var user2 = CreateTestUser(context, name: "Usuário 2");

        var lowerTierClaim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user1.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier3_SocialPresencial,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        };
        context.ChurchClaims.Add(lowerTierClaim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.CartorioRcpj,
            TargetTier: VerificationTier.Tier1_Cartorio
        );

        // Act
        var result = await orchestrator.InitiateClaimAsync(request, user2.Id, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ClaimRecordStatus.Pending);
        result.Value.TargetTier.Should().Be(VerificationTier.Tier1_Cartorio);

        // O claim inferior anterior foi revogado
        var updatedLowerClaim = await context.ChurchClaims.FindAsync(lowerTierClaim.Id);
        updatedLowerClaim!.Status.Should().Be(ClaimRecordStatus.Revoked);
    }

    [Fact]
    public async Task InitiateClaimAsync_DocumentalFlow_SetsTtlTo7DaysAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Unclaimed);
        var user = CreateTestUser(context);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.CartorioRcpj,
            TargetTier: VerificationTier.Tier1_Cartorio
        );

        // Act
        var result = await orchestrator.InitiateClaimAsync(request, user.Id, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TtlHours.Should().Be(168); // 7 dias
        result.Value.ExpiresAt.Should().Be(_timeProvider.GetUtcNow().AddDays(7));

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Pending_Verification);

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimInitiated",
            church.Id,
            user.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateClaimAsync_SocialFlow_SetsTtlTo48HoursAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Unclaimed);
        var user = CreateTestUser(context);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new InitiateClaimRequest(
            ChurchId: church.Id,
            TosVersion: "1.0",
            Art299Accepted: true,
            TechnicalIntermediaryAccepted: true,
            ValidationMethod: ValidationMethod.SocialBio,
            TargetTier: VerificationTier.Tier3_SocialPresencial
        );

        // Act
        var result = await orchestrator.InitiateClaimAsync(request, user.Id, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TtlHours.Should().Be(48); // 48 horas
        result.Value.ExpiresAt.Should().Be(_timeProvider.GetUtcNow().AddHours(48));

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Pending_Verification);
    }

    #endregion

    #region 2. SubmitEvidenceAsync Tests

    [Fact]
    public async Task SubmitEvidenceAsync_WhenClaimNotFound_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var orchestrator = CreateOrchestrator(context);

        var request = new SubmitEvidenceRequest(
            ClaimId: Guid.NewGuid(),
            EvidenceType: EvidenceType.PhotoGps
        );

        var result = await orchestrator.SubmitEvidenceAsync(request, Guid.NewGuid(), _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAIM_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task SubmitEvidenceAsync_WhenUserIsUnauthorized_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context);
        var owner = CreateTestUser(context, name: "Dono");
        var otherUser = CreateTestUser(context, name: "Outro");

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = owner.Id,
            Status = ClaimRecordStatus.Pending,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        };
        context.ChurchClaims.Add(claim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new SubmitEvidenceRequest(
            ClaimId: claim.Id,
            EvidenceType: EvidenceType.PhotoGps
        );

        var result = await orchestrator.SubmitEvidenceAsync(request, otherUser.Id, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task SubmitEvidenceAsync_WhenClaimAlreadyApproved_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context);
        var user = CreateTestUser(context);

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Approved,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        };
        context.ChurchClaims.Add(claim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new SubmitEvidenceRequest(
            ClaimId: claim.Id,
            EvidenceType: EvidenceType.PhotoGps
        );

        var result = await orchestrator.SubmitEvidenceAsync(request, user.Id, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAIM_JA_APROVADO");
    }

    [Fact]
    public async Task SubmitEvidenceAsync_WhenClaimExpiredByTtl_ExpiresClaimAndRevertsChurchToUnclaimed()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(-2) // Expirado há 2 horas
        };
        context.ChurchClaims.Add(claim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new SubmitEvidenceRequest(
            ClaimId: claim.Id,
            EvidenceType: EvidenceType.PhotoGps
        );

        // Act
        var result = await orchestrator.SubmitEvidenceAsync(request, user.Id, _testConnection);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLAIM_EXPIRADO");

        var updatedClaim = await context.ChurchClaims.FindAsync(claim.Id);
        updatedClaim!.Status.Should().Be(ClaimRecordStatus.Expired);

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimExpired",
            church.Id,
            user.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitEvidenceAsync_WhenValidAndWithinTtl_ApprovesClaimUpdatesChurchAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier3_SocialPresencial,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(24)
        };
        context.ChurchClaims.Add(claim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var request = new SubmitEvidenceRequest(
            ClaimId: claim.Id,
            EvidenceType: EvidenceType.PhotoGps,
            RawDataOrUrl: "https://storage.searchachurch.com/evidences/photo1.jpg",
            FileHashSha256: "hash_photo_123",
            MetadataJson: "{\"distanceMeters\": 15.2, \"accuracyMeters\": 8.0}"
        );

        // Act
        var result = await orchestrator.SubmitEvidenceAsync(request, user.Id, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsVerified.Should().BeTrue();
        result.Value.Tier.Should().Be(VerificationTier.Tier3_SocialPresencial);

        var updatedClaim = await context.ChurchClaims
            .Include(c => c.Evidences)
            .FirstOrDefaultAsync(c => c.Id == claim.Id);
        updatedClaim!.Status.Should().Be(ClaimRecordStatus.Approved);
        updatedClaim.Evidences.Should().HaveCount(1);
        updatedClaim.Evidences.First().EvidenceType.Should().Be(EvidenceType.PhotoGps);
        updatedClaim.Evidences.First().FileHashSha256.Should().Be("hash_photo_123");

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        updatedChurch.VerificationTier.Should().Be(VerificationTier.Tier3_SocialPresencial);
        updatedChurch.VerifiedByUserId.Should().Be(user.Id);
        updatedChurch.IsVerified.Should().BeTrue();
        updatedChurch.VerifiedAt.Should().NotBeNull();

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "EvidenceSubmitted",
            church.Id,
            user.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimApproved",
            church.Id,
            user.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyClaimApprovedAsync(
            user.Id,
            church.Id,
            church.Name,
            VerificationTier.Tier3_SocialPresencial,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 3. GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_WhenChurchNotFound_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var orchestrator = CreateOrchestrator(context);

        var result = await orchestrator.GetStatusAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task GetStatusAsync_WhenChurchVerifiedAndUserIsRepresentative_ReturnsExpectedStatus()
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateTestUser(context);
        var church = CreateTestChurch(context, state: ChurchClaimState.Verified, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: user.Id);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var result = await orchestrator.GetStatusAsync(church.Id, user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsVerified.Should().BeTrue();
        result.Value.IsCurrentUserRepresentative.Should().BeTrue();
        result.Value.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        result.Value.VerificationTier.Should().Be(VerificationTier.Tier1_Cartorio);
    }

    [Fact]
    public async Task GetStatusAsync_WhenUserHasPendingClaim_CalculatesTimeRemaining()
    {
        using var context = CreateInMemoryDbContext();
        var user = CreateTestUser(context);
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);

        var expiresAt = _timeProvider.GetUtcNow().AddHours(30);
        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            ExpiresAt = expiresAt
        };
        context.ChurchClaims.Add(claim);
        await context.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(context);
        var result = await orchestrator.GetStatusAsync(church.Id, user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ActiveClaimId.Should().Be(claim.Id);
        result.Value.ActiveClaimStatus.Should().Be(ClaimRecordStatus.Pending);
        result.Value.TimeRemaining.Should().BeCloseTo(TimeSpan.FromHours(30), precision: TimeSpan.FromSeconds(5));
    }

    #endregion

    #region 4. ClaimTtlBackgroundService Tests

    [Fact]
    public async Task ClaimTtlBackgroundService_ExpiresPendingClaimsPastDeadlineAndRevertsChurchToUnclaimed()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        var expiredClaim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier3_SocialPresencial,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(-10) // Venceu há 10 min
        };
        context.ChurchClaims.Add(expiredClaim);
        await context.SaveChangesAsync();

        // Configurar ServiceProvider simulado
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_ => context);
        serviceCollection.AddScoped(_ => _auditLogServiceMock.Object);
        serviceCollection.AddScoped(_ => _notificationServiceMock.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceProvider.CreateScope);

        var worker = new ClaimTtlBackgroundService(
            scopeFactoryMock.Object,
            NullLogger<ClaimTtlBackgroundService>.Instance,
            _timeProvider
        );

        // Act: Executar o ciclo de checagem de TTL
        await worker.ProcessTtlChecksAsync();

        // Assert
        var updatedClaim = await context.ChurchClaims.FindAsync(expiredClaim.Id);
        updatedClaim!.Status.Should().Be(ClaimRecordStatus.Expired);

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimExpired",
            church.Id,
            user.Id,
            It.IsAny<ClaimConnectionMetadata>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyClaimExpiredAsync(
            user.Id,
            church.Id,
            church.Name,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimTtlBackgroundService_Sends24HoursReminderBeforeExpiry()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        var claimNearExpiry = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier1_Cartorio,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(20), // Restam 20h (< 24h)
            ReminderSentAt = null
        };
        context.ChurchClaims.Add(claimNearExpiry);
        await context.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_ => context);
        serviceCollection.AddScoped(_ => _auditLogServiceMock.Object);
        serviceCollection.AddScoped(_ => _notificationServiceMock.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceProvider.CreateScope);

        var worker = new ClaimTtlBackgroundService(
            scopeFactoryMock.Object,
            NullLogger<ClaimTtlBackgroundService>.Instance,
            _timeProvider
        );

        // Act
        await worker.ProcessTtlChecksAsync();

        // Assert
        var updatedClaim = await context.ChurchClaims.FindAsync(claimNearExpiry.Id);
        updatedClaim!.ReminderSentAt.Should().NotBeNull();
        updatedClaim.ReminderSentAt.Should().Be(_timeProvider.GetUtcNow());
        updatedClaim.Status.Should().Be(ClaimRecordStatus.Pending); // Continua pending!

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimReminderSent",
            church.Id,
            user.Id,
            It.IsAny<ClaimConnectionMetadata>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyClaimExpiringReminderAsync(
            user.Id,
            church.Id,
            church.Name,
            claimNearExpiry.ExpiresAt,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimTtlBackgroundService_WhenReminderAlreadySent_DoesNotResend()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.Pending_Verification);
        var user = CreateTestUser(context);

        var claimAlreadyReminded = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(10),
            ReminderSentAt = _timeProvider.GetUtcNow().AddHours(-5) // Já foi notificado
        };
        context.ChurchClaims.Add(claimAlreadyReminded);
        await context.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_ => context);
        serviceCollection.AddScoped(_ => _auditLogServiceMock.Object);
        serviceCollection.AddScoped(_ => _notificationServiceMock.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceProvider.CreateScope);

        var worker = new ClaimTtlBackgroundService(
            scopeFactoryMock.Object,
            NullLogger<ClaimTtlBackgroundService>.Instance,
            _timeProvider
        );

        // Act
        await worker.ProcessTtlChecksAsync();

        // Assert: Nenhuma nova notificação de lembrete
        _notificationServiceMock.Verify(n => n.NotifyClaimExpiringReminderAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
