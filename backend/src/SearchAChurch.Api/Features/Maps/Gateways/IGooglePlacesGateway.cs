using SearchAChurch.Api.Common;
using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Gateways;

/// <summary>
/// Gateway contract for integrating with Google Maps Platform (Places Nearby Search, Place Details, and Geocoding APIs).
/// Implements resilient calls protected by Circuit Breaker and cache interception (AD-025, MAP-02, MAP-04, MAP-07).
/// </summary>
public interface IGooglePlacesGateway
{
    /// <summary>
    /// Searches for places within a radius around the provided coordinates.
    /// </summary>
    Task<Result<IReadOnlyList<GooglePlaceResult>>> SearchNearbyPlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves details for a specific place by placeId, consulting distributed cache first.
    /// </summary>
    Task<Result<GooglePlaceDetails>> GetPlaceDetailsAsync(
        string placeId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves textual address to geographic coordinates, consulting distributed cache first.
    /// </summary>
    Task<Result<GeocodeResult>> GeocodeAddressAsync(
        string address,
        CancellationToken ct = default);
}
