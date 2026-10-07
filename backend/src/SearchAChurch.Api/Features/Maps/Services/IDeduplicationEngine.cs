using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Engine responsible for consolidating and deduplicating church results from local database and Google Places API.
/// Applies strict App-First precedence (AD-023) and Haversine distance calculations (MAP-04, MAP-05, MAP-06).
/// </summary>
public interface IDeduplicationEngine
{
    /// <summary>
    /// Unifies and deduplicates church results from internal database and external Google Places provider.
    /// External places with place_id already present in local database are discarded.
    /// Unregistered external places receive Source = Maps and CanClaim = true.
    /// </summary>
    IReadOnlyList<ChurchMapItemDto> Deduplicate(
        IEnumerable<Church> appChurches,
        IEnumerable<GooglePlaceResult> googlePlaces,
        double centerLat,
        double centerLng);
}
