using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Gateways;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;

namespace SearchAChurch.UnitTests.Features.Maps;

[Trait("Category", "Unit")]
public class MapOrchestratorServiceTests
{
    private const double CenterLat = -23.5505;
    private const double CenterLng = -46.6333;
    private const string OverlappingPlaceId = "ChIJ_shared_place_id";

    private readonly Mock<IGooglePlacesGateway> _gatewayMock = new();
    private readonly Mock<ILogger<MapOrchestratorService>> _loggerMock = new();
    private readonly IDeduplicationEngine _deduplicationEngine = new DeduplicationEngine();

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Theory]
    [InlineData(-91.0, 0.0)]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, -181.0)]
    [InlineData(0.0, 181.0)]
    public async Task SearchAsync_WithInvalidCoordinates_ReturnsFailure(double lat, double lng)
    {
        // Arrange
        await using var db = CreateContext();
        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(lat, lng, 5.0);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_COORDINATES");
    }

    [Fact]
    public async Task SearchAsync_WhenBothLocalAndExternalSucceed_UnifiesAndDeduplicatesAppFirst()
    {
        // Arrange
        await using var db = CreateContext();
        var localChurch = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Local Cadastrada",
            FormattedAddress = "Rua Central, 100",
            PlaceId = OverlappingPlaceId,
            Latitude = CenterLat,
            Longitude = CenterLng,
            IsVerified = true
        };
        db.Churches.Add(localChurch);
        await db.SaveChangesAsync();

        var googleDuplicate = new GooglePlaceResult(
            PlaceId: OverlappingPlaceId,
            Name: "Igreja Externa Duplicada",
            FormattedAddress: "R. Central, 100",
            Latitude: CenterLat,
            Longitude: CenterLng
        );
        var googleNew = new GooglePlaceResult(
            PlaceId: "ChIJ_unique_maps_place",
            Name: "Comunidade Externa",
            FormattedAddress: "Av. Próxima, 200",
            Latitude: CenterLat + 0.01,
            Longitude: CenterLng + 0.01
        );

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Success(new[] { googleDuplicate, googleNew }));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, 5.0);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var response = result.Value!;

        response.IsDegraded.Should().BeFalse();
        response.DegradedMessage.Should().BeNull();
        response.AppliedRadiusKm.Should().Be(5.0);
        response.Results.Should().HaveCount(2);

        // Local church retained as App source
        var appItem = response.Results.First(r => r.Source == ChurchSource.App);
        appItem.PlaceId.Should().Be(OverlappingPlaceId);
        appItem.Name.Should().Be("Igreja Local Cadastrada");

        // External new church added as Maps source
        var mapsItem = response.Results.First(r => r.Source == ChurchSource.Maps);
        mapsItem.PlaceId.Should().Be("ChIJ_unique_maps_place");
        mapsItem.Name.Should().Be("Comunidade Externa");
    }

    [Fact]
    public async Task SearchAsync_WhenGooglePlacesFails_GracefullyDegradesReturningLocalChurches()
    {
        // Arrange
        await using var db = CreateContext();
        var localChurch = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Local Resiliente",
            FormattedAddress = "Rua Teste, 50",
            PlaceId = "ChIJ_local_only",
            Latitude = CenterLat,
            Longitude = CenterLng,
            IsVerified = false
        };
        db.Churches.Add(localChurch);
        await db.SaveChangesAsync();

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Failure("CIRCUIT_BREAKER_OPEN", "Circuito aberto"));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, 5.0);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        response.IsDegraded.Should().BeTrue();
        response.DegradedMessage.Should().Contain("Provedor externo indisponível");
        response.Results.Should().HaveCount(1);
        response.Results[0].Name.Should().Be("Igreja Local Resiliente");
    }

    [Fact]
    public async Task SearchAsync_WhenGooglePlacesThrowsException_CatchesAndGracefullyDegrades()
    {
        // Arrange
        await using var db = CreateContext();
        var localChurch = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Local",
            FormattedAddress = "Rua Teste",
            Latitude = CenterLat,
            Longitude = CenterLng
        };
        db.Churches.Add(localChurch);
        await db.SaveChangesAsync();

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, 5.0);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.IsDegraded.Should().BeTrue();
        response.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_WithTextualQuery_FiltersLocalChurchesByNameOrAddress()
    {
        // Arrange
        await using var db = CreateContext();
        var church1 = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Primeira Igreja Batista da Penha",
            FormattedAddress = "Av. Penha, 10",
            Latitude = CenterLat,
            Longitude = CenterLng
        };
        var church2 = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Catedral Ortodoxa",
            FormattedAddress = "Rua Vergueiro, 20",
            Latitude = CenterLat,
            Longitude = CenterLng
        };
        db.Churches.AddRange(church1, church2);
        await db.SaveChangesAsync();

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), "Batista", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Success(Array.Empty<GooglePlaceResult>()));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, 5.0, Query: "Batista");

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Results.Should().HaveCount(1);
        result.Value.Results[0].Name.Should().Be("Primeira Igreja Batista da Penha");
    }

    [Fact]
    public async Task SearchAsync_FiltersChurchesBeyondAppliedRadius()
    {
        // Arrange
        await using var db = CreateContext();
        // Sé (-23.5505, -46.6333)
        var churchClose = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja da Sé",
            FormattedAddress = "Praça da Sé",
            Latitude = -23.5505,
            Longitude = -46.6333
        };
        // Far point (~30km away in Santos or north)
        var churchFar = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Muito Distante",
            FormattedAddress = "Longe, 999",
            Latitude = -23.9000,
            Longitude = -46.6333
        };
        db.Churches.AddRange(churchClose, churchFar);
        await db.SaveChangesAsync();

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Success(Array.Empty<GooglePlaceResult>()));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, RadiusKm: 5.0);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Results.Should().HaveCount(1);
        result.Value.Results[0].Name.Should().Be("Igreja da Sé");
    }

    [Theory]
    [InlineData(-5.0, 5.0)]
    [InlineData(0.0, 5.0)]
    [InlineData(75.0, 50.0)]
    [InlineData(12.5, 12.5)]
    public async Task SearchAsync_ClampsRadiusAppropriately(double requestedRadius, double expectedAppliedRadius)
    {
        // Arrange
        await using var db = CreateContext();
        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Success(Array.Empty<GooglePlaceResult>()));

        var service = new MapOrchestratorService(db, _gatewayMock.Object, _deduplicationEngine, _loggerMock.Object);
        var request = new MapSearchRequest(CenterLat, CenterLng, RadiusKm: requestedRadius);

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AppliedRadiusKm.Should().Be(expectedAppliedRadius);
    }
}
