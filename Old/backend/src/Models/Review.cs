namespace SearchAChurch.Api.Models;

public class Review
{
    public Guid Id { get; set; }
    public Guid ChurchId { get; set; }
    public Guid UserId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
