namespace SearchAChurch.Api.Features.Profile.Services;

using SearchAChurch.Api.Features.Profile.Models;

public interface ITagCatalogService
{
    /// <summary>
    /// Consulta o catálogo oficial de tags ativas agrupadas em categorias, com cache Redis de 24 horas.
    /// </summary>
    Task<TagCatalogResponse> GetActiveCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida uma coleção de códigos de tags contra o catálogo ativo.
    /// Retorna a lista de códigos desconhecidos ou inativos. Se todos forem válidos, retorna vazio.
    /// </summary>
    Task<IReadOnlyList<string>> ValidateTagCodesAsync(IEnumerable<string> tagCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida o cache do catálogo de tags no Redis.
    /// </summary>
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
