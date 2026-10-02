namespace SearchAChurch.Api.Models;

public class Church
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Schedule { get; set; }
    public List<string>? ProfileTags { get; set; }
    public string? ServiceType { get; set; }
    public string Source { get; set; } = "app"; // or "maps"
    public bool IsRegistered { get; set; } = true;
}
