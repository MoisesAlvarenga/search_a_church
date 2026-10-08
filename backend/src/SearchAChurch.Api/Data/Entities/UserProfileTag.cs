namespace SearchAChurch.Api.Data.Entities;

public class UserProfileTag
{
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;

    public Guid TagId { get; set; }
    public TagCatalog Tag { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
