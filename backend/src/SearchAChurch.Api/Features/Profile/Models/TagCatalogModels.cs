namespace SearchAChurch.Api.Features.Profile.Models;

public record TagItemDto(
    string Code,
    string Name,
    string Description,
    string IconName
);

public record TagCategoryDto(
    int Id,
    string Name,
    IReadOnlyList<TagItemDto> Tags
);

public record TagCatalogResponse(
    IReadOnlyList<TagCategoryDto> Categories
);
