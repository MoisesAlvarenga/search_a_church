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
public class RegisterHandlerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<ILogger<RegisterHandler>> _loggerMock = new();
    private readonly PasswordHasher _passwordHasher = new();
    private readonly RegisterRequestValidator _validator;

    public RegisterHandlerTests()
    {
        _validator = new RegisterRequestValidator(_passwordHasher);

        // Setup default TokenService mock behavior
        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns("jwt.mock.access-token");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken())
            .Returns(("raw-refresh-token-32bytes", "hashed-refresh-token-sha256"));
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldCreateUserAndTokensAndLogAudit()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "maria.silva@exemplo.com",
            Password: "SenhaSegura123",
            Name: "Maria Silva",
            DeviceId: "iphone-15-pro-uuid");

        var clientContext = new ClientConnectionContext("189.40.10.2", 443, "MobileSafari/604.1");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt.mock.access-token");
        result.Value.RefreshToken.Should().Be("raw-refresh-token-32bytes");
        result.Value.ExpiresIn.Should().Be(900);
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.User.Email.Should().Be("maria.silva@exemplo.com");
        result.Value.User.Name.Should().Be("Maria Silva");
        result.Value.User.Role.Should().Be("User");
        result.Value.User.IsVerifiedRepresentative.Should().BeFalse();

        // Verify User was saved in Database
        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "maria.silva@exemplo.com");
        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Maria Silva");
        _passwordHasher.VerifyPassword("SenhaSegura123", savedUser.PasswordHash).Should().BeTrue();

        // Verify RefreshToken was saved in Database
        var savedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == savedUser.Id);
        savedToken.Should().NotBeNull();
        savedToken!.TokenHash.Should().Be("hashed-refresh-token-sha256");
        savedToken.DeviceId.Should().Be("iphone-15-pro-uuid");
        savedToken.Status.Should().Be(RefreshTokenStatus.Active);
        savedToken.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(59));
        savedToken.ClientIp.Should().Be("189.40.10.2");
        savedToken.UserAgent.Should().Be("MobileSafari/604.1");

        // Verify Audit Log was recorded
        _auditLogMock.Verify(a => a.LogEventAsync(
            "REGISTER",
            savedUser.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ShouldReturnFailureWithoutLeakingData()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "usuario.existente@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("OutraSenha123"),
            Name = "Usuario Existente",
            Role = UserRole.User
        };
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "USUARIO.EXISTENTE@exemplo.com",
            Password: "NovaSenha456",
            Name: "Novo Tentativa",
            DeviceId: "device-xyz");

        var clientContext = new ClientConnectionContext("10.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
        result.ErrorMessage.Should().Contain("em uso");

        // Ensure no new user was created
        var usersCount = await dbContext.Users.CountAsync();
        usersCount.Should().Be(1);

        // Ensure no tokens were generated
        var tokensCount = await dbContext.RefreshTokens.CountAsync();
        tokensCount.Should().Be(0);

        _tokenServiceMock.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithSoftDeletedDuplicateEmail_ShouldReturnFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var deletedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "deletado@exemplo.com",
            PasswordHash = _passwordHasher.HashPassword("SenhaSegura123"),
            Name = "Usuario Deletado",
            Role = UserRole.User,
            DeletedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        dbContext.Users.Add(deletedUser);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "deletado@exemplo.com",
            Password: "NovaSenha123",
            Name: "Tentativa Conta",
            DeviceId: "device-123");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Browser");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
    }

    [Theory]
    [InlineData("email-invalido")]
    [InlineData("@dominio.com")]
    [InlineData("usuario@")]
    [InlineData("")]
    public async Task HandleAsync_WithInvalidEmail_ShouldReturnValidationFailure(string invalidEmail)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: invalidEmail,
            Password: "SenhaValida123",
            Name: "Teste",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("Email");
    }

    [Theory]
    [InlineData("curto")] // < 8 caracteres
    [InlineData("semnumeroaqui")] // sem número
    [InlineData("1234567890")] // sem letra
    [InlineData("")] // vazia
    public async Task HandleAsync_WithWeakPassword_ShouldReturnValidationFailure(string weakPassword)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "teste@exemplo.com",
            Password: weakPassword,
            Name: "Teste",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("Password");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyName_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "teste@exemplo.com",
            Password: "SenhaSegura123",
            Name: "",
            DeviceId: "dev-1");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("Name");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyDeviceId_ShouldReturnValidationFailure()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "teste@exemplo.com",
            Password: "SenhaSegura123",
            Name: "Nome Valido",
            DeviceId: "");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.ValidationErrors.Should().ContainKey("DeviceId");
    }

    [Fact]
    public async Task HandleAsync_ShouldNormalizeEmailAndTrimFields()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest(
            Email: "  Maria.Teste@Exemplo.COM  ",
            Password: "SenhaSegura123",
            Name: "  Maria Teste  ",
            DeviceId: "  device-trim  ");

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var result = await handler.HandleAsync(request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var savedUser = await dbContext.Users.FirstOrDefaultAsync();
        savedUser.Should().NotBeNull();
        savedUser!.Email.Should().Be("maria.teste@exemplo.com");
        savedUser.Name.Should().Be("Maria Teste");

        var savedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync();
        savedToken.Should().NotBeNull();
        savedToken!.DeviceId.Should().Be("device-trim");
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterHandler(
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
        var handler = new RegisterHandler(
            dbContext,
            _passwordHasher,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _validator,
            _loggerMock.Object);

        var request = new RegisterRequest("a@b.com", "Pass1234", "Name", "dev");

        // Act
        var act = () => handler.HandleAsync(request, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
