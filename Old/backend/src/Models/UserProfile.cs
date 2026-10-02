namespace SearchAChurch.Api.Models;

public class UserProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string>? PreferredLanguages { get; set; }
    public List<string>? PreferredStyles { get; set; }
    public List<string>? PreferredServiceTypes { get; set; }
    public double MaxDistanceKm { get; set; } = 50.0;
}
