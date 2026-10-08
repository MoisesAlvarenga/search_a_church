namespace SearchAChurch.Api.Data.Entities;

public class Church
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Âncora determinística do Google Maps (AD-004 e AD-025).
    /// </summary>
    public string? PlaceId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string FormattedAddress { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Phone { get; set; }
    public string? Website { get; set; }

    /// <summary>
    /// Status do ciclo de vida da congregação no processo de reivindicação (AD-005, AD-015, AD-016).
    /// </summary>
    public ChurchClaimState ClaimStatus { get; set; } = ChurchClaimState.Unclaimed;

    /// <summary>
    /// Nível de validação concedido (AD-012, AD-013).
    /// </summary>
    public VerificationTier VerificationTier { get; set; } = VerificationTier.None;

    /// <summary>
    /// Indica se a congregação possui representante verificado (status Verified via church-profile-claim).
    /// </summary>
    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// Usuário representante verificado da congregação.
    /// </summary>
    public Guid? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }

    /// <summary>
    /// Data e hora da homologação da verificação.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Soft Delete para conformidade com a LGPD (Lei nº 13.709/2018).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation collections
    public ICollection<ChurchClaim> Claims { get; set; } = new List<ChurchClaim>();
    public ICollection<DisputeCase> DisputeCases { get; set; } = new List<DisputeCase>();
}
