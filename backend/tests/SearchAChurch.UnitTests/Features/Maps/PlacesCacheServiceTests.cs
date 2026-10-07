using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;
using StackExchange.Redis;

namespace SearchAChurch.UnitTests.Features.Maps;

[Trait("Category", "Unit")]
public class PlacesCacheServiceTests
{
    private readonly Mock<ILogger<PlacesCacheService>> _loggerMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] SampleOpeningHours = ["Domingo: 09:00 - 12:00", "Domingo: 18:00 - 20:00"];
    private static readonly string[] SamplePhotoUrls = ["https://photos.example.com/1.jpg"];

    public PlacesCacheServiceTests()
    {
        _redisMock.Setup(r => r.IsConnected).Returns(true);
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCachedPlaceAsync_WhenPlaceIdIsNullOrWhiteSpace_ReturnsNull(string? placeId)
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedPlaceAsync(placeId!);

        // Assert
        result.Should().BeNull();
        _databaseMock.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task GetCachedPlaceAsync_WhenRedisNull_ReturnsNull()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, redis: null);

        // Act
        var result = await service.GetCachedPlaceAsync("place_123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedPlaceAsync_WhenRedisDisconnected_ReturnsNull()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedPlaceAsync("place_123");

        // Assert
        result.Should().BeNull();
        _databaseMock.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task GetCachedPlaceAsync_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "places:details:place_123"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedPlaceAsync("place_123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedPlaceAsync_WhenCacheHit_ReturnsDeserializedDetails()
    {
        // Arrange
        var expected = new GooglePlaceDetails(
            PlaceId: "place_123",
            Name: "Primeira Igreja Batista",
            FormattedAddress: "Av. Paulista, 1000",
            Latitude: -23.561,
            Longitude: -46.655,
            PhoneNumber: "+55 11 99999-9999",
            Website: "https://igreja.org",
            Rating: 4.8,
            UserRatingsTotal: 120,
            OpeningHours: SampleOpeningHours,
            PhotoUrls: SamplePhotoUrls
        );
        var json = JsonSerializer.Serialize(expected, JsonOptions);

        _databaseMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "places:details:place_123"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(json);

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedPlaceAsync("place_123");

        // Assert
        result.Should().NotBeNull();
        result!.PlaceId.Should().Be("place_123");
        result.Name.Should().Be("Primeira Igreja Batista");
        result.FormattedAddress.Should().Be("Av. Paulista, 1000");
        result.Latitude.Should().Be(-23.561);
        result.Longitude.Should().Be(-46.655);
        result.Rating.Should().Be(4.8);
        result.UserRatingsTotal.Should().Be(120);
        result.OpeningHours.Should().HaveCount(2);
        result.PhotoUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCachedPlaceAsync_WhenRedisThrowsException_FailsOpenReturningNull()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis timeout"));

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedPlaceAsync("place_123");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CachePlaceAsync_WhenPlaceIdIsNullOrWhiteSpace_DoesNotWriteToRedis(string? placeId)
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var details = new GooglePlaceDetails("place_1", "Igreja Central", "Rua A", -23.5, -46.6);

        // Act
        await service.CachePlaceAsync(placeId!, details);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CachePlaceAsync_WhenDetailsNull_DoesNotWriteToRedis()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.CachePlaceAsync("place_1", null!);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CachePlaceAsync_WhenRedisNull_FailsOpenWithoutThrowing()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, redis: null);
        var details = new GooglePlaceDetails("place_1", "Igreja Central", "Rua A", -23.5, -46.6);

        // Act & Assert
        var act = async () => await service.CachePlaceAsync("place_1", details);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CachePlaceAsync_WhenRedisDisconnected_FailsOpenWithoutThrowing()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var details = new GooglePlaceDetails("place_1", "Igreja Central", "Rua A", -23.5, -46.6);

        // Act
        await service.CachePlaceAsync("place_1", details);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CachePlaceAsync_WithDefaultTtl_CachesWith30DaysExpiration()
    {
        // Arrange
        var details = new GooglePlaceDetails("place_123", "Igreja da Paz", "Av. Brasil, 500", -22.9, -43.2);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.CachePlaceAsync("place_123", details);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "places:details:place_123"),
            It.Is<RedisValue>(v => v.ToString().Contains("Igreja da Paz")),
            It.Is<Expiration>(e => e.ToString().Contains("2592000")),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CachePlaceAsync_WithCustomTtl_CachesWithProvidedExpiration()
    {
        // Arrange
        var details = new GooglePlaceDetails("place_123", "Igreja da Paz", "Av. Brasil, 500", -22.9, -43.2);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var customTtl = TimeSpan.FromHours(12);

        // Act
        await service.CachePlaceAsync("place_123", details, customTtl);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "places:details:place_123"),
            It.IsAny<RedisValue>(),
            It.Is<Expiration>(e => e.ToString().Contains("43200")),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CachePlaceAsync_WhenRedisThrowsException_FailsOpenWithoutThrowing()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis timeout"));

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var details = new GooglePlaceDetails("place_123", "Igreja da Paz", "Av. Brasil, 500", -22.9, -43.2);

        // Act & Assert
        var act = async () => await service.CachePlaceAsync("place_123", details);
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCachedGeocodeAsync_WhenAddressIsNullOrWhiteSpace_ReturnsNull(string? address)
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedGeocodeAsync(address!);

        // Assert
        result.Should().BeNull();
        _databaseMock.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task GetCachedGeocodeAsync_WhenRedisNull_ReturnsNull()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, redis: null);

        // Act
        var result = await service.GetCachedGeocodeAsync("Av. Paulista, 1000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedGeocodeAsync_WhenRedisDisconnected_ReturnsNull()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedGeocodeAsync("Av. Paulista, 1000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedGeocodeAsync_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        var address = "Av. Paulista, 1000";
        var hash = PlacesCacheService.ComputeSha256(address);

        _databaseMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == $"geo:address:{hash}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedGeocodeAsync(address);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedGeocodeAsync_WhenCacheHit_ReturnsDeserializedResult()
    {
        // Arrange
        var address = "Av. Paulista, 1000";
        var hash = PlacesCacheService.ComputeSha256(address);
        var expected = new GeocodeResult("Av. Paulista, 1000, Bela Vista, São Paulo - SP", -23.5614, -46.6558);
        var json = JsonSerializer.Serialize(expected, JsonOptions);

        _databaseMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == $"geo:address:{hash}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(json);

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedGeocodeAsync(address);

        // Assert
        result.Should().NotBeNull();
        result!.FormattedAddress.Should().Be("Av. Paulista, 1000, Bela Vista, São Paulo - SP");
        result.Latitude.Should().Be(-23.5614);
        result.Longitude.Should().Be(-46.6558);
    }

    [Fact]
    public async Task GetCachedGeocodeAsync_WhenRedisThrowsException_FailsOpenReturningNull()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection lost"));

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        var result = await service.GetCachedGeocodeAsync("Rua das Flores, 123");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CacheGeocodeAsync_WhenAddressIsNullOrWhiteSpace_DoesNotWriteToRedis(string? address)
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var geocode = new GeocodeResult("Rua A, 1", -23.5, -46.6);

        // Act
        await service.CacheGeocodeAsync(address!, geocode);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CacheGeocodeAsync_WhenResultNull_DoesNotWriteToRedis()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.CacheGeocodeAsync("Rua A, 1", null!);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CacheGeocodeAsync_WhenRedisNull_FailsOpenWithoutThrowing()
    {
        // Arrange
        var service = new PlacesCacheService(_loggerMock.Object, redis: null);
        var geocode = new GeocodeResult("Rua A, 1", -23.5, -46.6);

        // Act & Assert
        var act = async () => await service.CacheGeocodeAsync("Rua A, 1", geocode);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CacheGeocodeAsync_WhenRedisDisconnected_FailsOpenWithoutThrowing()
    {
        // Arrange
        _redisMock.Setup(r => r.IsConnected).Returns(false);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var geocode = new GeocodeResult("Rua A, 1", -23.5, -46.6);

        // Act
        await service.CacheGeocodeAsync("Rua A, 1", geocode);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CacheGeocodeAsync_WithDefaultTtl_HashesAddressAndCachesWith30DaysExpiration()
    {
        // Arrange
        var address = "Av. Brigadeiro Faria Lima, 2000";
        var hash = PlacesCacheService.ComputeSha256(address);
        var geocode = new GeocodeResult("Av. Brigadeiro Faria Lima, 2000 - Pinheiros", -23.57, -46.69);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.CacheGeocodeAsync(address, geocode);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == $"geo:address:{hash}"),
            It.Is<RedisValue>(v => v.ToString().Contains("Pinheiros")),
            It.Is<Expiration>(e => e.ToString().Contains("2592000")),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CacheGeocodeAsync_WithCustomTtl_CachesWithProvidedExpiration()
    {
        // Arrange
        var address = "Av. Brigadeiro Faria Lima, 2000";
        var hash = PlacesCacheService.ComputeSha256(address);
        var geocode = new GeocodeResult("Av. Brigadeiro Faria Lima, 2000 - Pinheiros", -23.57, -46.69);
        var customTtl = TimeSpan.FromDays(7);
        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.CacheGeocodeAsync(address, geocode, customTtl);

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == $"geo:address:{hash}"),
            It.IsAny<RedisValue>(),
            It.Is<Expiration>(e => e.ToString().Contains(((long)customTtl.TotalSeconds).ToString())),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CacheGeocodeAsync_WhenRedisThrowsException_FailsOpenWithoutThrowing()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis timeout"));

        var service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);
        var geocode = new GeocodeResult("Rua A, 1", -23.5, -46.6);

        // Act & Assert
        var act = async () => await service.CacheGeocodeAsync("Rua A, 1", geocode);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ComputeSha256_ProducesDeterministicLowerCaseHexHash()
    {
        // Arrange
        var address1 = "  Praça da Sé, São Paulo  ";
        var address2 = "praça da sé, são paulo";

        // Act
        var hash1 = PlacesCacheService.ComputeSha256(address1);
        var hash2 = PlacesCacheService.ComputeSha256(address2);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task InterfaceAliases_InvokePrimaryMethodsCorrectly()
    {
        // Arrange
        var details = new GooglePlaceDetails("place_999", "Igreja Teste", "Endereço", -23.0, -46.0);
        var geocode = new GeocodeResult("Endereço", -23.0, -46.0);
        IPlacesCacheService service = new PlacesCacheService(_loggerMock.Object, _redisMock.Object);

        // Act
        await service.SetPlaceDetailsAsync("place_999", details);
        var fetchedPlace = await service.GetPlaceDetailsAsync("place_999");
        await service.SetGeocodeAsync("Endereço", geocode);
        var fetchedGeocode = await service.GetGeocodeAsync("Endereço");

        // Assert
        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "places:details:place_999"),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);

        _databaseMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == $"geo:address:{PlacesCacheService.ComputeSha256("Endereço")}"),
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
