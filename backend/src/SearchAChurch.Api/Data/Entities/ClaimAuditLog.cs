namespace SearchAChurch.Api.Data.Entities;

/// <summary>
/// Trilha de auditoria append-only para o ciclo de vida de reivindicação e contestações (AD-014).
/// Conforme com o art. 15 da Lei Federal nº 12.965/2014 (Marco Civil da Internet) e Lei nº 13.709/2018 (LGPD).
/// </summary>
public class ClaimAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tipo de evento registrado (ClaimInitiated, EvidenceSubmitted, ClaimApproved, DisputeOpened, ClaimRevoked).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public Guid ChurchId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>
    /// Endereço IP do solicitante (IPv4 ou IPv6 público).
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// Porta lógica TCP de origem da conexão cliente-servidor.
    /// </summary>
    public int ClientPort { get; set; }

    /// <summary>
    /// Timestamp exato no padrão ISO 8601 UTC.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Identificador do browser, sistema operacional, dispositivo e versão do app.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Objeto estruturado com metadados complementares (hashes documentais, versão ToS, tokens).
    /// </summary>
    public string VerificationMetadata { get; set; } = "{}";

    /// <summary>
    /// Prazo obrigatório de retenção de 180 dias (6 meses).
    /// </summary>
    public DateTimeOffset RetentionUntil { get; set; } = DateTimeOffset.UtcNow.AddDays(180);

    // Navegações opcionais
    public Church? Church { get; set; }
    public User? User { get; set; }
}
