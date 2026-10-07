namespace SearchAChurch.Api.Features.Maps.Models;

public record GeocodeResult(
    string? FormattedAddress,
    double Latitude,
    double Longitude
);
