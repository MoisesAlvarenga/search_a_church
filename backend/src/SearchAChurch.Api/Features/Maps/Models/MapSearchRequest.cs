namespace SearchAChurch.Api.Features.Maps.Models;

/// <summary>
/// Parâmetros de pesquisa geográfica de igrejas no mapa.
/// </summary>
public record MapSearchRequest(
    double Latitude,
    double Longitude,
    double RadiusKm = 5.0,
    string? Query = null
);
