using FluentAssertions;
using SearchAChurch.Api.Features.Claim.Services;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class GeofencingServiceTests
{
    private readonly GeofencingService _service = new();

    // Coordenadas de referência: Praça da Sé, São Paulo (-23.55052, -46.633309)
    private const double ChurchLat = -23.55052;
    private const double ChurchLng = -46.633309;

    [Fact]
    public void CalculateHaversineDistanceMeters_SameCoordinates_ReturnsZero()
    {
        // Act
        double distance = _service.CalculateHaversineDistanceMeters(ChurchLat, ChurchLng, ChurchLat, ChurchLng);

        // Assert
        distance.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CalculateHaversineDistanceMeters_KnownCoordinates_ReturnsAccurateDistance()
    {
        // Arrange
        // Ponto a ~231 metros: Pateo do Collegio (-23.54848, -46.63290)
        double collegioLat = -23.54848;
        double collegioLng = -46.63290;

        // Act
        double distance = _service.CalculateHaversineDistanceMeters(ChurchLat, ChurchLng, collegioLat, collegioLng);

        // Assert
        distance.Should().BeInRange(225.0, 235.0);
    }

    [Theory]
    [InlineData(-91.0, 0.0)]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, -181.0)]
    [InlineData(0.0, 181.0)]
    public void CalculateHaversineDistanceMeters_InvalidCoordinates_ThrowsArgumentOutOfRangeException(double lat, double lon)
    {
        // Act & Assert
        var act1 = () => _service.CalculateHaversineDistanceMeters(lat, lon, 0, 0);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => _service.CalculateHaversineDistanceMeters(0, 0, lat, lon);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidatePresence_WhenMockLocationIsTrue_RejectsWithMockLocationDetected()
    {
        // Arrange
        // Dispositivo exatamente na coordenada da igreja, mas simulando GPS
        double deviceLat = ChurchLat;
        double deviceLng = ChurchLng;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 10.0,
            isMockLocation: true
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GeofencingService.ErrorMockLocationDetected);
        result.ErrorMessage.Should().Contain("simulada");
    }

    [Fact]
    public void ValidatePresence_WhenAccuracyGreaterThan50Meters_RejectsWithInsufficientGpsAccuracy()
    {
        // Arrange
        // Dispositivo a 20m, mas com precisão fraca (80m)
        double deviceLat = -23.55060;
        double deviceLng = -46.633309;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 80.0,
            isMockLocation: false
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GeofencingService.ErrorInsufficientGpsAccuracy);
        result.ErrorMessage.Should().Contain("≤ 50 metros");
    }

    [Fact]
    public void ValidatePresence_WhenDistanceExceeds100Meters_RejectsWithOutOfAllowedRadius()
    {
        // Arrange
        // Ponto a ~231 metros (Pateo do Collegio) com precisão ótima (15m)
        double deviceLat = -23.54848;
        double deviceLng = -46.63290;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 15.0,
            isMockLocation: false
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GeofencingService.ErrorOutOfAllowedRadius);
        result.DistanceMeters.Should().BeGreaterThan(100.0);
        result.ErrorMessage.Should().Contain("100 metros");
    }

    [Fact]
    public void ValidatePresence_WhenWithin100MetersAndAccuracyWithin50Meters_ApprovesPresence()
    {
        // Arrange
        // Deslocamento de ~0.0003 graus (~33 metros)
        double deviceLat = -23.55022;
        double deviceLng = -46.633309;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 20.0,
            isMockLocation: false
        );

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.DistanceMeters.Should().BeInRange(30.0, 40.0);
    }

    [Fact]
    public void ValidatePresence_BoundaryCase_AccuracyAtExactly50Meters_Approves()
    {
        // Arrange
        double deviceLat = ChurchLat;
        double deviceLng = ChurchLng;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 50.0,
            isMockLocation: false
        );

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePresence_BoundaryCase_AccuracyAt50Point1Meters_Rejects()
    {
        // Arrange
        double deviceLat = ChurchLat;
        double deviceLng = ChurchLng;

        // Act
        var result = _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: deviceLat,
            deviceLng: deviceLng,
            horizontalAccuracyMeters: 50.1,
            isMockLocation: false
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(GeofencingService.ErrorInsufficientGpsAccuracy);
    }

    [Fact]
    public void ValidatePresence_WhenAccuracyIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _service.ValidatePresence(
            churchLat: ChurchLat,
            churchLng: ChurchLng,
            deviceLat: ChurchLat,
            deviceLng: ChurchLng,
            horizontalAccuracyMeters: -5.0,
            isMockLocation: false
        );

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Horizontal accuracy cannot be negative*");
    }
}
