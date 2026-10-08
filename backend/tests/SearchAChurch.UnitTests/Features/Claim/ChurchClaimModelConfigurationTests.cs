using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class ChurchClaimModelConfigurationTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Church_ShouldHaveDefaultClaimStatusAndVerificationTier()
    {
        // Act
        var church = new Church();

        // Assert
        church.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        church.VerificationTier.Should().Be(VerificationTier.None);
        church.IsVerified.Should().BeFalse();
        church.VerifiedAt.Should().BeNull();
        church.Claims.Should().BeEmpty();
        church.DisputeCases.Should().BeEmpty();
    }

    [Fact]
    public void ChurchClaim_ShouldHaveExpectedDefaults()
    {
        // Act
        var claim = new ChurchClaim();

        // Assert
        claim.Status.Should().Be(ClaimRecordStatus.Pending);
        claim.TargetTier.Should().Be(VerificationTier.Tier3_SocialPresencial);
        claim.ValidationMethod.Should().Be(ValidationMethod.Geofence);
        claim.AttemptCount.Should().Be(1);
        claim.TosVersion.Should().Be("1.0");
        claim.TosAccepted.Should().BeFalse();
        claim.TosAcceptedAt.Should().BeNull();
        claim.ReminderSentAt.Should().BeNull();
        claim.Evidences.Should().BeEmpty();
    }

    [Fact]
    public void DisputeCase_ShouldHaveExpectedDefaults()
    {
        // Act
        var dispute = new DisputeCase();

        // Assert
        dispute.Status.Should().Be(DisputeStatus.Open);
        dispute.IncumbentUserId.Should().BeNull();
        dispute.IncumbentUser.Should().BeNull();
        dispute.ChallengerDocumentHash.Should().BeNull();
        dispute.IncumbentDocumentHash.Should().BeNull();
        dispute.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ClaimAuditLog_ShouldHave180DaysRetentionDefault()
    {
        // Arrange & Act
        var log = new ClaimAuditLog();

        // Assert
        log.RetentionUntil.Should().BeAfter(log.TimestampUtc.AddDays(179));
        log.RetentionUntil.Should().BeBefore(log.TimestampUtc.AddDays(181));
    }

    [Fact]
    public void DbContext_ModelConfiguration_ShouldConfigureTablesAndTypesProperly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var model = context.Model;

        // Act & Assert
        var churchEntity = model.FindEntityType(typeof(Church));
        churchEntity.Should().NotBeNull();
        churchEntity!.GetTableName().Should().Be("churches");

        var claimEntity = model.FindEntityType(typeof(ChurchClaim));
        claimEntity.Should().NotBeNull();
        claimEntity!.GetTableName().Should().Be("church_claims");

        var evidenceEntity = model.FindEntityType(typeof(ClaimEvidence));
        evidenceEntity.Should().NotBeNull();
        evidenceEntity!.GetTableName().Should().Be("claim_evidences");

        var disputeEntity = model.FindEntityType(typeof(DisputeCase));
        disputeEntity.Should().NotBeNull();
        disputeEntity!.GetTableName().Should().Be("dispute_cases");

        var auditLogEntity = model.FindEntityType(typeof(ClaimAuditLog));
        auditLogEntity.Should().NotBeNull();
        auditLogEntity!.GetTableName().Should().Be("claim_audit_logs");
    }

    [Fact]
    public async Task DbContext_CanPersistAndRetrieve_ChurchClaimWithEvidences()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "pastor@igreja.com.br",
            Name = "Pastor João",
            PasswordHash = "hash123"
        };

        var church = new Church
        {
            Id = Guid.NewGuid(),
            PlaceId = "ChIJ_claim_test_123",
            Name = "Igreja Central",
            FormattedAddress = "Av. Principal, 100",
            Latitude = -23.5505,
            Longitude = -46.6333,
            ClaimStatus = ChurchClaimState.Pending_Verification
        };

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending,
            TargetTier = VerificationTier.Tier3_SocialPresencial,
            ValidationMethod = ValidationMethod.Geofence,
            TosAccepted = true,
            TosVersion = "1.0",
            TosAcceptedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            Evidences = new List<ClaimEvidence>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    EvidenceType = EvidenceType.PhotoGps,
                    RawDataOrUrl = "https://storage.sac.com/photos/church1.jpg",
                    Metadata = "{\"distance_meters\": 35.5, \"accuracy\": 15.0}",
                    IsApproved = false
                }
            }
        };

        await context.Users.AddAsync(user);
        await context.Churches.AddAsync(church);
        await context.ChurchClaims.AddAsync(claim);
        await context.SaveChangesAsync();

        // Act
        var retrievedClaim = await context.ChurchClaims
            .Include(c => c.Church)
            .Include(c => c.User)
            .Include(c => c.Evidences)
            .FirstOrDefaultAsync(c => c.Id == claim.Id);

        // Assert
        retrievedClaim.Should().NotBeNull();
        retrievedClaim!.Church.Name.Should().Be("Igreja Central");
        retrievedClaim.User.Email.Should().Be("pastor@igreja.com.br");
        retrievedClaim.TosAccepted.Should().BeTrue();
        retrievedClaim.Evidences.Should().HaveCount(1);
        retrievedClaim.Evidences.First().EvidenceType.Should().Be(EvidenceType.PhotoGps);
    }

    [Fact]
    public async Task DbContext_CanPersistAndRetrieve_DisputeCase()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var incumbent = new User
        {
            Id = Guid.NewGuid(),
            Email = "incumbent@igreja.com",
            Name = "Gestor Antigo",
            PasswordHash = "hash1"
        };

        var challenger = new User
        {
            Id = Guid.NewGuid(),
            Email = "challenger@igreja.com",
            Name = "Presidente Atual",
            PasswordHash = "hash2"
        };

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja em Litígio",
            FormattedAddress = "Rua da Paz, 1",
            Latitude = -22.9068,
            Longitude = -43.1729,
            ClaimStatus = ChurchClaimState.In_Dispute
        };

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = DateTimeOffset.UtcNow.AddDays(5),
            ChallengerDocumentHash = "sha256_challenger_cert_abc",
            IncumbentDocumentHash = "sha256_incumbent_cert_xyz",
            ResolutionNotes = "Aguardando prazo de 5 dias úteis do RCPJ"
        };

        await context.Users.AddRangeAsync(incumbent, challenger);
        await context.Churches.AddAsync(church);
        await context.DisputeCases.AddAsync(dispute);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.DisputeCases
            .Include(d => d.Church)
            .Include(d => d.ChallengerUser)
            .Include(d => d.IncumbentUser)
            .FirstOrDefaultAsync(d => d.Id == dispute.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Church.Name.Should().Be("Igreja em Litígio");
        retrieved.ChallengerUser.Name.Should().Be("Presidente Atual");
        retrieved.IncumbentUser?.Name.Should().Be("Gestor Antigo");
        retrieved.Status.Should().Be(DisputeStatus.Open);
        retrieved.ChallengerDocumentHash.Should().Be("sha256_challenger_cert_abc");
    }

    [Fact]
    public async Task DbContext_CanPersistAndRetrieve_ClaimAuditLogUnderMarcoCivil()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var churchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var auditLog = new ClaimAuditLog
        {
            Id = Guid.NewGuid(),
            EventType = "ClaimInitiated",
            ChurchId = churchId,
            UserId = userId,
            ClientIp = "200.180.10.5",
            ClientPort = 44321,
            TimestampUtc = now,
            UserAgent = "SearchAChurch-Mobile/1.0.0 (Android 14)",
            VerificationMetadata = "{\"tos_version\":\"1.0\",\"art_299_accepted\":true}",
            RetentionUntil = now.AddDays(180)
        };

        await context.ClaimAuditLogs.AddAsync(auditLog);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.ClaimAuditLogs.FirstOrDefaultAsync(l => l.Id == auditLog.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.EventType.Should().Be("ClaimInitiated");
        retrieved.ClientIp.Should().Be("200.180.10.5");
        retrieved.ClientPort.Should().Be(44321);
        retrieved.UserAgent.Should().Contain("Android 14");
        retrieved.RetentionUntil.Should().BeAfter(now.AddDays(179));
    }

    [Fact]
    public async Task DbContext_SoftDeleteFilter_HidesSoftDeletedChurchesAndClaims()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@igreja.com",
            Name = "Membro",
            PasswordHash = "hash"
        };

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Deletada",
            FormattedAddress = "Rua Teste, 10",
            Latitude = -23.0,
            Longitude = -46.0,
            DeletedAt = DateTimeOffset.UtcNow
        };

        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = user.Id,
            Status = ClaimRecordStatus.Pending
        };

        await context.Users.AddAsync(user);
        await context.Churches.AddAsync(church);
        await context.ChurchClaims.AddAsync(claim);
        await context.SaveChangesAsync();

        // Act
        var activeChurches = await context.Churches.ToListAsync();
        var activeClaims = await context.ChurchClaims.ToListAsync();

        // Assert
        activeChurches.Should().BeEmpty();
        activeClaims.Should().BeEmpty();
    }
}
