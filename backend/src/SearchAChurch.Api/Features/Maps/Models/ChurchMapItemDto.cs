namespace SearchAChurch.Api.Features.Maps.Models;

public enum ChurchSource
{
    App,
    Maps
}

public record ChurchMapItemDto(
    string Id,
    string? PlaceId,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    double DistanceKm,
    ChurchSource Source,
    bool IsRegistered,
    bool IsVerifiedRepresentative,
    double? RatingAverage,
    int? ReviewCount,
    bool CanClaim
);
