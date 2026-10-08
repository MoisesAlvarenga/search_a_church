namespace SearchAChurch.Api.Data.Entities;

public class ChurchTag
{
    public Guid ChurchId { get; set; }
    public Church Church { get; set; } = null!;

    public Guid TagId { get; set; }
    public TagCatalog Tag { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
