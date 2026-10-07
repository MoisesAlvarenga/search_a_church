using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Unifies and deduplicates church results from internal database and Google Places API.
/// Enforces App-First precedence (AD-023) using place_id anchor and Haversine distance calculations.
/// </summary>
public class DeduplicationEngine : IDeduplicationEngine
{
    private const double EarthRadiusKm = 6371.0;

    public IReadOnlyList<ChurchMapItemDto> Deduplicate(
        IEnumerable<Church> appChurches,
        IEnumerable<GooglePlaceResult> googlePlaces,
        double centerLat,
        double centerLng)
    {
        var unified = new List<ChurchMapItemDto>();
        var registeredPlaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ProcessAppChurches(appChurches, centerLat, centerLng, unified, registeredPlaceIds);
        ProcessExternalPlaces(googlePlaces, centerLat, centerLng, registeredPlaceIds, unified);

        return unified
            .OrderBy(item => item.DistanceKm)
            .ThenBy(item => item.Source == ChurchSource.App ? 0 : 1)
            .ThenBy(item => item.Name)
            .ToList()
            .AsReadOnly();
    }

    private static void ProcessAppChurches(
        IEnumerable<Church>? appChurches,
        double centerLat,
        double centerLng,
        List<ChurchMapItemDto> destination,
        HashSet<string> registeredPlaceIds)
    {
        if (appChurches == null)
        {
            return;
        }

        foreach (var church in appChurches)
        {
            if (church.DeletedAt != null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(church.PlaceId))
            {
                registeredPlaceIds.Add(church.PlaceId.Trim());
            }

            var distance = CalculateHaversineDistanceKm(centerLat, centerLng, church.Latitude, church.Longitude);
            destination.Add(MapFromChurch(church, distance));
        }
    }

    private static ChurchMapItemDto MapFromChurch(Church church, double distance) =>
        new(
            Id: church.Id.ToString(),
            PlaceId: church.PlaceId?.Trim(),
            Name: church.Name,
            Address: church.FormattedAddress,
            Latitude: church.Latitude,
            Longitude: church.Longitude,
            DistanceKm: distance,
            Source: ChurchSource.App,
            IsRegistered: true,
            IsVerifiedRepresentative: church.IsVerified,
            RatingAverage: null,
            ReviewCount: null,
            CanClaim: !church.IsVerified
        );

    private static void ProcessExternalPlaces(
        IEnumerable<GooglePlaceResult>? googlePlaces,
        double centerLat,
        double centerLng,
        HashSet<string> registeredPlaceIds,
        List<ChurchMapItemDto> destination)
    {
        if (googlePlaces == null)
        {
            return;
        }

        var processedMapsPlaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var place in googlePlaces)
        {
            if (string.IsNullOrWhiteSpace(place.PlaceId) || string.IsNullOrWhiteSpace(place.Name))
            {
                continue;
            }

            var normalizedPlaceId = place.PlaceId.Trim();

            if (registeredPlaceIds.Contains(normalizedPlaceId) || !processedMapsPlaceIds.Add(normalizedPlaceId))
            {
                continue;
            }

            var distance = CalculateHaversineDistanceKm(centerLat, centerLng, place.Latitude, place.Longitude);
            destination.Add(MapFromGooglePlace(place, normalizedPlaceId, distance));
        }
    }

    private static ChurchMapItemDto MapFromGooglePlace(GooglePlaceResult place, string normalizedPlaceId, double distance) =>
        new(
            Id: $"maps_{normalizedPlaceId}",
            PlaceId: normalizedPlaceId,
            Name: place.Name.Trim(),
            Address: place.FormattedAddress ?? string.Empty,
            Latitude: place.Latitude,
            Longitude: place.Longitude,
            DistanceKm: distance,
            Source: ChurchSource.Maps,
            IsRegistered: false,
            IsVerifiedRepresentative: false,
            RatingAverage: place.Rating,
            ReviewCount: place.UserRatingsTotal,
            CanClaim: true
        );

    /// <summary>
    /// Calculates the great-circle distance between two geographic points using the Haversine formula in kilometers.
    /// </summary>
    public static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var rLat1 = DegreesToRadians(lat1);
        var rLat2 = DegreesToRadians(lat2);

        var a = (Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)) +
                (Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0));

        a = Math.Clamp(a, 0.0, 1.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return Math.Round(EarthRadiusKm * c, 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
