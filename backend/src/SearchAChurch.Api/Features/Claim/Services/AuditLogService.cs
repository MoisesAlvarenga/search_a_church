using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Metadados de conexão do cliente para conformidade com o Marco Civil da Internet (art. 15 da Lei nº 12.965/2014).
/// </summary>
public record ClaimConnectionMetadata(
    string ClientIp,
    int ClientPort,
    string UserAgent,
    DateTimeOffset TimestampUtc
)
{
    /// <summary>
    /// Extrai com segurança IP (IPv4 ou IPv6), porta lógica e User-Agent do contexto HTTP.
    /// </summary>
    public static ClaimConnectionMetadata FromHttpContext(HttpContext httpContext, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var time = timeProvider ?? TimeProvider.System;
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
            int.TryParse(forwardedPort.ToString(), out var parsedPort) && parsedPort > 0)
        {
            clientPort = parsedPort;
        }

        string userAgent = "Unknown";
        if (!string.IsNullOrWhiteSpace(httpContext.Request.Headers.UserAgent))
        {
            userAgent = httpContext.Request.Headers.UserAgent.ToString().Trim();
        }

        return new ClaimConnectionMetadata(
            ClientIp: clientIp,
            ClientPort: clientPort,
            UserAgent: userAgent,
            TimestampUtc: time.GetUtcNow()
        );
    }
}

/// <summary>
/// Contrato do serviço de auditoria append-only para o ciclo de vida de reivindicação e contestações (AD-014).
/// </summary>
public interface IClaimAuditLogService
{
    /// <summary>
    /// Registra de forma imutável (append-only) um evento de reivindicação ou disputa com retenção de 180 dias.
    /// </summary>
    Task<ClaimAuditLog> RecordEventAsync(
        string eventType,
        Guid churchId,
        Guid? userId,
        ClaimConnectionMetadata connection,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sobrecarga que extrai automaticamente os metadados de rede a partir do HttpContext.
    /// </summary>
    Task<ClaimAuditLog> RecordEventAsync(
        string eventType,
        Guid churchId,
        Guid? userId,
        HttpContext httpContext,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta registros de auditoria de uma congregação em modo somente leitura.
    /// </summary>
    Task<IReadOnlyList<ClaimAuditLog>> GetLogsByChurchIdAsync(
        Guid churchId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação do serviço de auditoria append-only em estrita conformidade com o Marco Civil e LGPD.
/// </summary>
public class ClaimAuditLogService : IClaimAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ClaimAuditLogService> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ClaimAuditLogService(
        AppDbContext dbContext,
        ILogger<ClaimAuditLogService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ClaimAuditLog> RecordEventAsync(
        string eventType,
        Guid churchId,
        Guid? userId,
        ClaimConnectionMetadata connection,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(connection);

        if (churchId == Guid.Empty)
        {
            throw new ArgumentException("ChurchId cannot be empty", nameof(churchId));
        }

        string serializedMetadata = "{}";
        if (metadata != null)
        {
            serializedMetadata = metadata is string jsonString
                ? jsonString
                : JsonSerializer.Serialize(metadata, JsonOptions);
        }

        var clientIp = string.IsNullOrWhiteSpace(connection.ClientIp)
            ? "127.0.0.1"
            : connection.ClientIp.Trim();

        var userAgent = string.IsNullOrWhiteSpace(connection.UserAgent)
            ? "Unknown"
            : connection.UserAgent.Trim();

        var timestamp = connection.TimestampUtc != default
            ? connection.TimestampUtc
            : _timeProvider.GetUtcNow();

        // Marco Civil da Internet (art. 15): retenção mínima de 180 dias (6 meses)
        var retentionUntil = timestamp.AddDays(180);

        var auditLog = new ClaimAuditLog
        {
            Id = Guid.NewGuid(),
            EventType = eventType.Trim(),
            ChurchId = churchId,
            UserId = userId,
            ClientIp = clientIp,
            ClientPort = connection.ClientPort,
            TimestampUtc = timestamp,
            UserAgent = userAgent,
            VerificationMetadata = serializedMetadata,
            RetentionUntil = retentionUntil
        };

        // Garantia Append-Only: apenas inserção permitida
        await _dbContext.ClaimAuditLogs.AddAsync(auditLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Claim audit event '{EventType}' recorded for Church {ChurchId} from {ClientIp}:{ClientPort} (Retention until {RetentionUntil:u})",
            auditLog.EventType,
            auditLog.ChurchId,
            auditLog.ClientIp,
            auditLog.ClientPort,
            auditLog.RetentionUntil);

        return auditLog;
    }

    public Task<ClaimAuditLog> RecordEventAsync(
        string eventType,
        Guid churchId,
        Guid? userId,
        HttpContext httpContext,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext, _timeProvider);
        return RecordEventAsync(eventType, churchId, userId, connection, metadata, cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimAuditLog>> GetLogsByChurchIdAsync(
        Guid churchId,
        CancellationToken cancellationToken = default)
    {
        if (churchId == Guid.Empty)
        {
            throw new ArgumentException("ChurchId cannot be empty", nameof(churchId));
        }

        return await _dbContext.ClaimAuditLogs
            .AsNoTracking()
            .Where(l => l.ChurchId == churchId)
            .OrderByDescending(l => l.TimestampUtc)
            .ToListAsync(cancellationToken);
    }
}
