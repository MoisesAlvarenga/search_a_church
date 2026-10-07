using Microsoft.AspNetCore.Http;

namespace SearchAChurch.Api.Services;

public record ClientConnectionContext(
    string ClientIp,
    int ClientPort,
    string? UserAgent
)
{
    public static ClientConnectionContext FromHttpContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string clientIp = "127.0.0.1";
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor.ToString()))
        {
            var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                clientIp = firstIp;
            }
        }
        else if (httpContext.Connection.RemoteIpAddress != null)
        {
            clientIp = httpContext.Connection.RemoteIpAddress.ToString();
        }

        int clientPort = httpContext.Connection.RemotePort;
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-Port", out var forwardedPort) &&
            int.TryParse(forwardedPort.ToString(), out var parsedPort))
        {
            clientPort = parsedPort;
        }

        string? userAgent = null;
        if (httpContext.Request.Headers.TryGetValue("User-Agent", out var ua) &&
            !string.IsNullOrWhiteSpace(ua.ToString()))
        {
            userAgent = ua.ToString();
        }

        return new ClientConnectionContext(clientIp, clientPort, userAgent);
    }
}

public interface IAuditLogService
{
    Task LogEventAsync(
        string eventType,
        Guid? userId,
        ClientConnectionContext context,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
