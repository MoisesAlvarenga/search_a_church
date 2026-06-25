using SearchAChurch.Api.Models;

namespace SearchAChurch.Api.Services;

public class MatchService
{
    public double ComputeMatchScore(UserProfile? profile, Church church, double distanceKm, double rating)
    {
        double score = 0.0;

        if (profile != null)
        {
            // language match
            if (profile.PreferredLanguages != null && church.ProfileTags != null)
            {
                var langMatch = profile.PreferredLanguages.Intersect(church.ProfileTags).Any() ? 1.0 : 0.0;
                score += 30 * langMatch;
            }

            // style match
            if (profile.PreferredStyles != null && church.ProfileTags != null)
            {
                var styleMatch = profile.PreferredStyles.Intersect(church.ProfileTags).Any() ? 1.0 : 0.0;
                score += 30 * styleMatch;
            }

            // service type match
            if (profile.PreferredServiceTypes != null && church.ServiceType != null)
            {
                var typeMatch = profile.PreferredServiceTypes.Contains(church.ServiceType) ? 1.0 : 0.0;
                score += 20 * typeMatch;
            }

            // distance influence (closer is better)
            var distanceFactor = Math.Max(0, (profile.MaxDistanceKm - distanceKm) / profile.MaxDistanceKm);
            score += 20 * distanceFactor;
        }
        else
        {
            // fallback: distance and rating
            var distanceFactor = Math.Max(0, (50.0 - distanceKm) / 50.0);
            score += 50 * distanceFactor;
        }

        // rating influence (normalized)
        score += Math.Min(10, rating) * 2; // up to +20

        return Math.Round(score, 2);
    }
}
