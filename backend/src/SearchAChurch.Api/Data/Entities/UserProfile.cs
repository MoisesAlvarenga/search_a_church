namespace SearchAChurch.Api.Data.Entities;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Chave estrangeira 1:1 única vinculada à conta do usuário.
    /// </summary>
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Denominação ou linha teológica preferida (ex: Batista, Presbiteriana, Assembleia de Deus).
    /// </summary>
    public string? Denomination { get; set; }

    /// <summary>
    /// Estilo de liturgia preferido (ex: Contemporâneo, Tradicional, Pentecostal).
    /// </summary>
    public string? WorshipStyle { get; set; }

    /// <summary>
    /// Códigos ISO dos idiomas de culto desejados (ex: ["pt", "en"]).
    /// </summary>
    public List<string> PreferredLanguages { get; set; } = new List<string> { "pt" };

    /// <summary>
    /// Raio padrão de busca em quilômetros (padrão: 10.0, permitido de 1.0 a 100.0).
    /// </summary>
    public double DefaultRadiusKm { get; set; } = 10.0;

    /// <summary>
    /// Flag indicando se a conta sofreu soft delete com anonimização cadastral (LGPD).
    /// </summary>
    public bool IsAnonymous { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Soft Delete para conformidade com a LGPD (Lei nº 13.709/2018).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation collections
    public ICollection<UserProfileTag> UserProfileTags { get; set; } = new List<UserProfileTag>();
}
