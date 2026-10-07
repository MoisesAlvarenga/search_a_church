using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Features.Maps.Models;
using StackExchange.Redis;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Redis distributed cache for Google Places API responses and geocoding coordinates.
/// Adheres to AD-025 with a default 30-day TTL and circuit-safe fail-open resilience.
/// </summary>
public class PlacesCacheService : IPlacesCacheService
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(30);

    private const string PlaceDetailsKeyPrefix = "places:details:";
    private const string GeocodeKeyPrefix = "geo:address:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<PlacesCacheService> _logger;

    public PlacesCacheService(
        ILogger<PlacesCacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task<GooglePlaceDetails?> GetCachedPlaceAsync(string placeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return null;
        }

        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Fail-open returning null for place details {PlaceId}.", placeId);
            return null;
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"{PlaceDetailsKeyPrefix}{placeId.Trim()}";
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<GooglePlaceDetails>(value.ToString(), SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached place details for {PlaceId} from Redis. Failing open.", placeId);
            return null;
        }
    }

    public async Task CachePlaceAsync(string placeId, GooglePlaceDetails details, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(placeId) || details == null)
        {
            return;
        }

        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Skipping caching place details for {PlaceId}.", placeId);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"{PlaceDetailsKeyPrefix}{placeId.Trim()}";
            var effectiveTtl = ttl.HasValue && ttl.Value > TimeSpan.Zero ? ttl.Value : DefaultTtl;
            var json = JsonSerializer.Serialize(details, SerializerOptions);

            await db.StringSetAsync(key, json, effectiveTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache place details for {PlaceId} in Redis. Failing open.", placeId);
        }
    }

    public async Task<GeocodeResult?> GetCachedGeocodeAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Fail-open returning null for geocode address.");
            return null;
        }

        try
        {
            var hash = ComputeSha256(address);
            var key = $"{GeocodeKeyPrefix}{hash}";
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<GeocodeResult>(value.ToString(), SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached geocode for address from Redis. Failing open.");
            return null;
        }
    }

    public async Task CacheGeocodeAsync(string address, GeocodeResult result, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address) || result == null)
        {
            return;
        }

        if (_redis == null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Skipping caching geocode for address.");
            return;
        }

        try
        {
            var hash = ComputeSha256(address);
            var key = $"{GeocodeKeyPrefix}{hash}";
            var effectiveTtl = ttl.HasValue && ttl.Value > TimeSpan.Zero ? ttl.Value : DefaultTtl;
            var json = JsonSerializer.Serialize(result, SerializerOptions);
            var db = _redis.GetDatabase();

            await db.StringSetAsync(key, json, effectiveTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache geocode in Redis. Failing open.");
        }
    }

    public static string ComputeSha256(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
