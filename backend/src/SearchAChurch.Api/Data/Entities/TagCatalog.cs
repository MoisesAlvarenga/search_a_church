namespace SearchAChurch.Api.Data.Entities;

public class TagCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Código normalizado em snake_case único (ex: rampa_acesso, interprete_libras).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TagCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Identificador do ícone Material para rendering mobile (ex: accessible, child_care).
    /// </summary>
    public string IconName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation collections
    public ICollection<UserProfileTag> UserProfileTags { get; set; } = new List<UserProfileTag>();
    public ICollection<ChurchTag> ChurchTags { get; set; } = new List<ChurchTag>();
}
