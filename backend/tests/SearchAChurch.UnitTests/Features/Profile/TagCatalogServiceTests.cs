using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;
using SearchAChurch.Api.Features.Profile.Services;

namespace SearchAChurch.UnitTests.Features.Profile;

[Trait("Category", "Unit")]
public class TagCatalogServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static IDistributedCache CreateRealDistributedCache()
    {
        return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }

    private static ILogger<TagCatalogService> CreateLogger()
    {
        return new Mock<ILogger<TagCatalogService>>().Object;
    }

    [Fact]
    public async Task GetActiveCatalogAsync_WhenCacheMiss_ShouldQueryDbGroupCategoriesAndPopulateCache()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        var response = await service.GetActiveCatalogAsync();

        // Assert
        response.Should().NotBeNull();
        response.Categories.Should().HaveCount(3);

        var accCat = response.Categories.First(c => c.Id == 1);
        accCat.Name.Should().Be("Acessibilidade");
        accCat.Tags.Should().HaveCount(5);
        accCat.Tags.Select(t => t.Code).Should().Contain("rampa_acesso");

        var infraCat = response.Categories.First(c => c.Id == 2);
        infraCat.Name.Should().Be("Infraestrutura");
        infraCat.Tags.Should().HaveCount(5);
        infraCat.Tags.Select(t => t.Code).Should().Contain("ar_condicionado");

        var minCat = response.Categories.First(c => c.Id == 3);
        minCat.Name.Should().Be("Ministérios");
        minCat.Tags.Should().HaveCount(5);
        minCat.Tags.Select(t => t.Code).Should().Contain("ministerio_jovens");

        // Verify cache was populated
        var cachedString = await cache.GetStringAsync(TagCatalogService.CacheKey);
        cachedString.Should().NotBeNullOrWhiteSpace();
        cachedString.Should().Contain("rampa_acesso");
    }

    [Fact]
    public async Task GetActiveCatalogAsync_WhenCacheHit_ShouldReturnCachedDataDirectly()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        var cachedResponse = new TagCatalogResponse(new List<TagCategoryDto>
        {
            new TagCategoryDto(1, "Acessibilidade", new List<TagItemDto>
            {
                new TagItemDto("cached_tag", "Cached Name", "Desc", "icon")
            })
        });
        var json = JsonSerializer.Serialize(cachedResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await cache.SetStringAsync(TagCatalogService.CacheKey, json);

        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        var result = await service.GetActiveCatalogAsync();

        // Assert
        result.Should().NotBeNull();
        result.Categories.Should().HaveCount(1);
        result.Categories[0].Tags[0].Code.Should().Be("cached_tag");
    }

    [Fact]
    public async Task GetActiveCatalogAsync_WhenCacheThrows_ShouldGracefullyFallbackToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        var failingCacheMock = new Mock<IDistributedCache>();
        failingCacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection timed out"));

        var service = new TagCatalogService(context, CreateLogger(), failingCacheMock.Object);

        // Act
        var response = await service.GetActiveCatalogAsync();

        // Assert
        response.Should().NotBeNull();
        response.Categories.Should().HaveCount(3);
        response.Categories.SelectMany(c => c.Tags).Should().HaveCount(15);
    }

    [Fact]
    public async Task GetActiveCatalogAsync_ShouldOnlyReturnActiveTagsAndExcludeInactive()
    {
        // Arrange
        using var context = CreateContext();
        var inactiveTag = new TagCatalog
        {
            Code = "tag_desativada",
            Name = "Tag Desativada",
            Category = TagCategory.Accessibility,
            Description = "Inativa",
            IconName = "block",
            IsActive = false,
            DisplayOrder = 99
        };
        context.TagCatalogs.Add(inactiveTag);
        await context.SaveChangesAsync();

        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        var response = await service.GetActiveCatalogAsync();

        // Assert
        var allCodes = response.Categories.SelectMany(c => c.Tags).Select(t => t.Code).ToList();
        allCodes.Should().NotContain("tag_desativada");
    }

    [Fact]
    public async Task ValidateTagCodesAsync_WhenAllCodesValid_ShouldReturnEmptyList()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        var input = new[] { "rampa_acesso", "estacionamento_proprio", "ministerio_casais" };

        // Act
        var invalidCodes = await service.ValidateTagCodesAsync(input);

        // Assert
        invalidCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateTagCodesAsync_WhenContainsInvalidCodes_ShouldReturnUnknownCodes()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        var input = new[] { "rampa_acesso", "codigo_fantasma", "tag_inventada", "escola_biblica" };

        // Act
        var invalidCodes = await service.ValidateTagCodesAsync(input);

        // Assert
        invalidCodes.Should().HaveCount(2);
        invalidCodes.Should().Contain(new[] { "codigo_fantasma", "tag_inventada" });
    }

    [Fact]
    public async Task ValidateTagCodesAsync_WhenCodeBelongsToInactiveTag_ShouldReturnItAsInvalid()
    {
        // Arrange
        using var context = CreateContext();
        var inactiveTag = new TagCatalog
        {
            Code = "tag_inativa_recurso",
            Name = "Recurso Desativado",
            Category = TagCategory.Infrastructure,
            Description = "Não mais disponível",
            IconName = "cancel",
            IsActive = false
        };
        context.TagCatalogs.Add(inactiveTag);
        await context.SaveChangesAsync();

        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        var invalidCodes = await service.ValidateTagCodesAsync(new[] { "rampa_acesso", "tag_inativa_recurso" });

        // Assert
        invalidCodes.Should().ContainSingle().Which.Should().Be("tag_inativa_recurso");
    }

    [Fact]
    public async Task ValidateTagCodesAsync_WhenEmptyOrNull_ShouldReturnEmptyList()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        var resNull = await service.ValidateTagCodesAsync(null!);
        var resEmpty = await service.ValidateTagCodesAsync(Array.Empty<string>());

        // Assert
        resNull.Should().BeEmpty();
        resEmpty.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidateCacheAsync_ShouldRemoveKeyFromCache()
    {
        // Arrange
        using var context = CreateContext();
        var cache = CreateRealDistributedCache();
        await cache.SetStringAsync(TagCatalogService.CacheKey, "dummy_value");

        var service = new TagCatalogService(context, CreateLogger(), cache);

        // Act
        await service.InvalidateCacheAsync();

        // Assert
        var remaining = await cache.GetStringAsync(TagCatalogService.CacheKey);
        remaining.Should().BeNull();
    }
}
