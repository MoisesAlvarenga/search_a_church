using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Worker em background que avalia periodicamente timeouts de reivindicações (7d doc / 48h social)
/// e dispara lembretes preventivos com 24 horas de antecedência (CLAIM-09, AD-015).
/// </summary>
public class ClaimTtlBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClaimTtlBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollingInterval;

    public ClaimTtlBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ClaimTtlBackgroundService> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? pollingInterval = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _pollingInterval = pollingInterval ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Executa as verificações e expirações de TTL de forma determinística (acessível diretamente por testes).
    /// </summary>
    public async Task ProcessTtlChecksAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditLogService = scope.ServiceProvider.GetRequiredService<IClaimAuditLogService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IClaimNotificationService>();

        var now = _timeProvider.GetUtcNow();
        var workerConnection = new ClaimConnectionMetadata("127.0.0.1", 443, "SearchAChurch.ClaimTtlWorker/1.0", now);

        // 1. Processar Claims Expirados por Timeout (Pending_Verification -> Unclaimed)
        var expiredClaims = await dbContext.ChurchClaims
            .Include(c => c.Church)
            .Where(c => c.Status == ClaimRecordStatus.Pending && c.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredClaims.Count > 0)
        {
            _logger.LogInformation("[CLAIM_TTL_WORKER] Processando {Count} claims expirados por timeout.", expiredClaims.Count);

            foreach (var claim in expiredClaims)
            {
                claim.Status = ClaimRecordStatus.Expired;
                claim.UpdatedAt = now;

                if (claim.Church != null && claim.Church.ClaimStatus == ChurchClaimState.Pending_Verification)
                {
                    claim.Church.ClaimStatus = ChurchClaimState.Unclaimed;
                    claim.Church.UpdatedAt = now;
                }

                await auditLogService.RecordEventAsync(
                    "ClaimExpired",
                    claim.ChurchId,
                    claim.UserId,
                    workerConnection,
                    new
                    {
                        claimId = claim.Id,
                        reason = "Timeout TTL decorrido",
                        targetTier = claim.TargetTier.ToString(),
                        originalExpiresAt = claim.ExpiresAt
                    },
                    cancellationToken);

                await notificationService.NotifyClaimExpiredAsync(
                    claim.UserId,
                    claim.ChurchId,
                    claim.Church?.Name ?? "Igreja",
                    cancellationToken);
            }
        }

        // 2. Disparar Lembretes Preventivos com 24 Horas de Antecedência
        var reminderWindow = now.AddHours(24);
        var claimsNeedingReminder = await dbContext.ChurchClaims
            .Include(c => c.Church)
            .Where(c => c.Status == ClaimRecordStatus.Pending
                     && c.ReminderSentAt == null
                     && c.ExpiresAt <= reminderWindow
                     && c.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        if (claimsNeedingReminder.Count > 0)
        {
            _logger.LogInformation("[CLAIM_TTL_WORKER] Disparando {Count} lembretes preventivos de 24 horas.", claimsNeedingReminder.Count);

            foreach (var claim in claimsNeedingReminder)
            {
                claim.ReminderSentAt = now;
                claim.UpdatedAt = now;

                double hoursLeft = (claim.ExpiresAt - now).TotalHours;

                await auditLogService.RecordEventAsync(
                    "ClaimReminderSent",
                    claim.ChurchId,
                    claim.UserId,
                    workerConnection,
                    new
                    {
                        claimId = claim.Id,
                        hoursLeft = Math.Round(hoursLeft, 1),
                        expiresAt = claim.ExpiresAt
                    },
                    cancellationToken);

                await notificationService.NotifyClaimExpiringReminderAsync(
                    claim.UserId,
                    claim.ChurchId,
                    claim.Church?.Name ?? "Igreja",
                    claim.ExpiresAt,
                    cancellationToken);
            }
        }

        if (expiredClaims.Count > 0 || claimsNeedingReminder.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CLAIM_TTL_WORKER] Serviço de background para TTL de reivindicações iniciado com intervalo de {Interval}.", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTtlChecksAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[CLAIM_TTL_WORKER] Erro durante a verificação de TTL de reivindicações.");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("[CLAIM_TTL_WORKER] Serviço de background para TTL finalizado.");
    }
}
