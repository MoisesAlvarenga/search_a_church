namespace SearchAChurch.Api.Data.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public RefreshTokenStatus Status { get; set; } = RefreshTokenStatus.Active;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConsumedAt { get; set; }
    public string ClientIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;

    // Navigation property
    public User User { get; set; } = null!;
}
