using FluentAssertions;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;

namespace SearchAChurch.UnitTests.Features.Maps;

[Trait("Category", "Unit")]
public class DeduplicationEngineTests
{
    private readonly DeduplicationEngine _engine = new();

    private const double CenterLat = -23.5505; // Praça da Sé, SP
    private const double CenterLng = -46.6333;

    [Fact]
    public void Deduplicate_WhenAppChurchesAndGooglePlacesOverlap_DiscardsGoogleDuplicateAccordingToAppFirstRule()
    {
        // Arrange
        const string overlappingPlaceId = "ChIJ_duplicate_123";

        var appChurch = new Church
        {
            Id = Guid.NewGuid(),
            PlaceId = overlappingPlaceId,
            Name = "Igreja Presbiteriana Unida",
            FormattedAddress = "Rua das Flores, 50",
            Latitude = -23.551,
            Longitude = -46.634,
            IsVerified = true
        };

        var googlePlace = new GooglePlaceResult(
            PlaceId: overlappingPlaceId,
            Name: "Igreja Presbiteriana (Google External)",
            FormattedAddress: "R. das Flores, 50 - Centro",
            Latitude: -23.5512,
            Longitude: -46.6341,
            Rating: 4.5,
            UserRatingsTotal: 30
        );

        // Act
        var results = _engine.Deduplicate(
            new[] { appChurch },
            new[] { googlePlace },
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(1);
        var item = results[0];
        item.Source.Should().Be(ChurchSource.App);
        item.PlaceId.Should().Be(overlappingPlaceId);
        item.Name.Should().Be("Igreja Presbiteriana Unida");
        item.IsRegistered.Should().BeTrue();
        item.IsVerifiedRepresentative.Should().BeTrue();
        item.CanClaim.Should().BeFalse();
    }

    [Fact]
    public void Deduplicate_WhenNoOverlap_IncludesBothSourcesWithCorrectMetadata()
    {
        // Arrange
        var appChurch = new Church
        {
            Id = Guid.NewGuid(),
            PlaceId = "ChIJ_app_only",
            Name = "Primeira Igreja Batista",
            FormattedAddress = "Av. Paulista, 1000",
            Latitude = -23.561,
            Longitude = -46.655,
            IsVerified = false
        };

        var googlePlace = new GooglePlaceResult(
            PlaceId: "ChIJ_maps_only",
            Name: "Comunidade Evangélica Externa",
            FormattedAddress: "Rua Augusta, 500",
            Latitude: -23.553,
            Longitude: -46.645,
            Rating: 4.8,
            UserRatingsTotal: 120
        );

        // Act
        var results = _engine.Deduplicate(
            new[] { appChurch },
            new[] { googlePlace },
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(2);

        var appItem = results.First(r => r.Source == ChurchSource.App);
        appItem.Name.Should().Be("Primeira Igreja Batista");
        appItem.IsRegistered.Should().BeTrue();
        appItem.CanClaim.Should().BeTrue(); // Not verified yet

        var mapsItem = results.First(r => r.Source == ChurchSource.Maps);
        mapsItem.Name.Should().Be("Comunidade Evangélica Externa");
        mapsItem.PlaceId.Should().Be("ChIJ_maps_only");
        mapsItem.IsRegistered.Should().BeFalse();
        mapsItem.IsVerifiedRepresentative.Should().BeFalse();
        mapsItem.RatingAverage.Should().Be(4.8);
        mapsItem.ReviewCount.Should().Be(120);
        mapsItem.CanClaim.Should().BeTrue(); // External place can be claimed
    }

    [Fact]
    public void Deduplicate_WhenAppChurchIsSoftDeleted_ExcludesItFromResults()
    {
        // Arrange
        var softDeleted = new Church
        {
            Id = Guid.NewGuid(),
            PlaceId = "ChIJ_deleted",
            Name = "Igreja Desativada",
            FormattedAddress = "Rua Antiga, 1",
            Latitude = -23.55,
            Longitude = -46.63,
            DeletedAt = DateTimeOffset.UtcNow
        };

        var googlePlace = new GooglePlaceResult(
            PlaceId: "ChIJ_active",
            Name: "Igreja Ativa",
            FormattedAddress: "Rua Nova, 2",
            Latitude: -23.55,
            Longitude: -46.63
        );

        // Act
        var results = _engine.Deduplicate(
            new[] { softDeleted },
            new[] { googlePlace },
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(1);
        results[0].PlaceId.Should().Be("ChIJ_active");
    }

    [Fact]
    public void Deduplicate_WhenInputsNullOrEmpty_ReturnsEmptyListWithoutThrowing()
    {
        // Act
        var results1 = _engine.Deduplicate(null!, null!, CenterLat, CenterLng);
        var results2 = _engine.Deduplicate(Array.Empty<Church>(), Array.Empty<GooglePlaceResult>(), CenterLat, CenterLng);

        // Assert
        results1.Should().BeEmpty();
        results2.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_WhenMultipleExternalResultsWithSamePlaceId_DeduplicatesExternalResults()
    {
        // Arrange
        var p1 = new GooglePlaceResult("ChIJ_same", "Igreja 1", "Rua A", -23.55, -46.63);
        var p2 = new GooglePlaceResult("ChIJ_same", "Igreja 1 Duplicada", "Rua A", -23.55, -46.63);

        // Act
        var results = _engine.Deduplicate(
            Array.Empty<Church>(),
            new[] { p1, p2 },
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(1);
        results[0].PlaceId.Should().Be("ChIJ_same");
    }

    [Fact]
    public void Deduplicate_CalculatesHaversineDistanceAndSortsAscending()
    {
        // Arrange
        // Center: Praça da Sé (-23.5505, -46.6333)
        // Church A: ~1.2 km away
        var churchClose = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Próxima",
            FormattedAddress = "Liberdade",
            Latitude = -23.5600,
            Longitude = -46.6350
        };

        // Church B: ~5.0 km away
        var churchFar = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Distante",
            FormattedAddress = "Pinheiros",
            Latitude = -23.5700,
            Longitude = -46.6800
        };

        // Act
        var results = _engine.Deduplicate(
            new[] { churchFar, churchClose },
            Array.Empty<GooglePlaceResult>(),
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Igreja Próxima");
        results[1].Name.Should().Be("Igreja Distante");
        results[0].DistanceKm.Should().BeLessThan(results[1].DistanceKm);
    }

    [Fact]
    public void Deduplicate_WhenDistancesIdentical_PrioritizesAppChurchesOverMaps()
    {
        // Arrange
        var appChurch = new Church
        {
            Id = Guid.NewGuid(),
            PlaceId = "ChIJ_app_point",
            Name = "Igreja da Base Oficial",
            FormattedAddress = "Rua Teste",
            Latitude = -23.5505,
            Longitude = -46.6333
        };

        var mapsPlace = new GooglePlaceResult(
            PlaceId: "ChIJ_maps_point",
            Name: "Igreja Externa Mesma Distância",
            FormattedAddress: "Rua Teste",
            Latitude: -23.5505,
            Longitude: -46.6333
        );

        // Act
        var results = _engine.Deduplicate(
            new[] { appChurch },
            new[] { mapsPlace },
            CenterLat,
            CenterLng);

        // Assert
        results.Should().HaveCount(2);
        results[0].Source.Should().Be(ChurchSource.App);
        results[1].Source.Should().Be(ChurchSource.Maps);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_WithSameCoordinates_ReturnsZero()
    {
        // Act
        var distance = DeduplicationEngine.CalculateHaversineDistanceKm(-23.55, -46.63, -23.55, -46.63);

        // Assert
        distance.Should().Be(0.0);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_WithKnownPoints_ReturnsAccurateDistance()
    {
        // Sé (-23.5505, -46.6333) to MASP (-23.5614, -46.6558) is approximately 2.6 - 2.7 km
        // Act
        var distance = DeduplicationEngine.CalculateHaversineDistanceKm(-23.5505, -46.6333, -23.5614, -46.6558);

        // Assert
        distance.Should().BeInRange(2.5, 2.8);
    }
}
