using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SearchAChurch.Api.Features.Maps.Gateways;
using SearchAChurch.Api.Features.Maps.Models;
using SearchAChurch.Api.Features.Maps.Services;

namespace SearchAChurch.Api.Endpoints;

public static class MapEndpoints
{
    private const string InvalidCoordinatesError = "INVALID_COORDINATES";
    private const string PlaceNotFoundError = "PLACE_NOT_FOUND";
    private const string AddressNotFoundError = "ADDRESS_NOT_FOUND";

    public static RouteGroupBuilder MapMapEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/search", SearchMapAsync)
             .RequireAuthorization()
             .Produces<MapSearchResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status401Unauthorized)
             .WithName("SearchMap")
             .WithSummary("Realiza busca híbrida georreferenciada de igrejas no mapa")
             .WithOpenApi();

        group.MapGet("/places/{placeId}", GetPlaceDetailsAsync)
             .RequireAuthorization()
             .Produces<GooglePlaceDetails>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status401Unauthorized)
             .WithName("GetPlaceDetails")
             .WithSummary("Obtém detalhes de uma igreja externa do Google Places via cache")
             .WithOpenApi();

        group.MapPost("/geocode", GeocodeAddressAsync)
             .RequireAuthorization()
             .Produces<GeocodeResult>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status401Unauthorized)
             .WithName("GeocodeAddress")
             .WithSummary("Converte endereço textual em coordenadas geográficas")
             .WithOpenApi();

        return group;
    }

    private static async Task<IResult> SearchMapAsync(
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] double? radiusKm,
        [FromQuery] string? query,
        IMapOrchestratorService orchestrator,
        CancellationToken ct)
    {
        if (!lat.HasValue || !lng.HasValue)
        {
            return Results.BadRequest(new { error = InvalidCoordinatesError, message = "Parâmetros 'lat' e 'lng' são obrigatórios." });
        }

        if (lat.Value is < -90.0 or > 90.0 || lng.Value is < -180.0 or > 180.0)
        {
            return Results.BadRequest(new { error = InvalidCoordinatesError, message = "Coordenadas geográficas inválidas." });
        }

        if (radiusKm.HasValue && radiusKm.Value <= 0)
        {
            return Results.BadRequest(new { error = "INVALID_RADIUS", message = "O raio de busca deve ser maior que zero." });
        }

        var request = new MapSearchRequest(lat.Value, lng.Value, radiusKm ?? 5.0, query);
        var result = await orchestrator.SearchAsync(request, ct);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetPlaceDetailsAsync(
        [FromRoute] string placeId,
        IGooglePlacesGateway gateway,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return Results.BadRequest(new { error = "INVALID_PLACE_ID", message = "Identificador 'placeId' é obrigatório." });
        }

        var result = await gateway.GetPlaceDetailsAsync(placeId.Trim(), ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == PlaceNotFoundError)
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GeocodeAddressAsync(
        [FromBody] GeocodeRequest? request,
        IGooglePlacesGateway gateway,
        CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.BadRequest(new { error = "INVALID_ADDRESS", message = "O endereço é obrigatório para geocodificação." });
        }

        var result = await gateway.GeocodeAddressAsync(request.Address.Trim(), ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == AddressNotFoundError)
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }
}
