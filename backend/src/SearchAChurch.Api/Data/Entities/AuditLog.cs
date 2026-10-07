namespace SearchAChurch.Api.Data.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public int ClientPort { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Metadata { get; set; } = "{}";

    // Navigation property
    public User? User { get; set; }
}
