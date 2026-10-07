using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SearchAChurch.Api.Features.Maps.Configurations;
using SearchAChurch.Api.Features.Maps.Gateways;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;

namespace SearchAChurch.UnitTests.Features.Maps;

[Trait("Category", "Unit")]
public class GooglePlacesGatewayTests
{
    private readonly Mock<IPlacesCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GooglePlacesGateway>> _loggerMock = new();
    private readonly Mock<ICircuitBreaker> _circuitBreakerMock = new();
    private readonly IOptions<GoogleMapsOptions> _options = Options.Create(new GoogleMapsOptions
    {
        ApiKey = "test_api_key",
        BaseUrl = "https://maps.googleapis.com/maps/api",
        TimeoutSeconds = 3,
        CircuitBreakerFailureThreshold = 3,
        CircuitBreakerDurationSeconds = 30
    });

    public GooglePlacesGatewayTests()
    {
        _circuitBreakerMock.Setup(c => c.CanExecute()).Returns(true);
    }

    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public HttpRequestMessage? LastRequest { get; private set; }

        public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler, out DelegatingHandlerStub stub)
    {
        stub = new DelegatingHandlerStub(handler);
        return new HttpClient(stub);
    }

    [Theory]
    [InlineData(-91.0, 0.0, 1000.0)]
    [InlineData(91.0, 0.0, 1000.0)]
    [InlineData(0.0, -181.0, 1000.0)]
    [InlineData(0.0, 181.0, 1000.0)]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(0.0, 0.0, -100.0)]
    public async Task SearchNearbyPlacesAsync_WithInvalidCoordinatesOrRadius_ReturnsInvalidArgument(
        double lat, double lng, double radius)
    {
        // Arrange
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out _);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(lat, lng, radius);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task SearchNearbyPlacesAsync_WhenCircuitBreakerOpen_ReturnsCircuitBreakerOpen()
    {
        // Arrange
        _circuitBreakerMock.Setup(c => c.CanExecute()).Returns(false);
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out var stub);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(-23.55, -46.63, 5000);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CIRCUIT_BREAKER_OPEN");
        stub.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task SearchNearbyPlacesAsync_WhenSuccess_ParsesPlacesListCorrectly()
    {
        // Arrange
        const string json = """
        {
            "status": "OK",
            "results": [
                {
                    "place_id": "place_abc",
                    "name": "Igreja Batista Central",
                    "vicinity": "Av. Brasil, 100",
                    "geometry": {
                        "location": { "lat": -23.551, "lng": -46.631 }
                    },
                    "rating": 4.9,
                    "user_ratings_total": 85
                }
            ]
        }
        """;

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out var stub);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(-23.551, -46.631, 2000, "Batista");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].PlaceId.Should().Be("place_abc");
        result.Value[0].Name.Should().Be("Igreja Batista Central");
        result.Value[0].FormattedAddress.Should().Be("Av. Brasil, 100");
        result.Value[0].Latitude.Should().Be(-23.551);
        result.Value[0].Longitude.Should().Be(-46.631);
        result.Value[0].Rating.Should().Be(4.9);
        result.Value[0].UserRatingsTotal.Should().Be(85);

        stub.LastRequest.Should().NotBeNull();
        stub.LastRequest!.RequestUri!.ToString().Should().Contain("keyword=Batista");
        stub.LastRequest.RequestUri.ToString().Should().Contain("type=church");
        _circuitBreakerMock.Verify(c => c.RecordSuccess(), Times.Once);
    }

    [Fact]
    public async Task SearchNearbyPlacesAsync_WhenZeroResults_ReturnsEmptySuccessList()
    {
        // Arrange
        const string json = """{ "status": "ZERO_RESULTS", "results": [] }""";
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out _);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(-23.55, -46.63, 1000);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _circuitBreakerMock.Verify(c => c.RecordSuccess(), Times.Once);
    }

    [Fact]
    public async Task SearchNearbyPlacesAsync_WhenOverQueryLimit_ReturnsFailureAndRecordsCircuitFailure()
    {
        // Arrange
        const string json = """{ "status": "OVER_QUERY_LIMIT" }""";
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out _);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(-23.55, -46.63, 1000);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("OVER_QUERY_LIMIT");
        _circuitBreakerMock.Verify(c => c.RecordFailure(), Times.Once);
    }

    [Fact]
    public async Task SearchNearbyPlacesAsync_WhenHttp500_ReturnsFailureAndRecordsCircuitFailure()
    {
        // Arrange
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError), out _);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.SearchNearbyPlacesAsync(-23.55, -46.63, 1000);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("EXTERNAL_PROVIDER_UNAVAILABLE");
        _circuitBreakerMock.Verify(c => c.RecordFailure(), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPlaceDetailsAsync_WithInvalidPlaceId_ReturnsInvalidArgument(string? placeId)
    {
        // Arrange
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out _);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GetPlaceDetailsAsync(placeId!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task GetPlaceDetailsAsync_WhenCacheHit_ReturnsCachedDetailsWithoutHttpCall()
    {
        // Arrange
        var cached = new GooglePlaceDetails("place_cached", "Igreja Local", "Rua 1", -23.0, -46.0);
        _cacheMock.Setup(c => c.GetCachedPlaceAsync("place_cached", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out var stub);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GetPlaceDetailsAsync("place_cached");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cached);
        stub.LastRequest.Should().BeNull();
        _circuitBreakerMock.Verify(c => c.CanExecute(), Times.Never);
    }

    [Fact]
    public async Task GetPlaceDetailsAsync_WhenCacheMiss_FetchesFromGoogleAndCachesResult()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetCachedPlaceAsync("place_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GooglePlaceDetails?)null);

        const string json = """
        {
            "status": "OK",
            "result": {
                "place_id": "place_123",
                "name": "Catedral da Sé",
                "formatted_address": "Praça da Sé, s/n",
                "geometry": {
                    "location": { "lat": -23.5505, "lng": -46.6333 }
                },
                "formatted_phone_number": "+55 11 3107-6832",
                "website": "https://catedraldase.org.br",
                "rating": 4.7,
                "user_ratings_total": 5200,
                "opening_hours": {
                    "weekday_text": ["Segunda-feira: 08:00 – 19:00"]
                },
                "photos": [
                    { "photo_reference": "ref_photo_1" }
                ]
            }
        }
        """;

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out var stub);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GetPlaceDetailsAsync("place_123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PlaceId.Should().Be("place_123");
        result.Value.Name.Should().Be("Catedral da Sé");
        result.Value.FormattedAddress.Should().Be("Praça da Sé, s/n");
        result.Value.PhoneNumber.Should().Be("+55 11 3107-6832");
        result.Value.Website.Should().Be("https://catedraldase.org.br");
        result.Value.Rating.Should().Be(4.7);
        result.Value.UserRatingsTotal.Should().Be(5200);
        result.Value.OpeningHours.Should().ContainSingle();
        result.Value.PhotoUrls.Should().ContainSingle();

        stub.LastRequest.Should().NotBeNull();
        _cacheMock.Verify(c => c.CachePlaceAsync("place_123", It.IsAny<GooglePlaceDetails>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
        _circuitBreakerMock.Verify(c => c.RecordSuccess(), Times.Once);
    }

    [Fact]
    public async Task GetPlaceDetailsAsync_WhenNotFound_ReturnsPlaceNotFound()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetCachedPlaceAsync("place_404", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GooglePlaceDetails?)null);

        const string json = """{ "status": "NOT_FOUND" }""";
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out _);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GetPlaceDetailsAsync("place_404");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PLACE_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GeocodeAddressAsync_WithInvalidAddress_ReturnsInvalidArgument(string? address)
    {
        // Arrange
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out _);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GeocodeAddressAsync(address!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_ARGUMENT");
    }

    [Fact]
    public async Task GeocodeAddressAsync_WhenCacheHit_ReturnsCachedWithoutHttpCall()
    {
        // Arrange
        var cached = new GeocodeResult("Av. Paulista, 1000", -23.56, -46.65);
        _cacheMock.Setup(c => c.GetCachedGeocodeAsync("Av. Paulista, 1000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out var stub);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GeocodeAddressAsync("Av. Paulista, 1000");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cached);
        stub.LastRequest.Should().BeNull();
        _circuitBreakerMock.Verify(c => c.CanExecute(), Times.Never);
    }

    [Fact]
    public async Task GeocodeAddressAsync_WhenCacheMiss_FetchesFromGoogleAndCachesResult()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetCachedGeocodeAsync("Praça da Sé", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodeResult?)null);

        const string json = """
        {
            "status": "OK",
            "results": [
                {
                    "formatted_address": "Praça da Sé - Sé, São Paulo - SP, Brasil",
                    "geometry": {
                        "location": { "lat": -23.5505, "lng": -46.6333 }
                    }
                }
            ]
        }
        """;

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out var stub);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GeocodeAddressAsync("Praça da Sé");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FormattedAddress.Should().Be("Praça da Sé - Sé, São Paulo - SP, Brasil");
        result.Value.Latitude.Should().Be(-23.5505);
        result.Value.Longitude.Should().Be(-46.6333);

        stub.LastRequest.Should().NotBeNull();
        _cacheMock.Verify(c => c.CacheGeocodeAsync("Praça da Sé", It.IsAny<GeocodeResult>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
        _circuitBreakerMock.Verify(c => c.RecordSuccess(), Times.Once);
    }

    [Fact]
    public async Task GeocodeAddressAsync_WhenZeroResults_ReturnsAddressNotFound()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetCachedGeocodeAsync("Endereço Inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodeResult?)null);

        const string json = """{ "status": "ZERO_RESULTS", "results": [] }""";
        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }, out _);

        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GeocodeAddressAsync("Endereço Inexistente");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task GeocodeAddressAsync_WhenCircuitBreakerOpenAndCacheMiss_ReturnsCircuitBreakerOpen()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetCachedGeocodeAsync("Rua Teste", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodeResult?)null);
        _circuitBreakerMock.Setup(c => c.CanExecute()).Returns(false);

        var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK), out var stub);
        var gateway = new GooglePlacesGateway(httpClient, _cacheMock.Object, _options, _loggerMock.Object, _circuitBreakerMock.Object);

        // Act
        var result = await gateway.GeocodeAddressAsync("Rua Teste");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CIRCUIT_BREAKER_OPEN");
        stub.LastRequest.Should().BeNull();
    }
}
