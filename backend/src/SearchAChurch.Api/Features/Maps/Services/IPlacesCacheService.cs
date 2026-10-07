using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Distributed caching service for Google Places details and geocoding responses.
/// Implements resilient 30-day caching to minimize Google Places API quota consumption (AD-025, MAP-07).
/// </summary>
public interface IPlacesCacheService
{
    /// <summary>
    /// Retrieves cached place details by place_id. Returns null on cache miss or Redis unavailability.
    /// </summary>
    Task<GooglePlaceDetails?> GetCachedPlaceAsync(string placeId, CancellationToken ct = default);

    /// <summary>
    /// Caches place details with configurable TTL (defaulting to 30 days). Fails open on Redis errors.
    /// </summary>
    Task CachePlaceAsync(string placeId, GooglePlaceDetails details, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves cached geocoding result by address. Returns null on cache miss or Redis unavailability.
    /// </summary>
    Task<GeocodeResult?> GetCachedGeocodeAsync(string address, CancellationToken ct = default);

    /// <summary>
    /// Caches geocoding result with configurable TTL (defaulting to 30 days) keyed by SHA256 of normalized address.
    /// </summary>
    Task CacheGeocodeAsync(string address, GeocodeResult result, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Convenience alias for <see cref="GetCachedPlaceAsync"/>.
    /// </summary>
    Task<GooglePlaceDetails?> GetPlaceDetailsAsync(string placeId, CancellationToken ct = default)
        => GetCachedPlaceAsync(placeId, ct);

    /// <summary>
    /// Convenience alias for <see cref="CachePlaceAsync"/>.
    /// </summary>
    Task SetPlaceDetailsAsync(string placeId, GooglePlaceDetails details, TimeSpan? ttl = null, CancellationToken ct = default)
        => CachePlaceAsync(placeId, details, ttl, ct);

    /// <summary>
    /// Convenience alias for <see cref="GetCachedGeocodeAsync"/>.
    /// </summary>
    Task<GeocodeResult?> GetGeocodeAsync(string address, CancellationToken ct = default)
        => GetCachedGeocodeAsync(address, ct);

    /// <summary>
    /// Convenience alias for <see cref="CacheGeocodeAsync"/>.
    /// </summary>
    Task SetGeocodeAsync(string address, GeocodeResult result, TimeSpan? ttl = null, CancellationToken ct = default)
        => CacheGeocodeAsync(address, result, ttl, ct);
}
