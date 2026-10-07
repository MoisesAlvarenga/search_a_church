using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Features.Maps.Configurations;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;

namespace SearchAChurch.Api.Features.Maps.Gateways;

/// <summary>
/// HTTP Gateway for Google Maps Platform APIs (Nearby Search, Place Details, and Geocoding).
/// Features Circuit Breaker protection, 3-second timeout, and transparent 30-day cache-aside integration (AD-025, MAP-07).
/// </summary>
public class GooglePlacesGateway : IGooglePlacesGateway
{
    private const string CircuitBreakerOpenCode = "CIRCUIT_BREAKER_OPEN";
    private const string ExternalUnavailableCode = "EXTERNAL_PROVIDER_UNAVAILABLE";
    private const string OverQueryLimitCode = "OVER_QUERY_LIMIT";
    private const string RequestDeniedCode = "REQUEST_DENIED";
    private const string PlaceNotFoundCode = "PLACE_NOT_FOUND";
    private const string AddressNotFoundCode = "ADDRESS_NOT_FOUND";
    private const string InvalidArgumentCode = "INVALID_ARGUMENT";

    private readonly HttpClient _httpClient;
    private readonly IPlacesCacheService _cacheService;
    private readonly GoogleMapsOptions _options;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<GooglePlacesGateway> _logger;

    public GooglePlacesGateway(
        HttpClient httpClient,
        IPlacesCacheService cacheService,
        IOptions<GoogleMapsOptions> options,
        ILogger<GooglePlacesGateway> logger,
        ICircuitBreaker? circuitBreaker = null)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _options = options.Value;
        _logger = logger;
        _circuitBreaker = circuitBreaker ?? new CircuitBreaker(
            _options.CircuitBreakerFailureThreshold,
            TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds));
    }

    public async Task<Result<IReadOnlyList<GooglePlaceResult>>> SearchNearbyPlacesAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? query = null,
        CancellationToken ct = default)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180 || radiusMeters <= 0)
        {
            return Result<IReadOnlyList<GooglePlaceResult>>.Failure(
                InvalidArgumentCode,
                "Parâmetros de busca geográfica inválidos (coordenadas ou raio).");
        }

        if (!_circuitBreaker.CanExecute())
        {
            _logger.LogWarning("Circuit breaker is OPEN. Fast-failing SearchNearbyPlacesAsync.");
            return Result<IReadOnlyList<GooglePlaceResult>>.Failure(
                CircuitBreakerOpenCode,
                "Circuito aberto devido a falhas consecutivas na Google Maps Platform.");
        }

        var latStr = latitude.ToString(CultureInfo.InvariantCulture);
        var lngStr = longitude.ToString(CultureInfo.InvariantCulture);
        var radStr = radiusMeters.ToString(CultureInfo.InvariantCulture);
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        var url = $"{baseUrl}/place/nearbysearch/json?location={latStr},{lngStr}&radius={radStr}&type=church&key={_options.ApiKey}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"&keyword={Uri.EscapeDataString(query.Trim())}";
        }

        var httpResult = await ExecuteGetAsync(url, ct);
        if (httpResult.IsFailure)
        {
            return Result<IReadOnlyList<GooglePlaceResult>>.Failure(httpResult.ErrorCode!, httpResult.ErrorMessage!);
        }

        return ParseNearbySearch(httpResult.Value!);
    }

    public async Task<Result<GooglePlaceDetails>> GetPlaceDetailsAsync(
        string placeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return Result<GooglePlaceDetails>.Failure(
                InvalidArgumentCode,
                "O placeId fornecido é inválido ou vazio.");
        }

        var normalizedPlaceId = placeId.Trim();

        // 1. Check cache first (Cache-Aside pattern, AD-025)
        var cached = await _cacheService.GetCachedPlaceAsync(normalizedPlaceId, ct);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for Google Place Details {PlaceId}.", normalizedPlaceId);
            return Result<GooglePlaceDetails>.Success(cached);
        }

        // 2. Circuit Breaker protection
        if (!_circuitBreaker.CanExecute())
        {
            _logger.LogWarning("Circuit breaker is OPEN. Fast-failing GetPlaceDetailsAsync for {PlaceId}.", normalizedPlaceId);
            return Result<GooglePlaceDetails>.Failure(
                CircuitBreakerOpenCode,
                "Circuito aberto devido a falhas consecutivas na Google Maps Platform.");
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        const string fields = "place_id,name,formatted_address,geometry,formatted_phone_number,website,rating,user_ratings_total,opening_hours,photos";
        var url = $"{baseUrl}/place/details/json?place_id={Uri.EscapeDataString(normalizedPlaceId)}&fields={fields}&key={_options.ApiKey}";

        var httpResult = await ExecuteGetAsync(url, ct);
        if (httpResult.IsFailure)
        {
            return Result<GooglePlaceDetails>.Failure(httpResult.ErrorCode!, httpResult.ErrorMessage!);
        }

        var parseResult = ParsePlaceDetails(httpResult.Value!, normalizedPlaceId);
        if (parseResult.IsSuccess && parseResult.Value != null)
        {
            // Cache details for 30 days
            await _cacheService.CachePlaceAsync(normalizedPlaceId, parseResult.Value, ct: ct);
        }

        return parseResult;
    }

    public async Task<Result<GeocodeResult>> GeocodeAddressAsync(
        string address,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Result<GeocodeResult>.Failure(
                InvalidArgumentCode,
                "O endereço fornecido é inválido ou vazio.");
        }

        var normalizedAddress = address.Trim();

        // 1. Check cache first (Cache-Aside pattern, AD-025)
        var cached = await _cacheService.GetCachedGeocodeAsync(normalizedAddress, ct);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for Geocode address.");
            return Result<GeocodeResult>.Success(cached);
        }

        // 2. Circuit Breaker protection
        if (!_circuitBreaker.CanExecute())
        {
            _logger.LogWarning("Circuit breaker is OPEN. Fast-failing GeocodeAddressAsync.");
            return Result<GeocodeResult>.Failure(
                CircuitBreakerOpenCode,
                "Circuito aberto devido a falhas consecutivas na Google Maps Platform.");
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/geocode/json?address={Uri.EscapeDataString(normalizedAddress)}&key={_options.ApiKey}";

        var httpResult = await ExecuteGetAsync(url, ct);
        if (httpResult.IsFailure)
        {
            return Result<GeocodeResult>.Failure(httpResult.ErrorCode!, httpResult.ErrorMessage!);
        }

        var parseResult = ParseGeocode(httpResult.Value!, normalizedAddress);
        if (parseResult.IsSuccess && parseResult.Value != null)
        {
            // Cache geocode result for 30 days
            await _cacheService.CacheGeocodeAsync(normalizedAddress, parseResult.Value, ct: ct);
        }

        return parseResult;
    }

    private async Task<Result<string>> ExecuteGetAsync(string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var response = await _httpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                {
                    _circuitBreaker.RecordFailure();
                }

                _logger.LogWarning(
                    "Google Maps API HTTP request failed with status {StatusCode}",
                    response.StatusCode);

                return Result<string>.Failure(
                    ExternalUnavailableCode,
                    $"A Google Maps Platform retornou erro HTTP {(int)response.StatusCode}.");
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            return Result<string>.Success(content);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "Google Maps API request timed out after {TimeoutSeconds}s.", _options.TimeoutSeconds);
            return Result<string>.Failure(ExternalUnavailableCode, "A chamada à Google Maps Platform excedeu o tempo limite de 3 segundos.");
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "Unexpected error communicating with Google Maps Platform.");
            return Result<string>.Failure(ExternalUnavailableCode, "Falha de rede ou conectividade com a Google Maps Platform.");
        }
    }

    private Result<IReadOnlyList<GooglePlaceResult>> ParseNearbySearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : "UNKNOWN";

        return status switch
        {
            "OK" => HandleSearchOk(root),
            "ZERO_RESULTS" => HandleSearchZeroResults(),
            OverQueryLimitCode => HandleOverQueryLimit<IReadOnlyList<GooglePlaceResult>>(),
            RequestDeniedCode => Result<IReadOnlyList<GooglePlaceResult>>.Failure(RequestDeniedCode, "Acesso à Google Maps Platform negado."),
            _ => HandleGenericError<IReadOnlyList<GooglePlaceResult>>(status)
        };
    }

    private Result<IReadOnlyList<GooglePlaceResult>> HandleSearchOk(JsonElement root)
    {
        _circuitBreaker.RecordSuccess();
        var results = new List<GooglePlaceResult>();

        if (root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsElement.EnumerateArray())
            {
                var place = ParseSingleSearchResult(item);
                if (place != null)
                {
                    results.Add(place);
                }
            }
        }

        return Result<IReadOnlyList<GooglePlaceResult>>.Success(results.AsReadOnly());
    }

    private static GooglePlaceResult? ParseSingleSearchResult(JsonElement item)
    {
        var placeId = item.TryGetProperty("place_id", out var pid) ? pid.GetString() : null;
        var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var address = ExtractAddress(item);
        var location = item.GetProperty("geometry").GetProperty("location");
        var lat = location.GetProperty("lat").GetDouble();
        var lng = location.GetProperty("lng").GetDouble();

        var rating = item.TryGetProperty("rating", out var r) ? r.GetDouble() : (double?)null;
        var userRatingsTotal = item.TryGetProperty("user_ratings_total", out var urt) ? urt.GetInt32() : (int?)null;

        return new GooglePlaceResult(placeId, name, address, lat, lng, rating, userRatingsTotal);
    }

    private static string? ExtractAddress(JsonElement item)
    {
        if (item.TryGetProperty("vicinity", out var vic) && vic.ValueKind == JsonValueKind.String)
        {
            return vic.GetString();
        }

        if (item.TryGetProperty("formatted_address", out var fa) && fa.ValueKind == JsonValueKind.String)
        {
            return fa.GetString();
        }

        return null;
    }

    private Result<IReadOnlyList<GooglePlaceResult>> HandleSearchZeroResults()
    {
        _circuitBreaker.RecordSuccess();
        return Result<IReadOnlyList<GooglePlaceResult>>.Success(Array.Empty<GooglePlaceResult>());
    }

    private Result<GooglePlaceDetails> ParsePlaceDetails(string json, string requestedPlaceId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : "UNKNOWN";

        return status switch
        {
            "OK" => HandlePlaceDetailsOk(root, requestedPlaceId),
            "NOT_FOUND" or "ZERO_RESULTS" => Result<GooglePlaceDetails>.Failure(PlaceNotFoundCode, "Local não encontrado na Google Places API."),
            OverQueryLimitCode => HandleOverQueryLimit<GooglePlaceDetails>(),
            RequestDeniedCode => Result<GooglePlaceDetails>.Failure(RequestDeniedCode, "Acesso à Google Maps Platform negado."),
            _ => HandleGenericError<GooglePlaceDetails>(status)
        };
    }

    private Result<GooglePlaceDetails> HandlePlaceDetailsOk(JsonElement root, string requestedPlaceId)
    {
        _circuitBreaker.RecordSuccess();
        if (!root.TryGetProperty("result", out var resultElement))
        {
            return Result<GooglePlaceDetails>.Failure(PlaceNotFoundCode, "Dados de detalhes do local não encontrados.");
        }

        var placeId = resultElement.TryGetProperty("place_id", out var pid) ? pid.GetString() ?? requestedPlaceId : requestedPlaceId;
        var name = resultElement.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var formattedAddress = resultElement.TryGetProperty("formatted_address", out var fa) ? fa.GetString() : null;

        var location = resultElement.GetProperty("geometry").GetProperty("location");
        var lat = location.GetProperty("lat").GetDouble();
        var lng = location.GetProperty("lng").GetDouble();

        var phoneNumber = resultElement.TryGetProperty("formatted_phone_number", out var pn) ? pn.GetString() : null;
        var website = resultElement.TryGetProperty("website", out var w) ? w.GetString() : null;
        var rating = resultElement.TryGetProperty("rating", out var r) ? r.GetDouble() : (double?)null;
        var userRatingsTotal = resultElement.TryGetProperty("user_ratings_total", out var urt) ? urt.GetInt32() : (int?)null;

        var openingHours = ExtractOpeningHours(resultElement);
        var photoUrls = ExtractPhotoReferences(resultElement);

        var details = new GooglePlaceDetails(
            placeId,
            name,
            formattedAddress,
            lat,
            lng,
            phoneNumber,
            website,
            rating,
            userRatingsTotal,
            openingHours?.AsReadOnly(),
            photoUrls?.AsReadOnly());

        return Result<GooglePlaceDetails>.Success(details);
    }

    private static List<string>? ExtractOpeningHours(JsonElement resultElement)
    {
        if (resultElement.TryGetProperty("opening_hours", out var oh) &&
            oh.TryGetProperty("weekday_text", out var wt) &&
            wt.ValueKind == JsonValueKind.Array)
        {
            return wt.EnumerateArray().Select(d => d.GetString() ?? string.Empty).ToList();
        }

        return null;
    }

    private static List<string>? ExtractPhotoReferences(JsonElement resultElement)
    {
        if (resultElement.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array)
        {
            return photos.EnumerateArray()
                .Where(p => p.TryGetProperty("photo_reference", out _))
                .Select(p => p.GetProperty("photo_reference").GetString() ?? string.Empty)
                .ToList();
        }

        return null;
    }

    private Result<GeocodeResult> ParseGeocode(string json, string requestedAddress)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : "UNKNOWN";

        return status switch
        {
            "OK" => HandleGeocodeOk(root, requestedAddress),
            "ZERO_RESULTS" => Result<GeocodeResult>.Failure(AddressNotFoundCode, "Nenhum resultado geográfico encontrado para o endereço informado."),
            OverQueryLimitCode => HandleOverQueryLimit<GeocodeResult>(),
            RequestDeniedCode => Result<GeocodeResult>.Failure(RequestDeniedCode, "Acesso à Google Maps Platform negado."),
            _ => HandleGenericError<GeocodeResult>(status)
        };
    }

    private Result<GeocodeResult> HandleGeocodeOk(JsonElement root, string requestedAddress)
    {
        _circuitBreaker.RecordSuccess();
        if (root.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array &&
            results.GetArrayLength() > 0)
        {
            var first = results[0];
            var formattedAddress = first.TryGetProperty("formatted_address", out var fa)
                ? fa.GetString() ?? requestedAddress
                : requestedAddress;

            var location = first.GetProperty("geometry").GetProperty("location");
            var lat = location.GetProperty("lat").GetDouble();
            var lng = location.GetProperty("lng").GetDouble();

            return Result<GeocodeResult>.Success(new GeocodeResult(formattedAddress, lat, lng));
        }

        return Result<GeocodeResult>.Failure(AddressNotFoundCode, "Nenhum resultado geográfico retornado pela Google Maps Platform.");
    }

    private Result<T> HandleOverQueryLimit<T>()
    {
        _circuitBreaker.RecordFailure();
        _logger.LogWarning("Google Maps API quota exceeded (OVER_QUERY_LIMIT).");
        return Result<T>.Failure(OverQueryLimitCode, "Cota da Google Maps Platform excedida.");
    }

    private Result<T> HandleGenericError<T>(string? status)
    {
        _logger.LogWarning("Google Maps API returned error status: {Status}", status);
        return Result<T>.Failure("GOOGLE_MAPS_ERROR", $"Erro retornado pela Google Maps Platform: {status}.");
    }
}
