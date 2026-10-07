using SearchAChurch.Api.Common;
using SearchAChurch.Api.Features.Maps.Models;

namespace SearchAChurch.Api.Features.Maps.Services;

/// <summary>
/// Contrato do serviço orquestrador de descoberta híbrida de congregações no mapa.
/// </summary>
public interface IMapOrchestratorService
{
    Task<Result<MapSearchResponse>> SearchAsync(MapSearchRequest request, CancellationToken ct = default);
}
