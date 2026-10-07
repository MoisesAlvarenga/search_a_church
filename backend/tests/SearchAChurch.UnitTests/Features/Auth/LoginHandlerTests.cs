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
public class LoginHandlerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<ILogger<LoginHandler>> _loggerMock = new();
    private readonly PasswordHasher _passwordHasher = new();
    private readonly LoginRequestValidator _validator = new();

    public LoginHandlerTests()
    {
        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns("jwt.mock.access-token");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken())
            .Returns(("raw-refresh-token-login", "hashed-refresh-token-sha256-login"));
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ShouldAuthenticateAndGenerateNewFamilyIdAndTokens()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "carlos.souza@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("MinhaSenhaSegura123"),
            Name = "Carlos Souza",
            Role = UserRole.User,
            IsVerifiedRepresentative = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(
            Email: "carlos.souza@exemplo.com",
            Password: "MinhaSenhaSegura123",
            DeviceId: "android-pixel-7");

        var clientContext = new ClientConnectionContext("177.18.29.40", 50000, "AndroidApp/1.0.0");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt.mock.access-token");
        result.Value.RefreshToken.Should().Be("raw-refresh-token-login");
        result.Value.ExpiresIn.Should().Be(900);
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.User.Email.Should().Be("carlos.souza@exemplo.com");
        result.Value.User.Name.Should().Be("Carlos Souza");

        // Verify RefreshToken was stored with active status and new FamilyId
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
        storedToken.Should().NotBeNull();
        storedToken!.TokenHash.Should().Be("hashed-refresh-token-sha256-login");
        storedToken.DeviceId.Should().Be("android-pixel-7");
        storedToken.Status.Should().Be(RefreshTokenStatus.Active);
        storedToken.FamilyId.Should().NotBeEmpty();
        storedToken.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(59));
        storedToken.ClientIp.Should().Be("177.18.29.40");
        storedToken.UserAgent.Should().Be("AndroidApp/1.0.0");

        // Verify Audit Log recorded
        _auditLogMock.Verify(a => a.LogEventAsync(
            "LOGIN",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentEmail_ShouldReturnInvalidCredentialsGenericError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(
            Email: "inexistente@exemplo.com",
            Password: "QualquerSenha123",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        result.ErrorMessage.Should().Be("E-mail ou senha inválidos.");

        // Verify no token was created
        var tokensCount = await dbContext.RefreshTokens.CountAsync();
        tokensCount.Should().Be(0);

        // Verify failed audit log recorded
        _auditLogMock.Verify(a => a.LogEventAsync(
            "LOGIN_FAILED",
            null,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithIncorrectPassword_ShouldReturnInvalidCredentialsGenericError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "teste@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaCorreta123"),
            Name = "Usuario Teste",
            Role = UserRole.User
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(
            Email: "teste@exemplo.com",
            Password: "SenhaErradaTotal456",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        result.ErrorMessage.Should().Be("E-mail ou senha inválidos.");

        // Verify failed audit log recorded with user id
        _auditLogMock.Verify(a => a.LogEventAsync(
            "LOGIN_FAILED",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // No token created
        var tokensCount = await dbContext.RefreshTokens.CountAsync();
        tokensCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithSoftDeletedUser_ShouldReturnInvalidCredentials()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var deletedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "deletado@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaSegura123"),
            Name = "Deletado",
            Role = UserRole.User,
            DeletedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        dbContext.Users.Add(deletedUser);
        await dbContext.SaveChangesAsync();

        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(
            Email: "deletado@exemplo.com",
            Password: "SenhaSegura123",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task HandleAsync_ShouldNormalizeEmailCasingAndSpaces()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ana.paula@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaValida123"),
            Name = "Ana Paula",
            Role = UserRole.User
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(
            Email: "  Ana.Paula@EXEMPLO.com  ",
            Password: "SenhaValida123",
            DeviceId: "dev-trim");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Email.Should().Be("ana.paula@exemplo.com");
    }

    [Fact]
    public async Task HandleAsync_ShouldGenerateDistinctFamilyIdForSubsequentLogins()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "multi.login@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaSegura123"),
            Name = "Multi Login",
            Role = UserRole.User
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var req1 = new LoginRequest("multi.login@exemplo.com", "SenhaSegura123", "device-ios");
        var req2 = new LoginRequest("multi.login@exemplo.com", "SenhaSegura123", "device-android");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var res1 = await handler.HandleAsync(req1, clientContext);
        var res2 = await handler.HandleAsync(req2, clientContext);

        // Assert
        res1.IsSuccess.Should().BeTrue();
        res2.IsSuccess.Should().BeTrue();

        var tokens = await dbContext.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync();
        tokens.Should().HaveCount(2);
        tokens[0].FamilyId.Should().NotBe(tokens[1].FamilyId);
    }

    [Theory]
    [InlineData("email-invalido")]
    [InlineData("")]
    public async Task HandleAsync_WithInvalidEmail_ShouldReturnValidationFailure(string invalidEmail)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest(invalidEmail, "Senha123", "dev-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("Email");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyPassword_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest("user@exemplo.com", "", "dev-1");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("Password");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyDeviceId_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest("user@exemplo.com", "Senha123", "");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

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
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

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
        var handler = new LoginHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new LoginRequest("user@exemplo.com", "Pass", "dev");

        // Act
        var act = () => handler.HandleAsync(request, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
