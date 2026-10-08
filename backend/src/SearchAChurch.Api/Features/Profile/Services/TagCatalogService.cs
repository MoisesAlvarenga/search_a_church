using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;

namespace SearchAChurch.Api.Features.Profile.Services;

/// <summary>
/// Serviço responsável pelo catálogo oficial de tags do sistema bidirecional.
/// Gerencia agrupamento por categorias, cache com TTL de 24 horas e validação estrita de vocabulário.
/// </summary>
public class TagCatalogService : ITagCatalogService
{
    public const string CacheKey = "tags:catalog:active";
    public static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<TagCatalogService> _logger;

    public TagCatalogService(
        AppDbContext dbContext,
        ILogger<TagCatalogService> logger,
        IDistributedCache? cache = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
    }

    public async Task<TagCatalogResponse> GetActiveCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            try
            {
                var cached = await _cache.GetStringAsync(CacheKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var deserialized = JsonSerializer.Deserialize<TagCatalogResponse>(cached, SerializerOptions);
                    if (deserialized != null)
                    {
                        return deserialized;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve tag catalog from cache. Falling back to database.");
            }
        }

        var activeTags = await _dbContext.TagCatalogs
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync(cancellationToken);

        var categories = Enum.GetValues<TagCategory>()
            .OrderBy(c => (int)c)
            .Select(cat =>
            {
                var tagsInCat = activeTags
                    .Where(t => t.Category == cat)
                    .Select(t => new TagItemDto(
                        t.Code,
                        t.Name,
                        t.Description,
                        t.IconName))
                    .ToList();

                return new TagCategoryDto(
                    (int)cat,
                    GetCategoryDisplayName(cat),
                    tagsInCat);
            })
            .ToList();

        var response = new TagCatalogResponse(categories);

        if (_cache != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(response, SerializerOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                };
                await _cache.SetStringAsync(CacheKey, json, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache tag catalog in distributed cache.");
            }
        }

        return response;
    }

    public async Task<IReadOnlyList<string>> ValidateTagCodesAsync(IEnumerable<string> tagCodes, CancellationToken cancellationToken = default)
    {
        if (tagCodes == null)
        {
            return Array.Empty<string>();
        }

        var list = tagCodes.ToList();
        if (list.Count == 0)
        {
            return Array.Empty<string>();
        }

        var catalog = await GetActiveCatalogAsync(cancellationToken);
        var activeCodes = catalog.Categories
            .SelectMany(c => c.Tags)
            .Select(t => t.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidCodes = new List<string>();
        foreach (var code in list)
        {
            var trimmed = code?.Trim();
            if (string.IsNullOrEmpty(trimmed) || !activeCodes.Contains(trimmed))
            {
                invalidCodes.Add(code ?? string.Empty);
            }
        }

        return invalidCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            try
            {
                await _cache.RemoveAsync(CacheKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate tag catalog cache key {Key}.", CacheKey);
            }
        }
    }

    public static string GetCategoryDisplayName(TagCategory category) => category switch
    {
        TagCategory.Accessibility => "Acessibilidade",
        TagCategory.Infrastructure => "Infraestrutura",
        TagCategory.Ministries => "Ministérios",
        _ => category.ToString()
    };
}
