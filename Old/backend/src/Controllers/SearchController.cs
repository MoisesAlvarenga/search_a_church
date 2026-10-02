using Microsoft.AspNetCore.Mvc;
using SearchAChurch.Api.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChurchesController : ControllerBase
{
    private readonly MatchService _matchService;
    private readonly Repositories.InMemoryStore _store;

    public ChurchesController(MatchService matchService, Repositories.InMemoryStore store)
    {
        _matchService = matchService;
        _store = store;
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string? profile, [FromQuery] double latitude = 0, [FromQuery] double longitude = 0, [FromQuery] double radiusKm = 50, [FromQuery] string? language = null, [FromQuery] string? serviceType = null, [FromQuery] Guid? userProfileId = null, [FromQuery] bool includeReviews = false, [FromQuery] string sortBy = "matchScore")
    {
        var results = new List<object>();

        UserProfile? userProfile = null;
        if (userProfileId.HasValue)
        {
            userProfile = _store.Profiles.FirstOrDefault(p => p.Id == userProfileId.Value);
        }

        foreach (var ch in _store.Churches)
        {
            var distance = DistanceKm(latitude, longitude, ch.Latitude, ch.Longitude);
            var rating = _store.Reviews.Where(r => r.ChurchId == ch.Id).Select(r => r.Stars).DefaultIfEmpty(4).Average();
            var score = _matchService.ComputeMatchScore(userProfile, ch, distance, rating);

            results.Add(new {
                id = ch.Id,
                name = ch.Name,
                address = ch.Address,
                latitude = ch.Latitude,
                longitude = ch.Longitude,
                distanceKm = Math.Round(distance,2),
                schedule = ch.Schedule,
                profileTags = ch.ProfileTags,
                serviceType = ch.ServiceType,
                matchScore = score,
                rating = Math.Round(rating,2),
                reviewsCount = _store.Reviews.Count(r => r.ChurchId == ch.Id),
                isRegistered = ch.IsRegistered,
                source = ch.Source
            });
        }

        var ordered = results.OrderByDescending(r => ((dynamic)r).matchScore).ToList();
        return Ok(new { results = ordered });
    }

    private double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371; // km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat/2) * Math.Sin(dLat/2) + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon/2) * Math.Sin(dLon/2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
        return R * c;
    }

    private double ToRadians(double deg) => deg * (Math.PI/180);
}
