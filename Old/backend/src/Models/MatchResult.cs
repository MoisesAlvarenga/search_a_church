namespace SearchAChurch.Api.Models;

public class MatchResult
{
    public Guid ChurchId { get; set; }
    public double MatchScore { get; set; }
    public double DistanceKm { get; set; }
    public double Rating { get; set; }
    public int ReviewsCount { get; set; }
    public string Source { get; set; } = "app";
}
