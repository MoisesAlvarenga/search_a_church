using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Services;

namespace SearchAChurch.UnitTests.Services;

[Trait("Category", "Unit")]
public class MarcoCivilAuditLoggerTests
{
    private readonly Mock<ILogger<MarcoCivilAuditLogger>> _loggerMock = new();

    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task LogEventAsync_WithValidData_ShouldPersistAuditLogEntryStrictlyAppendOnly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new MarcoCivilAuditLogger(dbContext, _loggerMock.Object);

        var userId = Guid.NewGuid();
        var context = new ClientConnectionContext("189.40.55.12", 54321, "FlutterApp/1.0.0");
        var metadata = new { reason = "successful_login", device = "Pixel 7" };

        // Act
        await service.LogEventAsync("LOGIN", userId, context, metadata);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.UserId.Should().Be(userId);
        savedLog.EventType.Should().Be("LOGIN");
        savedLog.ClientIp.Should().Be("189.40.55.12");
        savedLog.ClientPort.Should().Be(54321);
        savedLog.UserAgent.Should().Be("FlutterApp/1.0.0");
        savedLog.Metadata.Should().Contain("successful_login");
        savedLog.TimestampUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogEventAsync_WithNullMetadata_ShouldPersistEmptyJsonObject()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new MarcoCivilAuditLogger(dbContext, _loggerMock.Object);

        var context = new ClientConnectionContext("10.0.0.1", 8080, "Browser");

        // Act
        await service.LogEventAsync("REGISTER", null, context, metadata: null);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.UserId.Should().BeNull();
        savedLog.EventType.Should().Be("REGISTER");
        savedLog.Metadata.Should().Be("{}");
    }

    [Fact]
    public async Task LogEventAsync_WithStringMetadata_ShouldPersistDirectString()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new MarcoCivilAuditLogger(dbContext, _loggerMock.Object);

        var context = new ClientConnectionContext("10.0.0.1", 8080, "Browser");
        var rawJson = "{\"action\":\"raw_event\"}";

        // Act
        await service.LogEventAsync("LOGOUT", null, context, rawJson);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync();
        savedLog.Should().NotBeNull();
        savedLog!.Metadata.Should().Be(rawJson);
    }

    [Fact]
    public async Task LogEventAsync_WhenEventTypeEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new MarcoCivilAuditLogger(dbContext, _loggerMock.Object);
        var context = new ClientConnectionContext("127.0.0.1", 80, "Test");

        // Act
        var act = () => service.LogEventAsync("", null, context);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LogEventAsync_WhenContextNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new MarcoCivilAuditLogger(dbContext, _loggerMock.Object);

        // Act
        var act = () => service.LogEventAsync("EVENT", null, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void FromHttpContext_WithXForwardedHeaders_ShouldExtractRealClientIpAndPort()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 70.41.3.18, 150.172.238.178";
        httpContext.Request.Headers["X-Forwarded-Port"] = "49152";
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 Chrome/120.0";

        // Act
        var context = ClientConnectionContext.FromHttpContext(httpContext);

        // Assert
        context.ClientIp.Should().Be("203.0.113.195");
        context.ClientPort.Should().Be(49152);
        context.UserAgent.Should().Be("Mozilla/5.0 Chrome/120.0");
    }

    [Fact]
    public void FromHttpContext_WithoutForwardedHeaders_ShouldFallbackToRemoteConnection()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
        httpContext.Connection.RemotePort = 55123;
        httpContext.Request.Headers["User-Agent"] = "TestRunner/2.0";

        // Act
        var context = ClientConnectionContext.FromHttpContext(httpContext);

        // Assert
        context.ClientIp.Should().Be("192.168.1.100");
        context.ClientPort.Should().Be(55123);
        context.UserAgent.Should().Be("TestRunner/2.0");
    }

    [Fact]
    public void FromHttpContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => ClientConnectionContext.FromHttpContext(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
