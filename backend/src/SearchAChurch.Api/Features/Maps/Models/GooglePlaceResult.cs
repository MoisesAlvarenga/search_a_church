namespace SearchAChurch.Api.Features.Maps.Models;

public record GooglePlaceResult(
    string PlaceId,
    string Name,
    string? FormattedAddress,
    double Latitude,
    double Longitude,
    double? Rating = null,
    int? UserRatingsTotal = null
);
