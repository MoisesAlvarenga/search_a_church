using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Services;

public class MarcoCivilAuditLogger : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MarcoCivilAuditLogger> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MarcoCivilAuditLogger(
        AppDbContext dbContext,
        ILogger<MarcoCivilAuditLogger> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task LogEventAsync(
        string eventType,
        Guid? userId,
        ClientConnectionContext context,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(context);

        string serializedMetadata = "{}";
        if (metadata != null)
        {
            serializedMetadata = metadata is string jsonString
                ? jsonString
                : JsonSerializer.Serialize(metadata, JsonOptions);
        }

        var clientIp = string.IsNullOrWhiteSpace(context.ClientIp)
            ? "127.0.0.1"
            : context.ClientIp.Trim();

        var userAgent = string.IsNullOrWhiteSpace(context.UserAgent)
            ? "Unknown"
            : context.UserAgent.Trim();

        var auditLog = new AuditLog
        {
            UserId = userId,
            EventType = eventType.Trim().ToUpperInvariant(),
            ClientIp = clientIp,
            ClientPort = context.ClientPort,
            UserAgent = userAgent,
            TimestampUtc = _timeProvider.GetUtcNow(),
            Metadata = serializedMetadata
        };

        await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit event {EventType} recorded for User {UserId} from {ClientIp}:{ClientPort}",
            auditLog.EventType,
            userId,
            auditLog.ClientIp,
            auditLog.ClientPort);
    }
}
