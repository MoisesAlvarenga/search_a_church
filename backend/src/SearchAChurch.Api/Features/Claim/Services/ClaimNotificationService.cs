using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Serviço de disparo de notificações para o ciclo de vida de reivindicação e disputas (AD-012, AD-015, AD-016).
/// </summary>
public interface IClaimNotificationService
{
    Task NotifyRevocationAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        string reason,
        CancellationToken cancellationToken = default);

    Task NotifyDisputeOpenedAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        DateTimeOffset deadlineAt,
        bool isChallenger,
        CancellationToken cancellationToken = default);

    Task NotifyDisputeResolvedAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        DisputeStatus status,
        bool isWinner,
        string notes,
        CancellationToken cancellationToken = default);

    Task NotifyJudicialCancellationAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        string notes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação padrão do serviço de notificações de claim e disputas.
/// Registra logs estruturados e simula entrega multicanal (Push/E-mail).
/// </summary>
public class ClaimNotificationService : IClaimNotificationService
{
    private readonly ILogger<ClaimNotificationService> _logger;

    public ClaimNotificationService(ILogger<ClaimNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyRevocationAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[CLAIM_NOTIFICATION] Titularidade revogada para Usuário {UserId} na Igreja {ChurchId} ({ChurchName}). Motivo: {Reason}",
            userId, churchId, churchName, reason);

        return Task.CompletedTask;
    }

    public Task NotifyDisputeOpenedAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        DateTimeOffset deadlineAt,
        bool isChallenger,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[CLAIM_NOTIFICATION] Disputa paritária instaurada {DisputeId} para Igreja {ChurchId} ({ChurchName}). Usuário {UserId} ({Papel}). Prazo: {DeadlineAt:yyyy-MM-dd HH:mm:ss 'UTC'}",
            disputeId, churchId, churchName, userId, isChallenger ? "Contestante" : "Incumbente", deadlineAt);

        return Task.CompletedTask;
    }

    public Task NotifyDisputeResolvedAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        DisputeStatus status,
        bool isWinner,
        string notes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[CLAIM_NOTIFICATION] Disputa {DisputeId} resolvida ({Status}) para Usuário {UserId}. Vencedor: {IsWinner}. Notas: {Notes}",
            disputeId, status, userId, isWinner, notes);

        return Task.CompletedTask;
    }

    public Task NotifyJudicialCancellationAsync(
        Guid userId,
        Guid churchId,
        string churchName,
        Guid disputeId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[CLAIM_NOTIFICATION] Disputa {DisputeId} cancelada para resolução judicial para Usuário {UserId}. Notas: {Notes}",
            disputeId, userId, notes);

        return Task.CompletedTask;
    }
}
