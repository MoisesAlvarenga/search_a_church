using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Services;
using StackExchange.Redis;

namespace SearchAChurch.UnitTests.Services;

[Trait("Category", "Unit")]
public class RateLimiterServiceTests
{
    private readonly Mock<ILogger<RedisRateLimiter>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();

    public RateLimiterServiceTests()
    {
        _redisMock.Setup(r => r.IsConnected).Returns(true);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenRedisNull_ShouldFailOpen()
    {
        // Arrange
        var service = new RedisRateLimiter(_loggerMock.Object, redis: null);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(5);
        result.RetryAfterSeconds.Should().Be(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenRedisDisconnected_ShouldFailOpen()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var service = new RedisRateLimiter(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(5);
        result.RetryAfterSeconds.Should().Be(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenUnderLimit_ShouldAllowAndReturnRemaining()
    {
        // Arrange
        // Lua returns { 1, 4, 0 } => allowed, 4 remaining, 0 retryAfter
        var results = new RedisResult[]
        {
            RedisResult.Create(1L),
            RedisResult.Create(4L),
            RedisResult.Create(0L)
        };
        var redisArrayResult = RedisResult.Create(results);

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisArrayResult);

        var service = new RedisRateLimiter(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(4);
        result.RetryAfterSeconds.Should().Be(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenLimitExceeded_ShouldBlockAndReturnRetryAfter()
    {
        // Arrange
        // Lua returns { 0, 0, 42 } => blocked, 0 remaining, 42 retryAfter
        var results = new RedisResult[]
        {
            RedisResult.Create(0L),
            RedisResult.Create(0L),
            RedisResult.Create(42L)
        };
        var redisArrayResult = RedisResult.Create(results);

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisArrayResult);

        var service = new RedisRateLimiter(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.RemainingRequests.Should().Be(0);
        result.RetryAfterSeconds.Should().Be(42);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenResultFormatInvalid_ShouldFailOpenDefensively()
    {
        // Arrange
        var redisSingleResult = RedisResult.Create((RedisValue)"unexpected-string");

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisSingleResult);

        var service = new RedisRateLimiter(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(5);
        result.RetryAfterSeconds.Should().Be(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenRedisThrowsException_ShouldFailOpenDefensively()
    {
        // Arrange
        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
#pragma warning disable CS0618
            .ThrowsAsync(new RedisTimeoutException("Redis server timed out", CommandStatus.Unknown));
#pragma warning restore CS0618

        var service = new RedisRateLimiter(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.CheckRateLimitAsync("login", "192.168.1.1", limit: 5, window: TimeSpan.FromMinutes(15));

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(5);
        result.RetryAfterSeconds.Should().Be(0);
    }
}
