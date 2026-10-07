using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Gateways;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Integration;

[Trait("Category", "E2E")]
public class MapEndpointsE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TestPlaceId = "ChIJ_test_place_123";
    private const string NotFoundPlaceId = "ChIJ_non_existent";
    private const string ValidAddress = "Av. Paulista, 1000";
    private const string NotFoundAddress = "Endereço Desconhecido 99999";
    private static readonly string[] SampleOpeningHours = ["Segunda: Aberto"];

    private readonly Mock<IGooglePlacesGateway> _gatewayMock = new();
    private readonly HttpClient _client;
    private readonly string _accessToken;

    public MapEndpointsE2ETests(CustomWebApplicationFactory factory)
    {
        var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGooglePlacesGateway>();
                services.AddSingleton(_gatewayMock.Object);
            });
        });

        _client = testFactory.CreateClient();

        using var scope = testFactory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"map_test_{Guid.NewGuid():N}@church.org",
            Name = "Map Tester",
            PasswordHash = "hash",
            Role = UserRole.User,
            IsVerifiedRepresentative = false
        };

        _accessToken = tokenService.GenerateAccessToken(testUser, Guid.NewGuid());
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        if (content != null)
        {
            request.Content = content;
        }
        return request;
    }

    [Theory]
    [InlineData("/map/search?lat=-23.55&lng=-46.63")]
    [InlineData("/map/places/ChIJ123")]
    public async Task MapEndpoints_WithoutToken_Return401Unauthorized(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Geocode_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/map/geocode", new GeocodeRequest(ValidAddress));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchMap_WithValidCoordinates_Returns200OkWithResults()
    {
        // Arrange
        var googlePlace = new GooglePlaceResult(
            PlaceId: "ChIJ_external_nearby",
            Name: "Igreja Presbiteriana Externa",
            FormattedAddress: "Rua das Flores, 100",
            Latitude: -23.5510,
            Longitude: -46.6340
        );

        _gatewayMock
            .Setup(g => g.SearchNearbyPlacesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GooglePlaceResult>>.Success(new[] { googlePlace }));

        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/map/search?lat=-23.5505&lng=-46.6333&radiusKm=5.0");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MapSearchResponse>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.AppliedRadiusKm.Should().Be(5.0);
        body.IsDegraded.Should().BeFalse();
    }

    [Theory]
    [InlineData("/map/search")]
    [InlineData("/map/search?lat=95.0&lng=-46.63")]
    [InlineData("/map/search?lat=-23.55&lng=190.0")]
    [InlineData("/map/search?lat=-23.55&lng=-46.63&radiusKm=-2.0")]
    public async Task SearchMap_WithInvalidParameters_Returns400BadRequest(string uri)
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, uri);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPlaceDetails_WhenFound_Returns200Ok()
    {
        // Arrange
        var details = new GooglePlaceDetails(
            PlaceId: TestPlaceId,
            Name: "Catedral da Sé",
            FormattedAddress: "Praça da Sé, s/n",
            Latitude: -23.5505,
            Longitude: -46.6333,
            Rating: 4.8,
            UserRatingsTotal: 1200,
            PhoneNumber: "(11) 3107-6832",
            Website: "https://catedraldase.org.br",
            OpeningHours: SampleOpeningHours
        );

        _gatewayMock
            .Setup(g => g.GetPlaceDetailsAsync(TestPlaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GooglePlaceDetails>.Success(details));

        var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/map/places/{TestPlaceId}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GooglePlaceDetails>();
        body.Should().NotBeNull();
        body!.PlaceId.Should().Be(TestPlaceId);
        body.Name.Should().Be("Catedral da Sé");
    }

    [Fact]
    public async Task GetPlaceDetails_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        _gatewayMock
            .Setup(g => g.GetPlaceDetailsAsync(NotFoundPlaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GooglePlaceDetails>.Failure("PLACE_NOT_FOUND", "Local não encontrado."));

        var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/map/places/{NotFoundPlaceId}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Geocode_WhenFound_Returns200Ok()
    {
        // Arrange
        var geocodeResult = new GeocodeResult(
            FormattedAddress: ValidAddress,
            Latitude: -23.5614,
            Longitude: -46.6558
        );

        _gatewayMock
            .Setup(g => g.GeocodeAddressAsync(ValidAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeocodeResult>.Success(geocodeResult));

        var content = JsonContent.Create(new GeocodeRequest(ValidAddress));
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/map/geocode", content);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeocodeResult>();
        body.Should().NotBeNull();
        body!.FormattedAddress.Should().Be(ValidAddress);
        body.Latitude.Should().Be(-23.5614);
    }

    [Fact]
    public async Task Geocode_WhenNotFound_Returns404NotFound()
    {
        // Arrange
        _gatewayMock
            .Setup(g => g.GeocodeAddressAsync(NotFoundAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeocodeResult>.Failure("ADDRESS_NOT_FOUND", "Endereço não localizado."));

        var content = JsonContent.Create(new GeocodeRequest(NotFoundAddress));
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/map/geocode", content);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Geocode_WithEmptyAddress_Returns400BadRequest(string address)
    {
        // Arrange
        var content = JsonContent.Create(new GeocodeRequest(address));
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/map/geocode", content);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
