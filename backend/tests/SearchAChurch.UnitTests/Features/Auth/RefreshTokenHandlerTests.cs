using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Features.Auth.Validators;
using SearchAChurch.Api.Services;

namespace SearchAChurch.UnitTests.Features.Auth;

[Trait("Category", "Unit")]
public class RefreshTokenHandlerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<ILogger<RefreshTokenHandler>> _loggerMock = new();
    private readonly RefreshTokenRequestValidator _validator = new();

    public RefreshTokenHandlerTests()
    {
        _tokenServiceMock.Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns<string>(raw => $"hash_of_{raw}");
        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns("jwt.mock.new-access-token");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken())
            .Returns(("raw-new-refresh-token", "hash_of_raw-new-refresh-token"));
    }

    private class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public TestTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_WithActiveToken_ShouldConsumeCurrentAndGenerateNewPairPreservingFamilyId()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "usuario.rtr@exemplo.com",
            Name = "Usuario RTR",
            Role = UserRole.User
        };
        var familyId = Guid.NewGuid();
        var initialToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_rt-active-123",
            DeviceId = "device-ios",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            ClientIp = "189.1.2.3",
            UserAgent = "App/1.0"
        };
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(initialToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("rt-active-123", "device-ios");
        var clientContext = new ClientConnectionContext("189.1.2.3", 443, "App/1.0");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt.mock.new-access-token");
        result.Value.RefreshToken.Should().Be("raw-new-refresh-token");
        result.Value.ExpiresIn.Should().Be(900);

        // Verify initial token was Consumed
        var updatedInitial = await dbContext.RefreshTokens.FindAsync(initialToken.Id);
        updatedInitial!.Status.Should().Be(RefreshTokenStatus.Consumed);
        updatedInitial.ConsumedAt.Should().NotBeNull();

        // Verify new token was created with same FamilyId and Active status (+60d)
        var tokensInFamily = await dbContext.RefreshTokens.Where(rt => rt.FamilyId == familyId).ToListAsync();
        tokensInFamily.Should().HaveCount(2);

        var newToken = tokensInFamily.FirstOrDefault(rt => rt.Status == RefreshTokenStatus.Active);
        newToken.Should().NotBeNull();
        newToken!.TokenHash.Should().Be("hash_of_raw-new-refresh-token");
        newToken.DeviceId.Should().Be("device-ios");
        newToken.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(59));

        // Audit log verified
        _auditLogMock.Verify(a => a.LogEventAsync(
            "REFRESH",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithConsumedToken_OutsideToleranceWindow_ShouldDetectBreachAndRevokeAllTokensInFamily()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "vitima@exemplo.com",
            Name = "Vitima",
            Role = UserRole.User
        };
        var familyId = Guid.NewGuid();

        // Old consumed token (consumed 10 seconds ago)
        var stolenConsumedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_token-roubado-ja-consumido",
            DeviceId = "device-vitima",
            Status = RefreshTokenStatus.Consumed,
            ConsumedAt = fixedNow.AddSeconds(-10),
            ExpiresAt = fixedNow.AddDays(30),
            ClientIp = "189.1.2.3",
            UserAgent = "App/1.0"
        };

        // Active token currently used by legitimate device
        var activeLegitToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_token-legitimo-ativo",
            DeviceId = "device-vitima",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = fixedNow.AddDays(60),
            ClientIp = "189.1.2.3",
            UserAgent = "App/1.0"
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.AddRange(stolenConsumedToken, activeLegitToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object,
            timeProvider);

        // Attacker attempts to reuse the stolen consumed token
        var attackerRequest = new RefreshTokenRequest("token-roubado-ja-consumido", "device-atacante");
        var attackerContext = new ClientConnectionContext("200.50.60.70", 12345, "AttackerTool/2.0");

        // Act
        var result = await handler.HandleAsync(attackerRequest, attackerContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_BREACH_DETECTED");
        result.ErrorMessage.Should().Contain("Tentativa de violação detectada");

        // Verify ALL tokens in the compromised family are now Revoked
        var familyTokens = await dbContext.RefreshTokens.Where(rt => rt.FamilyId == familyId).ToListAsync();
        familyTokens.Should().HaveCount(2);
        familyTokens.Should().OnlyContain(t => t.Status == RefreshTokenStatus.Revoked);

        // Verify Critical Audit Log was generated
        _auditLogMock.Verify(a => a.LogEventAsync(
            "TOKEN_BREACH_DETECTED",
            user.Id,
            attackerContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithConsumedToken_Within2SecTolerance_SameDeviceAndIp_ShouldAllowReplayWithoutBreach()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "concorrente@exemplo.com",
            Name = "Usuario Concorrente",
            Role = UserRole.User
        };
        var familyId = Guid.NewGuid();

        // Token consumed just 1 second ago by parallel in-flight call
        var inFlightConsumedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_rt-inflight",
            DeviceId = "device-iphone-14",
            Status = RefreshTokenStatus.Consumed,
            ConsumedAt = fixedNow.AddSeconds(-1),
            ExpiresAt = fixedNow.AddDays(50),
            ClientIp = "189.10.20.30",
            UserAgent = "Mobile/1.0"
        };

        var activeChildToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_rt-active-child",
            DeviceId = "device-iphone-14",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = fixedNow.AddDays(60),
            ClientIp = "189.10.20.30",
            UserAgent = "Mobile/1.0",
            CreatedAt = fixedNow
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.AddRange(inFlightConsumedToken, activeChildToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object,
            timeProvider);

        var request = new RefreshTokenRequest("rt-inflight", "device-iphone-14");
        var clientContext = new ClientConnectionContext("189.10.20.30", 443, "Mobile/1.0");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt.mock.new-access-token");

        // Verify tokens in family were NOT revoked
        var activeToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == "hash_of_rt-active-child");
        activeToken!.Status.Should().Be(RefreshTokenStatus.Active);
    }

    [Fact]
    public async Task HandleAsync_WithConsumedToken_Within2SecTolerance_DifferentDevice_ShouldDetectBreach()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "breach.device@exemplo.com",
            Name = "Breach Device",
            Role = UserRole.User
        };
        var familyId = Guid.NewGuid();

        var consumedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_rt-consumed-diff-dev",
            DeviceId = "legitimate-device",
            Status = RefreshTokenStatus.Consumed,
            ConsumedAt = fixedNow.AddSeconds(-1),
            ExpiresAt = fixedNow.AddDays(50),
            ClientIp = "189.10.20.30",
            UserAgent = "Mobile/1.0"
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(consumedToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object,
            timeProvider);

        // Different DeviceId
        var request = new RefreshTokenRequest("rt-consumed-diff-dev", "attacker-device");
        var clientContext = new ClientConnectionContext("189.10.20.30", 443, "Mobile/1.0");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_BREACH_DETECTED");
    }

    [Fact]
    public async Task HandleAsync_WithConsumedToken_Within2SecTolerance_DifferentIp_ShouldDetectBreach()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "breach.ip@exemplo.com",
            Name = "Breach IP",
            Role = UserRole.User
        };
        var familyId = Guid.NewGuid();

        var consumedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = familyId,
            TokenHash = "hash_of_rt-consumed-diff-ip",
            DeviceId = "device-1",
            Status = RefreshTokenStatus.Consumed,
            ConsumedAt = fixedNow.AddSeconds(-1),
            ExpiresAt = fixedNow.AddDays(50),
            ClientIp = "189.10.20.30",
            UserAgent = "Mobile/1.0"
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(consumedToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object,
            timeProvider);

        // Different IP
        var request = new RefreshTokenRequest("rt-consumed-diff-ip", "device-1");
        var clientContext = new ClientConnectionContext("200.1.2.3", 443, "Mobile/1.0");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_BREACH_DETECTED");
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentToken_ShouldReturnInvalidTokenError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("token-inexistente", "device-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_TOKEN");
        result.ErrorMessage.Should().Contain("inválido");
    }

    [Fact]
    public async Task HandleAsync_WithExpiredToken_ShouldReturnTokenExpiredError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "expirado@exemplo.com",
            Name = "Expirado",
            Role = UserRole.User
        };
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash_of_rt-expired",
            DeviceId = "device-1",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Expirado ontem
            ClientIp = "127.0.0.1",
            UserAgent = "App/1.0"
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(expiredToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("rt-expired", "device-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_EXPIRED");
        result.ErrorMessage.Should().Contain("expirada");
    }

    [Fact]
    public async Task HandleAsync_WithRevokedToken_ShouldReturnTokenRevokedError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "revogado@exemplo.com",
            Name = "Revogado",
            Role = UserRole.User
        };
        var revokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash_of_rt-revoked",
            DeviceId = "device-1",
            Status = RefreshTokenStatus.Revoked,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            ClientIp = "127.0.0.1",
            UserAgent = "App/1.0"
        };

        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(revokedToken);
        await dbContext.SaveChangesAsync();

        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("rt-revoked", "device-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOKEN_REVOKED");
        result.ErrorMessage.Should().Contain("revogada");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyRefreshToken_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("", "device-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("RefreshToken");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyDeviceId_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("token-123", "");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("DeviceId");
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var act = () => handler.HandleAsync(null!, clientContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RefreshTokenHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RefreshTokenRequest("token", "device");

        // Act
        var act = () => handler.HandleAsync(request, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
