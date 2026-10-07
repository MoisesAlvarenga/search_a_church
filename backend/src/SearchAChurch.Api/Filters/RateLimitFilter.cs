using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Filters;

public enum RateLimitKeyType
{
    ClientIp,
    IpAndEmail,
    UserAndDevice
}

public record RateLimitPolicy(
    string RouteKey,
    RateLimitKeyType KeyType,
    int Limit,
    TimeSpan Window
);

public record RateLimitExceededResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("retryAfterSeconds")] int RetryAfterSeconds
);

public static class RateLimitPolicies
{
    public static readonly RateLimitPolicy Register = new("register", RateLimitKeyType.ClientIp, 3, TimeSpan.FromHours(1));
    public static readonly RateLimitPolicy Login = new("login", RateLimitKeyType.IpAndEmail, 5, TimeSpan.FromMinutes(15));
    public static readonly RateLimitPolicy LoginGlobalIp = new("login-global", RateLimitKeyType.ClientIp, 50, TimeSpan.FromHours(1));
    public static readonly RateLimitPolicy Refresh = new("refresh", RateLimitKeyType.UserAndDevice, 20, TimeSpan.FromMinutes(1));
    public static readonly RateLimitPolicy ForgotPassword = new("forgot-password", RateLimitKeyType.IpAndEmail, 3, TimeSpan.FromHours(1));
    public static readonly RateLimitPolicy ResetPassword = new("reset-password", RateLimitKeyType.IpAndEmail, 3, TimeSpan.FromMinutes(15));

    public static readonly IReadOnlyDictionary<string, RateLimitPolicy> Defaults = new Dictionary<string, RateLimitPolicy>(StringComparer.OrdinalIgnoreCase)
    {
        ["register"] = Register,
        ["login"] = Login,
        ["login-global"] = LoginGlobalIp,
        ["refresh"] = Refresh,
        ["forgot-password"] = ForgotPassword,
        ["reset-password"] = ResetPassword
    };

    public static RateLimitPolicy GetPolicy(string routeKey)
    {
        if (Defaults.TryGetValue(routeKey, out var policy))
        {
            return policy;
        }

        return new RateLimitPolicy(routeKey, RateLimitKeyType.ClientIp, 100, TimeSpan.FromMinutes(1));
    }
}

public class RateLimitFilter : IEndpointFilter
{
    private readonly IRateLimiterService _rateLimiter;
    private readonly RateLimitPolicy? _policy;
    private readonly string? _routeKey;
    private readonly ITokenService? _tokenService;
    private readonly ILogger<RateLimitFilter>? _logger;

    public RateLimitFilter(
        IRateLimiterService rateLimiter,
        RateLimitPolicy policy,
        ITokenService? tokenService = null,
        ILogger<RateLimitFilter>? logger = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _routeKey = policy.RouteKey;
        _tokenService = tokenService;
        _logger = logger;
    }

    public RateLimitFilter(
        IRateLimiterService rateLimiter,
        string routeKey,
        ITokenService? tokenService = null,
        ILogger<RateLimitFilter>? logger = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _routeKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
        _policy = RateLimitPolicies.GetPolicy(routeKey);
        _tokenService = tokenService;
        _logger = logger;
    }

    public RateLimitFilter(
        IRateLimiterService rateLimiter,
        ITokenService? tokenService = null,
        ILogger<RateLimitFilter>? logger = null)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _policy = null;
        _routeKey = null;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var policy = ResolvePolicy(context);
        var identifier = ExtractIdentifier(context, policy.KeyType, _tokenService);

        var checkResult = await _rateLimiter.CheckRateLimitAsync(
            policy.RouteKey,
            identifier,
            policy.Limit,
            policy.Window);

        var httpContext = context.HttpContext;

        if (checkResult.IsAllowed)
        {
            httpContext.Response.Headers["RateLimit-Limit"] = policy.Limit.ToString();
            httpContext.Response.Headers["RateLimit-Remaining"] = checkResult.RemainingRequests.ToString();
            
            var resetSeconds = checkResult.RetryAfterSeconds > 0
                ? checkResult.RetryAfterSeconds
                : (int)policy.Window.TotalSeconds;
            httpContext.Response.Headers["RateLimit-Reset"] = resetSeconds.ToString();

            return await next(context);
        }

        var retryAfter = Math.Max(1, checkResult.RetryAfterSeconds);
        httpContext.Response.Headers["RateLimit-Limit"] = policy.Limit.ToString();
        httpContext.Response.Headers["RateLimit-Remaining"] = "0";
        httpContext.Response.Headers["RateLimit-Reset"] = retryAfter.ToString();
        httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();

        _logger?.LogWarning(
            "Rate limit exceeded for route '{RouteKey}' and identifier '{Identifier}'. Retry-After: {RetryAfter}s",
            policy.RouteKey,
            identifier,
            retryAfter);

        var errorResponse = new RateLimitExceededResponse(
            Error: "RATE_LIMIT_EXCEEDED",
            Message: "Limite de tentativas excedido. Por favor, aguarde antes de tentar novamente.",
            RetryAfterSeconds: retryAfter
        );

        return Results.Json(
            errorResponse,
            statusCode: StatusCodes.Status429TooManyRequests,
            contentType: "application/json"
        );
    }

    private RateLimitPolicy ResolvePolicy(EndpointFilterInvocationContext context)
    {
        if (_policy != null)
        {
            return _policy;
        }

        if (!string.IsNullOrWhiteSpace(_routeKey))
        {
            return RateLimitPolicies.GetPolicy(_routeKey);
        }

        var endpointMetadata = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<RateLimitPolicy>();
        if (endpointMetadata != null)
        {
            return endpointMetadata;
        }

        return RateLimitPolicies.GetPolicy("default");
    }

    public static string ExtractIdentifier(
        EndpointFilterInvocationContext context,
        RateLimitKeyType keyType,
        ITokenService? tokenService = null)
    {
        var clientContext = ClientConnectionContext.FromHttpContext(context.HttpContext);
        var clientIp = string.IsNullOrWhiteSpace(clientContext.ClientIp) ? "127.0.0.1" : clientContext.ClientIp;

        switch (keyType)
        {
            case RateLimitKeyType.ClientIp:
                return clientIp;

            case RateLimitKeyType.IpAndEmail:
            {
                var email = TryExtractEmailFromArguments(context.Arguments);
                if (!string.IsNullOrWhiteSpace(email))
                {
                    return $"ip:{clientIp}:email:{email.Trim().ToLowerInvariant()}";
                }
                return $"ip:{clientIp}";
            }

            case RateLimitKeyType.UserAndDevice:
            {
                var deviceId = TryExtractDeviceId(context);
                var userId = TryExtractUserId(context, tokenService);

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return $"user:{userId}:device:{deviceId}";
                }

                return $"device:{deviceId}";
            }

            default:
                return clientIp;
        }
    }

    private static string? TryExtractEmailFromArguments(IList<object?> arguments)
    {
        foreach (var arg in arguments)
        {
            if (arg == null) continue;

            var prop = arg.GetType().GetProperty("Email", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(arg)?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
        }

        return null;
    }

    private static string TryExtractDeviceId(EndpointFilterInvocationContext context)
    {
        if (context.HttpContext.Request.Headers.TryGetValue("X-Device-Id", out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal.ToString()))
        {
            return headerVal.ToString().Trim();
        }

        foreach (var arg in context.Arguments)
        {
            if (arg == null) continue;

            var prop = arg.GetType().GetProperty("DeviceId", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(arg)?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val.Trim();
                }
            }
        }

        return "unknown";
    }

    private static string? TryExtractUserId(
        EndpointFilterInvocationContext context,
        ITokenService? tokenService)
    {
        var user = context.HttpContext.User;
        if (user != null && user.Identity?.IsAuthenticated == true)
        {
            var claimVal = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("sub")?.Value;
            if (!string.IsNullOrWhiteSpace(claimVal))
            {
                return claimVal;
            }
        }

        if (tokenService != null &&
            context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerStr = authHeader.ToString();
            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var rawToken = headerStr["Bearer ".Length..].Trim();
                try
                {
                    var principal = tokenService.GetPrincipalFromExpiredToken(rawToken);
                    var claimVal = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                   ?? principal?.FindFirst("sub")?.Value;
                    if (!string.IsNullOrWhiteSpace(claimVal))
                    {
                        return claimVal;
                    }
                }
                catch
                {
                    // Ignore parsing failure and fallback
                }
            }
        }

        return null;
    }
}
