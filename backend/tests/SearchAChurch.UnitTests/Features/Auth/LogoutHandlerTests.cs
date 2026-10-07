using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.UnitTests.Features.Auth;

[Trait("Category", "Unit")]
public class LogoutHandlerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<ILogger<LogoutHandler>> _loggerMock = new();

    public LogoutHandlerTests()
    {
        _tokenServiceMock.Setup(t => t.HashToken(It.IsAny<string>()))
            .Returns<string>(raw => $"hash_of_{raw}");
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_WithSpecificRefreshToken_ShouldMarkTokenAsRevokedAndRecordAuditLog()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "logout.user@exemplo.com",
            Name = "Logout User",
            Role = UserRole.User
        };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash_of_rt-to-logout",
            DeviceId = "device-ios",
            Status = RefreshTokenStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60)
        };
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var handler = new LogoutHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object);

        var request = new LogoutRequest("rt-to-logout");
        var clientContext = new ClientConnectionContext("189.1.2.3", 443, "Mobile/1.0");

        // Act
        var result = await handler.HandleAsync(user.Id, request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedToken = await dbContext.RefreshTokens.FindAsync(token.Id);
        updatedToken!.Status.Should().Be(RefreshTokenStatus.Revoked);

        _auditLogMock.Verify(a => a.LogEventAsync(
            "LOGOUT",
            user.Id,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyRefreshToken_ShouldSucceedIdempotentlyAndRecordAuditLog()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        var handler = new LogoutHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object);

        var request = new LogoutRequest(null);
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(userId, request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _auditLogMock.Verify(a => a.LogEventAsync(
            "LOGOUT",
            userId,
            clientContext,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenAlreadyRevoked_ShouldSucceedIdempotently()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "already.revoked@exemplo.com",
            Name = "Revoked User",
            Role = UserRole.User
        };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash_of_rt-already-revoked",
            DeviceId = "dev-1",
            Status = RefreshTokenStatus.Revoked,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60)
        };
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var handler = new LogoutHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object);

        var request = new LogoutRequest("rt-already-revoked");
        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var result = await handler.HandleAsync(user.Id, request, clientContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LogoutHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object);

        var clientContext = new ClientConnectionContext("127.0.0.1", 80, "App");

        // Act
        var act = () => handler.HandleAsync(Guid.NewGuid(), null!, clientContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var handler = new LogoutHandler(
            dbContext,
            _tokenServiceMock.Object,
            _auditLogMock.Object,
            _loggerMock.Object);

        var request = new LogoutRequest("token");

        // Act
        var act = () => handler.HandleAsync(Guid.NewGuid(), request, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
