namespace SearchAChurch.Api.Data.Entities;

/// <summary>
/// Representa uma solicitação de reivindicação de perfil de igreja (AD-005, AD-015).
/// </summary>
public class ChurchClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChurchId { get; set; }
    public Church Church { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ClaimRecordStatus Status { get; set; } = ClaimRecordStatus.Pending;
    public VerificationTier TargetTier { get; set; } = VerificationTier.Tier3_SocialPresencial;
    public ValidationMethod ValidationMethod { get; set; } = ValidationMethod.Geofence;

    /// <summary>
    /// Aceite explícito de declaração sob o art. 299 CP e enquadramento como Provedora de Aplicação (AD-013).
    /// </summary>
    public bool TosAccepted { get; set; }
    public string TosVersion { get; set; } = "1.0";
    public DateTimeOffset? TosAcceptedAt { get; set; }

    /// <summary>
    /// Prazo fatal de timeout / TTL (7 dias corridos para fluxo documental ou 48 horas para fluxo social) - AD-015.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Registro de notificação preventiva disparada com 24h de antecedência do timeout.
    /// </summary>
    public DateTimeOffset? ReminderSentAt { get; set; }

    public int AttemptCount { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ClaimEvidence> Evidences { get; set; } = new List<ClaimEvidence>();
}
