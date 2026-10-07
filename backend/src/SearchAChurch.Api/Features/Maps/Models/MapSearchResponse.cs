namespace SearchAChurch.Api.Features.Maps.Models;

/// <summary>
/// Resposta estruturada da busca híbrida georreferenciada no mapa.
/// </summary>
public record MapSearchResponse(
    IReadOnlyList<ChurchMapItemDto> Results,
    double CenterLatitude,
    double CenterLongitude,
    double AppliedRadiusKm,
    bool IsDegraded,
    string? DegradedMessage = null
);
