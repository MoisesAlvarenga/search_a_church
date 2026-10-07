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
public class PasswordResetHandlerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<ILogger<PasswordResetHandler>> _loggerMock = new();
    private readonly PasswordHasher _passwordHasher = new();
    private readonly ForgotPasswordRequestValidator _forgotValidator = new();
    private readonly ResetPasswordRequestValidator _resetValidator;

    public PasswordResetHandlerTests()
    {
        _resetValidator = new ResetPasswordRequestValidator(_passwordHasher);

        _tokenServiceMock.Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns<string>(raw => $"hash_of_{raw}");
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
    public async Task RequestOtpAsync_WithRegisteredUser_ShouldGenerate6DigitOtpAndExpirePreviousOtps()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "lucas.melo@exemplo.com",
            Name = "Lucas Melo",
            Role = UserRole.User
        };
        var oldOtp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = "old_hash",
            AttemptCount = 0,
            IsConsumed = false,
            ExpiresAt = fixedNow.AddMinutes(10),
            CreatedAt = fixedNow.AddMinutes(-5)
        };
        dbContext.Users.Add(user);
        dbContext.PasswordResetOtps.Add(oldOtp);
        await dbContext.SaveChangesAsync();

        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object,
            timeProvider);

        var request = new ForgotPasswordRequest("LUCAS.MELO@exemplo.com");
        var clientContext = new ClientConnectionContext("189.1.2.3", 443, "Mobile/1.0");

        // Act
        var result = await handler.RequestOtpAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Old OTP consumed
        var reloadedOldOtp = await dbContext.PasswordResetOtps.FindAsync(oldOtp.Id);
        reloadedOldOtp!.IsConsumed.Should().BeTrue();

        // New OTP created
        var activeOtps = await dbContext.PasswordResetOtps
            .Where(o => o.UserId == user.Id && !o.IsConsumed)
            .ToListAsync();
        activeOtps.Should().ContainSingle();

        var newOtp = activeOtps[0];
        newOtp.OtpHash.Should().StartWith("hash_of_");
        newOtp.ExpiresAt.Should().Be(fixedNow.AddMinutes(15));
        newOtp.AttemptCount.Should().Be(0);

        _auditLogMock.Verify(a => a.LogEventAsync(
            "PASSWORD_RESET_OTP_REQUESTED",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_WithUnregisteredUser_ShouldReturnSuccessWithoutLeakingInformation()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object);

        var request = new ForgotPasswordRequest("inexistente@exemplo.com");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.RequestOtpAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var otpsCount = await dbContext.PasswordResetOtps.CountAsync();
        otpsCount.Should().Be(0);

        _auditLogMock.Verify(a => a.LogEventAsync(
            "FORGOT_PASSWORD_REQUEST",
            null,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidOtpAndStrongPassword_ShouldUpdatePasswordAndRevokeAllUserSessions()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "marcos@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaAntiga123"),
            Name = "Marcos",
            Role = UserRole.User
        };

        var otp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = "hash_of_654321",
            AttemptCount = 0,
            IsConsumed = false,
            ExpiresAt = fixedNow.AddMinutes(15),
            CreatedAt = fixedNow
        };

        var session1 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "session1_hash",
            DeviceId = "device-iphone",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = fixedNow.AddDays(30)
        };

        var session2 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "session2_hash",
            DeviceId = "device-ipad",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = fixedNow.AddDays(40)
        };

        dbContext.Users.Add(user);
        dbContext.PasswordResetOtps.Add(otp);
        dbContext.RefreshTokens.AddRange(session1, session2);
        await dbContext.SaveChangesAsync();

        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object,
            timeProvider);

        var request = new ResetPasswordRequest("marcos@exemplo.com", "654321", "NovaSenhaSuperForte789");
        var clientContext = new ClientConnectionContext("189.1.2.3", 443, "Mobile/1.0");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Password updated
        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        _passwordHasher.VerifyPassword("NovaSenhaSuperForte789", updatedUser!.PasswordHash).Should().BeTrue();

        // OTP consumed
        var reloadedOtp = await dbContext.PasswordResetOtps.FindAsync(otp.Id);
        reloadedOtp!.IsConsumed.Should().BeTrue();

        // ALL sessions revoked
        var activeTokensCount = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.Status == RefreshTokenStatus.Active)
            .CountAsync();
        activeTokensCount.Should().Be(0);

        var revokedTokens = await dbContext.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync();
        revokedTokens.Should().HaveCount(2);
        revokedTokens.Should().OnlyContain(t => t.Status == RefreshTokenStatus.Revoked);

        _auditLogMock.Verify(a => a.LogEventAsync(
            "PASSWORD_RESET_SUCCESS",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredOtp_ShouldReturnOtpExpiredError()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "expirado@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaAntiga123"),
            Name = "Expirado",
            Role = UserRole.User
        };

        var expiredOtp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = "hash_of_112233",
            AttemptCount = 0,
            IsConsumed = false,
            ExpiresAt = fixedNow.AddMinutes(-1), // Expirado 1 min atrás
            CreatedAt = fixedNow.AddMinutes(-16)
        };

        dbContext.Users.Add(user);
        dbContext.PasswordResetOtps.Add(expiredOtp);
        await dbContext.SaveChangesAsync();

        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object,
            timeProvider);

        var request = new ResetPasswordRequest("expirado@exemplo.com", "112233", "NovaSenha123");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_EXPIRED");

        var reloadedOtp = await dbContext.PasswordResetOtps.FindAsync(expiredOtp.Id);
        reloadedOtp!.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithIncorrectCode_ShouldIncrementAttemptCountAndReturnError()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "tentativas@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaAntiga123"),
            Name = "Tentativas",
            Role = UserRole.User
        };

        var otp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = "hash_of_999999",
            AttemptCount = 0,
            IsConsumed = false,
            ExpiresAt = fixedNow.AddMinutes(15),
            CreatedAt = fixedNow
        };

        dbContext.Users.Add(user);
        dbContext.PasswordResetOtps.Add(otp);
        await dbContext.SaveChangesAsync();

        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object,
            timeProvider);

        var request = new ResetPasswordRequest("tentativas@exemplo.com", "000000", "NovaSenha123");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OTP");
        result.ErrorMessage.Should().Contain("Tentativa 1 de 3");

        var reloadedOtp = await dbContext.PasswordResetOtps.FindAsync(otp.Id);
        reloadedOtp!.AttemptCount.Should().Be(1);
        reloadedOtp.IsConsumed.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenReachingThirdFailedAttempt_ShouldBlockAndConsumeOtp()
    {
        // Arrange
        var fixedNow = DateTimeOffset.UtcNow;
        var timeProvider = new TestTimeProvider(fixedNow);
        using var dbContext = CreateInMemoryDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "bloqueio@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaAntiga123"),
            Name = "Bloqueio",
            Role = UserRole.User
        };

        var otp = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = "hash_of_888888",
            AttemptCount = 2, // Já errou 2 vezes
            IsConsumed = false,
            ExpiresAt = fixedNow.AddMinutes(15),
            CreatedAt = fixedNow
        };

        dbContext.Users.Add(user);
        dbContext.PasswordResetOtps.Add(otp);
        await dbContext.SaveChangesAsync();

        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object,
            timeProvider);

        var request = new ResetPasswordRequest("bloqueio@exemplo.com", "123123", "NovaSenha123");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MAX_ATTEMPTS_EXCEEDED");

        var reloadedOtp = await dbContext.PasswordResetOtps.FindAsync(otp.Id);
        reloadedOtp!.AttemptCount.Should().Be(3);
        reloadedOtp.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithWeakPassword_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object);

        var request = new ResetPasswordRequest("user@exemplo.com", "123456", "fraca");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("NewPassword");
    }

    [Theory]
    [InlineData("12345")] // 5 dígitos
    [InlineData("1234567")] // 7 dígitos
    [InlineData("abcdef")] // letras
    [InlineData("")]
    public async Task ResetPasswordAsync_WithInvalidOtpFormat_ShouldReturnValidationFailure(string invalidOtp)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new PasswordResetHandler(
            dbContext,
            _tokenServiceMock.Object,
            _passwordHasher,
            _auditLogMock.Object,
            _forgotValidator,
            _resetValidator,
            _loggerMock.Object);

        var request = new ResetPasswordRequest("user@exemplo.com", invalidOtp, "SenhaForte123");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.ResetPasswordAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("OtpCode");
    }
}
