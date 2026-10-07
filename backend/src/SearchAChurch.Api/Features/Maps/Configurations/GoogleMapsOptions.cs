namespace SearchAChurch.Api.Features.Maps.Configurations;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://maps.googleapis.com/maps/api";
    public int TimeoutSeconds { get; set; } = 3;
    public int CircuitBreakerFailureThreshold { get; set; } = 3;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
