using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Requisição para início de reivindicação de perfil com aceite obrigatório de ToS (art. 299 CP e Provedora de Aplicação).
/// </summary>
public record InitiateClaimRequest(
    Guid ChurchId,
    string TosVersion,
    bool Art299Accepted,
    bool TechnicalIntermediaryAccepted,
    ValidationMethod ValidationMethod,
    VerificationTier TargetTier
);

/// <summary>
/// Resposta de reivindicação iniciada com sucesso.
/// </summary>
public record InitiateClaimResponse(
    Guid ClaimId,
    Guid ChurchId,
    ClaimRecordStatus Status,
    VerificationTier TargetTier,
    DateTimeOffset? ExpiresAt,
    int TtlHours,
    string Message
);

/// <summary>
/// Requisição para submissão de evidência comprobatória de reivindicação.
/// </summary>
public record SubmitEvidenceRequest(
    Guid ClaimId,
    EvidenceType EvidenceType,
    string? RawDataOrUrl = null,
    string? FileHashSha256 = null,
    string? MetadataJson = null
);

/// <summary>
/// Resposta de verificação de evidência e concessão de selo.
/// </summary>
public record VerificationResultResponse(
    Guid ClaimId,
    Guid ChurchId,
    bool IsVerified,
    VerificationTier Tier,
    string Message
);

/// <summary>
/// Resposta com status detalhado do perfil da congregação no ciclo de claim.
/// </summary>
public record ChurchClaimStatusResponse(
    Guid ChurchId,
    string ChurchName,
    ChurchClaimState ClaimStatus,
    VerificationTier VerificationTier,
    bool IsVerified,
    bool IsCurrentUserRepresentative,
    Guid? ActiveClaimId,
    ClaimRecordStatus? ActiveClaimStatus,
    DateTimeOffset? ExpiresAt,
    TimeSpan? TimeRemaining
);

/// <summary>
/// Contrato do serviço orquestrador de ciclo de vida de reivindicação de igrejas (AD-005, AD-013, AD-015).
/// </summary>
public interface IClaimOrchestratorService
{
    /// <summary>
    /// Inicia o processo de claim validando termos de uso, rate limits e concorrência, atribuindo o TTL correto.
    /// </summary>
    Task<Result<InitiateClaimResponse>> InitiateClaimAsync(
        InitiateClaimRequest request,
        Guid userId,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submete uma evidência probatória, homologa a verificação e atualiza a congregação para Verified.
    /// </summary>
    Task<Result<VerificationResultResponse>> SubmitEvidenceAsync(
        SubmitEvidenceRequest request,
        Guid userId,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta o estado atual da congregação no ciclo de claim para o usuário requisitante.
    /// </summary>
    Task<Result<ChurchClaimStatusResponse>> GetStatusAsync(
        Guid churchId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação do serviço orquestrador de reivindicação de perfil.
/// </summary>
public class ClaimOrchestratorService : IClaimOrchestratorService
{
    private readonly AppDbContext _dbContext;
    private readonly IClaimAuditLogService _auditLogService;
    private readonly IClaimNotificationService _notificationService;
    private readonly ILogger<ClaimOrchestratorService> _logger;
    private readonly TimeProvider _timeProvider;

    public ClaimOrchestratorService(
        AppDbContext dbContext,
        IClaimAuditLogService auditLogService,
        IClaimNotificationService notificationService,
        ILogger<ClaimOrchestratorService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Result<InitiateClaimResponse>> InitiateClaimAsync(
        InitiateClaimRequest request,
        Guid userId,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connectionMeta);

        // 1. Validação de aceite de Termos de Uso (art. 299 CP e Provedora de Aplicação - AD-013)
        if (!request.Art299Accepted)
        {
            return Result<InitiateClaimResponse>.Failure(
                "TERMOS_NAO_ACEITOS",
                "O aceite da declaração sob as penas do art. 299 CP é obrigatório para iniciar a reivindicação.");
        }

        // Validação da cláusula de intermediária técnica para fluxos simplificados (Níveis 2 e 3)
        if (request.TargetTier != VerificationTier.Tier1_Cartorio && !request.TechnicalIntermediaryAccepted)
        {
            return Result<InitiateClaimResponse>.Failure(
                "CLAUSULA_INTERMEDIARIA_OBRIGATORIA",
                "O aceite da cláusula de enquadramento técnico e mera intermediária é obrigatório para validações simplificadas.");
        }

        var now = _timeProvider.GetUtcNow();

        // 2. Verificar existência da igreja
        var church = await _dbContext.Churches
            .FirstOrDefaultAsync(c => c.Id == request.ChurchId, cancellationToken);

        if (church == null)
        {
            return Result<InitiateClaimResponse>.Failure(
                "IGREJA_NAO_ENCONTRADA",
                "A igreja especificada não foi encontrada.");
        }

        // 3. Validação do estado atual da igreja
        if (church.ClaimStatus == ChurchClaimState.Verified)
        {
            return Result<InitiateClaimResponse>.Failure(
                "IGREJA_JA_REIVINDICADA",
                "Esta congregação já possui representante verificado. Para contestar a titularidade, utilize o fluxo de contestação de propriedade.");
        }

        if (church.ClaimStatus == ChurchClaimState.In_Dispute)
        {
            return Result<InitiateClaimResponse>.Failure(
                "DISPUTA_EM_ANDAMENTO",
                "A congregação está com litígio paritário em andamento. Novas reivindicações estão bloqueadas temporariamente.");
        }

        // 4. Rate Limiting / Lockout: máximo de 3 tentativas rejeitadas consecutivas em 72h (CLAIM-09)
        var lockoutCutoff = now.AddHours(-72);
        var recentRejections = await _dbContext.ChurchClaims
            .Where(c => c.ChurchId == church.Id && c.UserId == userId && c.Status == ClaimRecordStatus.Rejected && c.CreatedAt >= lockoutCutoff)
            .CountAsync(cancellationToken);

        if (recentRejections >= 3)
        {
            return Result<InitiateClaimResponse>.Failure(
                "BLOQUEIO_TEMPORARIO_TENTATIVAS",
                "Limite de 3 tentativas consecutivas excedido para esta congregação. Bloqueio temporário de 72 horas ativo.");
        }

        // 5. Concorrência e Claims Ativos Concorrentes (seção 6 da spec)
        if (church.ClaimStatus == ChurchClaimState.Pending_Verification)
        {
            var activePendingClaim = await _dbContext.ChurchClaims
                .FirstOrDefaultAsync(c => c.ChurchId == church.Id && c.Status == ClaimRecordStatus.Pending && c.ExpiresAt > now, cancellationToken);

            if (activePendingClaim != null)
            {
                if (activePendingClaim.UserId == userId)
                {
                    return Result<InitiateClaimResponse>.Failure(
                        "CLAIM_JA_EM_ANDAMENTO",
                        "Você já possui uma solicitação de reivindicação em andamento para esta congregação.");
                }

                // Se o novo solicitante apresentar prova de maior autoridade (Tier 1 contra Tier 2 ou 3), ganha precedência e cancela o processo concorrente inferior
                if (request.TargetTier == VerificationTier.Tier1_Cartorio && activePendingClaim.TargetTier != VerificationTier.Tier1_Cartorio)
                {
                    activePendingClaim.Status = ClaimRecordStatus.Revoked;
                    activePendingClaim.UpdatedAt = now;

                    await _auditLogService.RecordEventAsync(
                        "ClaimOverriddenByHigherTier",
                        church.Id,
                        activePendingClaim.UserId,
                        connectionMeta,
                        new { reason = "Precedência de Nível 1 sobre reivindicação pendente de nível inferior", newUserId = userId },
                        cancellationToken);
                }
                else
                {
                    return Result<InitiateClaimResponse>.Failure(
                        "CLAIM_PENDENTE_OUTRO_USUARIO",
                        "Já existe uma solicitação de reivindicação em análise para esta congregação por outro solicitante.");
                }
            }
        }

        // 6. Cálculo diferenciado de TTL conforme o fluxo (AD-015)
        // Fluxo Documental (Cartório/QSA): TTL de até 7 dias corridos (168 horas)
        // Fluxo Social/Digital (Bio/Email/Geofence): TTL reduzido de 48 horas
        bool isDocumentalFlow = request.TargetTier == VerificationTier.Tier1_Cartorio
            || request.ValidationMethod == ValidationMethod.CartorioRcpj
            || request.ValidationMethod == ValidationMethod.ReceitaQsa;

        int ttlHours = isDocumentalFlow ? 168 : 48;
        var expiresAt = now.AddHours(ttlHours);

        // 7. Persistência do novo claim
        var claim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = userId,
            Status = ClaimRecordStatus.Pending,
            TargetTier = request.TargetTier,
            ValidationMethod = request.ValidationMethod,
            TosAccepted = true,
            TosVersion = request.TosVersion,
            TosAcceptedAt = now,
            ExpiresAt = expiresAt,
            AttemptCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.ChurchClaims.Add(claim);

        // Atualizar status da congregação
        church.ClaimStatus = ChurchClaimState.Pending_Verification;
        church.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 8. Gravação de auditoria do Marco Civil (art. 15 da Lei nº 12.965/2014)
        await _auditLogService.RecordEventAsync(
            "ClaimInitiated",
            church.Id,
            userId,
            connectionMeta,
            new
            {
                claimId = claim.Id,
                tosVersion = request.TosVersion,
                validationMethod = request.ValidationMethod.ToString(),
                targetTier = request.TargetTier.ToString(),
                expiresAt = expiresAt,
                ttlHours = ttlHours
            },
            cancellationToken);

        _logger.LogInformation(
            "[CLAIM_ORCHESTRATOR] Reivindicação {ClaimId} iniciada para Igreja {ChurchId} pelo Usuário {UserId}. TTL: {TtlHours}h.",
            claim.Id, church.Id, userId, ttlHours);

        return Result<InitiateClaimResponse>.Success(new InitiateClaimResponse(
            ClaimId: claim.Id,
            ChurchId: church.Id,
            Status: ClaimRecordStatus.Pending,
            TargetTier: request.TargetTier,
            ExpiresAt: expiresAt,
            TtlHours: ttlHours,
            Message: "Reivindicação iniciada com sucesso. Submeta as evidências necessárias antes da expiração do prazo."
        ));
    }

    /// <inheritdoc />
    public async Task<Result<VerificationResultResponse>> SubmitEvidenceAsync(
        SubmitEvidenceRequest request,
        Guid userId,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connectionMeta);

        var claim = await _dbContext.ChurchClaims
            .Include(c => c.Church)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken);

        if (claim == null)
        {
            return Result<VerificationResultResponse>.Failure(
                "CLAIM_NAO_ENCONTRADO",
                "Solicitação de reivindicação não encontrada.");
        }

        if (claim.UserId != userId)
        {
            return Result<VerificationResultResponse>.Failure(
                "USUARIO_NAO_AUTORIZADO",
                "O usuário não tem permissão para submeter evidências para esta reivindicação.");
        }

        if (claim.Status == ClaimRecordStatus.Approved)
        {
            return Result<VerificationResultResponse>.Failure(
                "CLAIM_JA_APROVADO",
                "Esta reivindicação já foi aprovada anteriormente.");
        }

        if (claim.Status != ClaimRecordStatus.Pending)
        {
            return Result<VerificationResultResponse>.Failure(
                "CLAIM_STATUS_INVALIDO",
                $"Não é possível submeter evidências para um claim com status {claim.Status}.");
        }

        var now = _timeProvider.GetUtcNow();

        // Validar timeout / TTL expirado (AD-015)
        if (now > claim.ExpiresAt)
        {
            claim.Status = ClaimRecordStatus.Expired;
            claim.UpdatedAt = now;

            if (claim.Church != null)
            {
                claim.Church.ClaimStatus = ChurchClaimState.Unclaimed;
                claim.Church.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogService.RecordEventAsync(
                "ClaimExpired",
                claim.ChurchId,
                userId,
                connectionMeta,
                new { claimId = claim.Id, reason = "TTL vencido ao tentar submeter evidência" },
                cancellationToken);

            return Result<VerificationResultResponse>.Failure(
                "CLAIM_EXPIRADO",
                "O prazo limite para submissão de evidências desta reivindicação expirou.");
        }

        // Criar registro de evidência
        var evidence = new ClaimEvidence
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            EvidenceType = request.EvidenceType,
            RawDataOrUrl = request.RawDataOrUrl,
            FileHashSha256 = request.FileHashSha256,
            Metadata = request.MetadataJson ?? "{}",
            IsApproved = true,
            SubmittedAt = now,
            ReviewedAt = now
        };

        _dbContext.ClaimEvidences.Add(evidence);

        // Homologar aprovação do claim
        claim.Status = ClaimRecordStatus.Approved;
        claim.UpdatedAt = now;

        // Atualizar perfil da congregação com concessão do selo correspondente
        if (claim.Church != null)
        {
            claim.Church.ClaimStatus = ChurchClaimState.Verified;
            claim.Church.VerificationTier = claim.TargetTier;
            claim.Church.VerifiedByUserId = userId;
            claim.Church.VerifiedAt = now;
            claim.Church.IsVerified = true;
            claim.Church.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trilha de auditoria append-only
        await _auditLogService.RecordEventAsync(
            "EvidenceSubmitted",
            claim.ChurchId,
            userId,
            connectionMeta,
            new { claimId = claim.Id, evidenceId = evidence.Id, evidenceType = request.EvidenceType.ToString(), fileHash = request.FileHashSha256 },
            cancellationToken);

        await _auditLogService.RecordEventAsync(
            "ClaimApproved",
            claim.ChurchId,
            userId,
            connectionMeta,
            new { claimId = claim.Id, targetTier = claim.TargetTier.ToString() },
            cancellationToken);

        // Notificar solicitante da concessão do selo
        if (claim.Church != null)
        {
            await _notificationService.NotifyClaimApprovedAsync(
                userId,
                claim.ChurchId,
                claim.Church.Name,
                claim.TargetTier,
                cancellationToken);
        }

        _logger.LogInformation(
            "[CLAIM_ORCHESTRATOR] Reivindicação {ClaimId} aprovada com sucesso. Igreja {ChurchId} verificada com {Tier}.",
            claim.Id, claim.ChurchId, claim.TargetTier);

        return Result<VerificationResultResponse>.Success(new VerificationResultResponse(
            ClaimId: claim.Id,
            ChurchId: claim.ChurchId,
            IsVerified: true,
            Tier: claim.TargetTier,
            Message: "Evidência aprovada e titularidade concedida com sucesso."
        ));
    }

    /// <inheritdoc />
    public async Task<Result<ChurchClaimStatusResponse>> GetStatusAsync(
        Guid churchId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var church = await _dbContext.Churches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);

        if (church == null)
        {
            return Result<ChurchClaimStatusResponse>.Failure(
                "IGREJA_NAO_ENCONTRADA",
                "Igreja não encontrada.");
        }

        var now = _timeProvider.GetUtcNow();

        var activeClaim = await _dbContext.ChurchClaims
            .AsNoTracking()
            .Where(c => c.ChurchId == churchId && c.UserId == currentUserId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        bool isRepresentative = church.IsVerified && church.VerifiedByUserId == currentUserId;
        TimeSpan? timeRemaining = null;

        if (activeClaim != null && activeClaim.Status == ClaimRecordStatus.Pending)
        {
            timeRemaining = activeClaim.ExpiresAt > now
                ? activeClaim.ExpiresAt - now
                : TimeSpan.Zero;
        }

        return Result<ChurchClaimStatusResponse>.Success(new ChurchClaimStatusResponse(
            ChurchId: church.Id,
            ChurchName: church.Name,
            ClaimStatus: church.ClaimStatus,
            VerificationTier: church.VerificationTier,
            IsVerified: church.IsVerified,
            IsCurrentUserRepresentative: isRepresentative,
            ActiveClaimId: activeClaim?.Id,
            ActiveClaimStatus: activeClaim?.Status,
            ExpiresAt: activeClaim != null ? activeClaim.ExpiresAt : null,
            TimeRemaining: timeRemaining
        ));
    }
}
