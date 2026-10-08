namespace SearchAChurch.Api.Data.Entities;

/// <summary>
/// Evidência comprobatória vinculada a um processo de reivindicação.
/// </summary>
public class ClaimEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClaimId { get; set; }
    public ChurchClaim Claim { get; set; } = null!;

    public EvidenceType EvidenceType { get; set; }

    /// <summary>
    /// Conteúdo bruto da evidência (URL de arquivo, token de bio, e-mail institucional).
    /// </summary>
    public string? RawDataOrUrl { get; set; }

    /// <summary>
    /// Hash criptográfico SHA-256 do documento cartorial enviado (Ata de Posse / Estatuto).
    /// </summary>
    public string? FileHashSha256 { get; set; }

    /// <summary>
    /// Metadados estruturados em formato JSON (accuracy, distância calculada, dados de verificação).
    /// </summary>
    public string Metadata { get; set; } = "{}";

    public bool IsApproved { get; set; } = false;

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
}
