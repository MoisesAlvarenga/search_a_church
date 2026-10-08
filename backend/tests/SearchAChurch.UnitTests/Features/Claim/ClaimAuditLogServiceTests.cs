using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Features.Claim.Services;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class ClaimAuditLogServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordEventAsync_WithExplicitConnection_PersistsAppendOnlyLogWith180DaysRetention()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        var connection = new ClaimConnectionMetadata(
            ClientIp: "200.180.10.5",
            ClientPort: 44321,
            UserAgent: "Mozilla/5.0 (Android 14; Mobile; rv:128.0)",
            TimestampUtc: fixedTime
        );

        var metadata = new { TosVersion = "1.0", Art299Accepted = true, Method = "Geofence" };

        // Act
        var result = await service.RecordEventAsync(
            eventType: "ClaimInitiated",
            churchId: churchId,
            userId: userId,
            connection: connection,
            metadata: metadata
        );

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.EventType.Should().Be("ClaimInitiated");
        result.ChurchId.Should().Be(churchId);
        result.UserId.Should().Be(userId);
        result.ClientIp.Should().Be("200.180.10.5");
        result.ClientPort.Should().Be(44321);
        result.TimestampUtc.Should().Be(fixedTime);
        result.UserAgent.Should().Be("Mozilla/5.0 (Android 14; Mobile; rv:128.0)");
        result.RetentionUntil.Should().Be(fixedTime.AddDays(180));
        result.VerificationMetadata.Should().Contain("\"tosVersion\":\"1.0\"");
        result.VerificationMetadata.Should().Contain("\"art299Accepted\":true");

        // Verify persisted in DB
        var persisted = await context.ClaimAuditLogs.FirstOrDefaultAsync(l => l.Id == result.Id);
        persisted.Should().NotBeNull();
        persisted!.EventType.Should().Be("ClaimInitiated");
    }

    [Fact]
    public async Task RecordEventAsync_FromHttpContext_ExtractsXForwardedForAndPortCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "177.18.25.10, 10.0.0.1";
        httpContext.Request.Headers["X-Forwarded-Port"] = "44322";
        httpContext.Request.Headers.UserAgent = "SearchAChurch-App/2.0.0 (Android)";

        // Act
        var result = await service.RecordEventAsync(
            eventType: "EvidenceSubmitted",
            churchId: churchId,
            userId: null,
            httpContext: httpContext,
            metadata: new { EvidenceType = "PhotoGps", DistanceMeters = 42.0 }
        );

        // Assert
        result.ClientIp.Should().Be("177.18.25.10");
        result.ClientPort.Should().Be(44322);
        result.UserAgent.Should().Be("SearchAChurch-App/2.0.0 (Android)");
        result.VerificationMetadata.Should().Contain("\"evidenceType\":\"PhotoGps\"");
    }

    [Fact]
    public async Task RecordEventAsync_FromHttpContext_WithIpv6_ExtractsIpv6Correctly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchId = Guid.NewGuid();
        const string ipv6Address = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = ipv6Address;
        httpContext.Connection.RemotePort = 8443;

        // Act
        var result = await service.RecordEventAsync(
            eventType: "ClaimApproved",
            churchId: churchId,
            userId: Guid.NewGuid(),
            httpContext: httpContext
        );

        // Assert
        result.ClientIp.Should().Be(ipv6Address);
        result.ClientPort.Should().Be(8443);
    }

    [Fact]
    public async Task RecordEventAsync_FromHttpContext_FallbackToRemoteIpAddressAndPort()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("187.12.34.56");
        httpContext.Connection.RemotePort = 55123;

        // Act
        var result = await service.RecordEventAsync(
            eventType: "DisputeOpened",
            churchId: churchId,
            userId: Guid.NewGuid(),
            httpContext: httpContext
        );

        // Assert
        result.ClientIp.Should().Be("187.12.34.56");
        result.ClientPort.Should().Be(55123);
        result.UserAgent.Should().Be("Unknown");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordEventAsync_WhenEventTypeIsInvalid_ThrowsArgumentException(string invalidEventType)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var connection = new ClaimConnectionMetadata("127.0.0.1", 80, "Test", DateTimeOffset.UtcNow);

        // Act & Assert
        var act = () => service.RecordEventAsync(invalidEventType, Guid.NewGuid(), null, connection);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordEventAsync_WhenChurchIdIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var connection = new ClaimConnectionMetadata("127.0.0.1", 80, "Test", DateTimeOffset.UtcNow);

        // Act & Assert
        var act = () => service.RecordEventAsync("ClaimInitiated", Guid.Empty, null, connection);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ChurchId cannot be empty*");
    }

    [Fact]
    public async Task RecordEventAsync_WithStringMetadata_StoresRawStringDirectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        const string rawJson = "{\"customKey\":\"customValue\"}";
        var connection = new ClaimConnectionMetadata("127.0.0.1", 80, "Test", DateTimeOffset.UtcNow);

        // Act
        var result = await service.RecordEventAsync(
            "CustomEvent",
            Guid.NewGuid(),
            null,
            connection,
            metadata: rawJson
        );

        // Assert
        result.VerificationMetadata.Should().Be(rawJson);
    }

    [Fact]
    public async Task GetLogsByChurchIdAsync_ReturnsLogsOrderedByTimestampDescending()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchA = Guid.NewGuid();
        var churchB = Guid.NewGuid();
        var t1 = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 10, 8, 11, 0, 0, TimeSpan.Zero);
        var t3 = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        await service.RecordEventAsync("Event1", churchA, null, new ClaimConnectionMetadata("1.1.1.1", 80, "A", t1));
        await service.RecordEventAsync("Event2", churchA, null, new ClaimConnectionMetadata("1.1.1.1", 80, "A", t3));
        await service.RecordEventAsync("Event3", churchA, null, new ClaimConnectionMetadata("1.1.1.1", 80, "A", t2));
        await service.RecordEventAsync("EventOtherChurch", churchB, null, new ClaimConnectionMetadata("2.2.2.2", 80, "B", t2));

        // Act
        var logs = await service.GetLogsByChurchIdAsync(churchA);

        // Assert
        logs.Should().HaveCount(3);
        logs[0].EventType.Should().Be("Event2"); // t3 is newest
        logs[1].EventType.Should().Be("Event3"); // t2
        logs[2].EventType.Should().Be("Event1"); // t1
    }

    [Fact]
    public async Task RecordEventAsync_GuaranteesAppendOnly_MultipleEventsPersistedSeparately()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new ClaimAuditLogService(context, NullLogger<ClaimAuditLogService>.Instance);

        var churchId = Guid.NewGuid();
        var connection = new ClaimConnectionMetadata("127.0.0.1", 443, "TestApp", DateTimeOffset.UtcNow);

        // Act
        var log1 = await service.RecordEventAsync("ClaimInitiated", churchId, null, connection);
        var log2 = await service.RecordEventAsync("EvidenceSubmitted", churchId, null, connection);
        var log3 = await service.RecordEventAsync("ClaimApproved", churchId, null, connection);

        // Assert
        var allLogs = await context.ClaimAuditLogs.Where(l => l.ChurchId == churchId).ToListAsync();
        allLogs.Should().HaveCount(3);
        allLogs.Select(l => l.Id).Should().OnlyHaveUniqueItems();
        allLogs.Select(l => l.EventType).Should().ContainInOrder("ClaimInitiated", "EvidenceSubmitted", "ClaimApproved");
    }
}
