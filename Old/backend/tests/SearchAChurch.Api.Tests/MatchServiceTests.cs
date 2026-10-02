using System;
using System.Collections.Generic;
using SearchAChurch.Api.Models;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.Api.Tests;

public class MatchServiceTests
{
    [Fact]
    public void FullMatch_GeneratesHighScore()
    {
        var matchService = new MatchService();
        var profile = new UserProfile
        {
            PreferredLanguages = new List<string>{ "english" },
            PreferredStyles = new List<string>{ "contemporary" },
            PreferredServiceTypes = new List<string>{ "worship" },
            MaxDistanceKm = 20.0
        };

        var church = new Church
        {
            ProfileTags = new List<string>{ "english", "contemporary" },
            ServiceType = "worship"
        };

        double distanceKm = 5.0; // within max (distanceFactor = 0.75 -> +15)
        double rating = 4.5; // +9

        var score = matchService.ComputeMatchScore(profile, church, distanceKm, rating);

        // Expected: 30 + 30 + 20 + 15 + 9 = 104
        Assert.Equal(104.00, score);
    }

    [Fact]
    public void NullProfile_UsesFallbackDistanceAndRating()
    {
        var matchService = new MatchService();
        UserProfile? profile = null;
        var church = new Church();

        double distanceKm = 10.0; // fallback uses 50km baseline -> (50-10)/50 = 0.8 -> 50*0.8 = 40
        double rating = 5.0; // -> +10

        var score = matchService.ComputeMatchScore(profile, church, distanceKm, rating);

        Assert.Equal(50.00, score);
    }

    [Fact]
    public void RatingAboveTen_IsCappedToTwentyPoints()
    {
        var matchService = new MatchService();
        UserProfile? profile = null;
        var church = new Church();

        double distanceKm = 0.0; // (50-0)/50 = 1.0 -> 50
        double rating = 15.0; // capped to 10 -> +20

        var score = matchService.ComputeMatchScore(profile, church, distanceKm, rating);

        // 50 + 20 = 70
        Assert.Equal(70.00, score);
    }
}