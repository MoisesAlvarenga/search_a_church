namespace SearchAChurch.Api.Features.Maps.Models;

public record GooglePlaceDetails(
    string PlaceId,
    string Name,
    string? FormattedAddress,
    double Latitude,
    double Longitude,
    string? PhoneNumber = null,
    string? Website = null,
    double? Rating = null,
    int? UserRatingsTotal = null,
    IReadOnlyList<string>? OpeningHours = null,
    IReadOnlyList<string>? PhotoUrls = null
);
