namespace SearchAChurch.Api.Services;

public record RateLimitCheckResult(
    bool IsAllowed,
    long RemainingRequests,
    int RetryAfterSeconds
);

public interface IRateLimiterService
{
    Task<RateLimitCheckResult> CheckRateLimitAsync(string routeKey, string identifier, int limit, TimeSpan window);
}
