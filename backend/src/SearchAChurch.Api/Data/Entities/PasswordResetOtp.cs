namespace SearchAChurch.Api.Data.Entities;

public class PasswordResetOtp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 0;
    public bool IsConsumed { get; set; } = false;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}
