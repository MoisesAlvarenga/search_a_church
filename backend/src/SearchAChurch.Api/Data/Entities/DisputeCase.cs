namespace SearchAChurch.Api.Data.Entities;

/// <summary>
/// Registro de contestação de titularidade / litígio paritário (AD-012, AD-016).
/// </summary>
public class DisputeCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChurchId { get; set; }
    public Church Church { get; set; } = null!;

    /// <summary>
    /// Solicitante que abriu a contestação munido de documento de Nível 1.
    /// </summary>
    public Guid ChallengerUserId { get; set; }
    public User ChallengerUser { get; set; } = null!;

    /// <summary>
    /// Gestor incumbente anterior cuja titularidade está sendo contestada.
    /// </summary>
    public Guid? IncumbentUserId { get; set; }
    public User? IncumbentUser { get; set; }

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Prazo improrrogável de 5 dias úteis para juntada de certidão do RCPJ (AD-016).
    /// </summary>
    public DateTimeOffset DeadlineAt { get; set; }

    public string? ChallengerDocumentHash { get; set; }
    public string? IncumbentDocumentHash { get; set; }

    public DateTimeOffset? ChallengerAverbationDate { get; set; }
    public DateTimeOffset? IncumbentAverbationDate { get; set; }

    public string? ResolutionNotes { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
