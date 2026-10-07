using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Maps.Gateways;
using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Orquestrador de busca híbrida de congregações no mapa.
/// Coordena consultas paralelas no banco PostgreSQL e Google Places, aplicando
/// motor de deduplicação App-First e degradação graciosa em cenários de indisponibilidade externa.
/// </summary>
public class MapOrchestratorService : IMapOrchestratorService
{
    private const string FallbackDegradedMessage = "Provedor externo indisponível no momento. Exibindo apenas templos da base local.";
    private const string InvalidCoordinatesCode = "INVALID_COORDINATES";

    private readonly AppDbContext _dbContext;
    private readonly IGooglePlacesGateway _googlePlacesGateway;
    private readonly IDeduplicationEngine _deduplicationEngine;
    private readonly ILogger<MapOrchestratorService> _logger;

    public MapOrchestratorService(
        AppDbContext dbContext,
        IGooglePlacesGateway googlePlacesGateway,
        IDeduplicationEngine deduplicationEngine,
        ILogger<MapOrchestratorService> logger)
    {
        _dbContext = dbContext;
        _googlePlacesGateway = googlePlacesGateway;
        _deduplicationEngine = deduplicationEngine;
        _logger = logger;
    }

    public async Task<Result<MapSearchResponse>> SearchAsync(MapSearchRequest request, CancellationToken ct = default)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var appliedRadiusKm = Math.Clamp(request.RadiusKm <= 0 ? 5.0 : request.RadiusKm, 0.1, 50.0);

        // 1. Execução paralela: consulta banco de dados local e API externa Google Places
        var localTask = QueryLocalChurchesAsync(request.Latitude, request.Longitude, appliedRadiusKm, request.Query, ct);
        var externalTask = FetchGooglePlacesAsync(request.Latitude, request.Longitude, appliedRadiusKm, request.Query, ct);

        await Task.WhenAll(localTask, externalTask);

        var localChurches = await localTask;
        var (googlePlaces, isDegraded, degradedMessage) = await externalTask;

        // 2. Unificação e deduplicação App-First
        var deduplicated = _deduplicationEngine.Deduplicate(
            localChurches,
            googlePlaces,
            request.Latitude,
            request.Longitude);

        // 3. Filtro de segurança por raio aplicado
        var resultsInRadius = deduplicated
            .Where(item => item.DistanceKm <= appliedRadiusKm)
            .ToList()
            .AsReadOnly();

        var response = new MapSearchResponse(
            Results: resultsInRadius,
            CenterLatitude: request.Latitude,
            CenterLongitude: request.Longitude,
            AppliedRadiusKm: appliedRadiusKm,
            IsDegraded: isDegraded,
            DegradedMessage: degradedMessage
        );

        return Result<MapSearchResponse>.Success(response);
    }

    private static Result<MapSearchResponse>? ValidateRequest(MapSearchRequest request)
    {
        if (request.Latitude is < -90.0 or > 90.0)
        {
            return Result<MapSearchResponse>.Failure(InvalidCoordinatesCode, "Latitude deve estar entre -90 e 90 graus.");
        }

        if (request.Longitude is < -180.0 or > 180.0)
        {
            return Result<MapSearchResponse>.Failure(InvalidCoordinatesCode, "Longitude deve estar entre -180 e 180 graus.");
        }

        return null;
    }

    private async Task<IReadOnlyList<Church>> QueryLocalChurchesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        string? query,
        CancellationToken ct)
    {
        // 1 grau lat ~ 111 km. Limites de bounding box para uso do índice espacial
        var latDelta = radiusKm / 111.0;
        var cosLat = Math.Cos(latitude * Math.PI / 180.0);
        var lngDelta = cosLat > 0.0001 ? radiusKm / (111.0 * cosLat) : radiusKm / 111.0;

        var minLat = latitude - latDelta;
        var maxLat = latitude + latDelta;
        var minLng = longitude - lngDelta;
        var maxLng = longitude + lngDelta;

        var queryable = _dbContext.Churches.AsNoTracking()
            .Where(c => c.Latitude >= minLat && c.Latitude <= maxLat &&
                        c.Longitude >= minLng && c.Longitude <= maxLng);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim();
            queryable = queryable.Where(c => c.Name.Contains(normalized) || c.FormattedAddress.Contains(normalized));
        }

        var candidates = await queryable.ToListAsync(ct);

        return candidates
            .Where(c => DeduplicationEngine.CalculateHaversineDistanceKm(latitude, longitude, c.Latitude, c.Longitude) <= radiusKm)
            .ToList();
    }

    private async Task<(IReadOnlyList<GooglePlaceResult> Places, bool IsDegraded, string? DegradedMessage)> FetchGooglePlacesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        string? query,
        CancellationToken ct)
    {
        try
        {
            var radiusMeters = Math.Min(radiusKm * 1000.0, 50000.0);
            var result = await _googlePlacesGateway.SearchNearbyPlacesAsync(latitude, longitude, radiusMeters, query, ct);

            if (result.IsSuccess && result.Value != null)
            {
                return (result.Value, false, null);
            }

            _logger.LogWarning("Busca externa no Google Places falhou com código {Code}: {Message}. Degradação graciosa ativada.",
                result.ErrorCode, result.ErrorMessage);

            return (Array.Empty<GooglePlaceResult>(), true, FallbackDegradedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exceção inesperada na busca ao Google Places. Degradação graciosa ativada.");
            return (Array.Empty<GooglePlaceResult>(), true, FallbackDegradedMessage);
        }
    }
}
