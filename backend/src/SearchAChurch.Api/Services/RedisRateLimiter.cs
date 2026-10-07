using System.Reflection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SearchAChurch.Api.Services;

public class RedisRateLimiter : IRateLimiterService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly Lazy<string> LuaScriptLazy = new(() =>
    {
        var assembly = typeof(RedisRateLimiter).Assembly;
        const string resourceName = "SearchAChurch.Api.Services.Scripts.sliding_window.lua";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        return FallbackLuaScript;
    });

    private const string FallbackLuaScript = @"
local key = KEYS[1]
local now = tonumber(ARGV[1])
local window = tonumber(ARGV[2])
local limit = tonumber(ARGV[3])
local memberId = ARGV[4]
local clearBefore = now - window

redis.call('ZREMRANGEBYSCORE', key, '-inf', clearBefore)
local currentRequests = redis.call('ZCARD', key)

if currentRequests < limit then
    redis.call('ZADD', key, now, memberId)
    redis.call('PEXPIRE', key, window)
    return { 1, limit - currentRequests - 1, 0 }
else
    local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
    local retryAfter = 0
    if #oldest > 0 then
        retryAfter = math.ceil((tonumber(oldest[2]) + window - now) / 1000)
        if retryAfter < 1 then retryAfter = 1 end
    end
    return { 0, 0, retryAfter }
end
";

    public RedisRateLimiter(
        ILogger<RedisRateLimiter> logger,
        IConnectionMultiplexer? redis = null,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _redis = redis;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RateLimitCheckResult> CheckRateLimitAsync(
        string routeKey,
        string identifier,
        int limit,
        TimeSpan window)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning(
                "Redis connection unavailable for rate limiter (key '{RouteKey}:{Identifier}'). Failing open.",
                routeKey,
                identifier);
            return new RateLimitCheckResult(IsAllowed: true, RemainingRequests: limit, RetryAfterSeconds: 0);
        }

        try
        {
            var db = _redis.GetDatabase();
            var redisKey = new RedisKey[] { $"rl:{routeKey}:{identifier}" };
            var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            var windowMs = (long)window.TotalMilliseconds;
            var memberId = Guid.NewGuid().ToString("N");

            var redisValues = new RedisValue[]
            {
                nowMs.ToString(),
                windowMs.ToString(),
                limit.ToString(),
                memberId
            };

            var executionResult = await db.ScriptEvaluateAsync(LuaScriptLazy.Value, redisKey, redisValues);

            var arrayResult = (RedisResult[]?)executionResult;
            if (arrayResult != null && arrayResult.Length >= 3)
            {
                var isAllowed = (long)arrayResult[0] == 1;
                var remaining = (long)arrayResult[1];
                var retryAfter = (int)(long)arrayResult[2];

                return new RateLimitCheckResult(isAllowed, remaining, retryAfter);
            }

            _logger.LogWarning(
                "Unexpected Redis Lua script evaluation result format for '{RouteKey}:{Identifier}'. Failing open.",
                routeKey,
                identifier);
            return new RateLimitCheckResult(IsAllowed: true, RemainingRequests: limit, RetryAfterSeconds: 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Redis rate limiter exception occurred for key '{RouteKey}:{Identifier}'. Failing open.",
                routeKey,
                identifier);
            return new RateLimitCheckResult(IsAllowed: true, RemainingRequests: limit, RetryAfterSeconds: 0);
        }
    }
}
