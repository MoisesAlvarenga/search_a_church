using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Claim.Gateways;

namespace SearchAChurch.Api.Features.Claim.Services;

/// <summary>
/// Tipos de desfecho do processo de contestação / disputa (AD-012, AD-016).
/// </summary>
public enum DisputeResolutionType
{
    /// <summary>
    /// Documento de Nível 1 sobrepôs e revogou sumariamente titularidade de Nível 2 ou 3 (AD-012).
    /// </summary>
    AutomaticOverrideN1 = 1,

    /// <summary>
    /// Disputa de mesma hierarquia (N1 vs N1) instaurada com congelamento In_Dispute por 5 dias úteis (AD-016).
    /// </summary>
    ParityDisputeOpened = 2,

    /// <summary>
    /// Resolvido tempestivamente por Prevalência Registral (averbação mais recente no RCPJ).
    /// </summary>
    ResolvedByAverbationPrevalence = 3,

    /// <summary>
    /// Resolvido por preclusão e desclassificação sumária da parte inerte após 5 dias úteis.
    /// </summary>
    ResolvedByInertia = 4,

    /// <summary>
    /// Litígio anulado e retornado a Unclaimed por dúvida jurídica insanável / desfecho judicial.
    /// </summary>
    CanceledJudicialFallback = 5
}

/// <summary>
/// Decisão manual forçada para arbitragem técnica de disputa.
/// </summary>
public enum DisputeManualDecision
{
    DetermineAutomatically = 0,
    InertiaChallengerWins = 1,
    InertiaIncumbentWins = 2,
    JudicialCancel = 3
}

/// <summary>
/// Payload para acionamento do fluxo "Contestar Propriedade desta Igreja" (CLAIM-08, CLAIM-10, CLAIM-11).
/// </summary>
public record DisputeContestRequest(
    string TosVersion,
    bool TosAccepted,
    string LegalRepresentativeName,
    string LegalRepresentativeCpf,
    string ChurchCnpj,
    VerificationTier SubmittedTier,
    string? DocumentFileHash,
    DateTimeOffset? DocumentAverbationDate,
    string? Justification
);

/// <summary>
/// Resposta da abertura ou processamento de contestação de propriedade.
/// </summary>
public record DisputeContestResponse(
    bool Success,
    DisputeResolutionType ResolutionType,
    Guid ChurchId,
    Guid? DisputeId,
    DateTimeOffset? DeadlineAt,
    string Message,
    VerificationTier CurrentTier,
    Guid? ActiveRepresentativeUserId
);

/// <summary>
/// Requisição para anexação de prova/certidão tempestiva durante litígio paritário (AD-016).
/// </summary>
public record DisputeEvidenceSubmissionRequest(
    string DocumentFileHash,
    DateTimeOffset AverbationDate,
    string? Notes = null
);

/// <summary>
/// Resposta da anexação de prova complementar de litígio.
/// </summary>
public record DisputeEvidenceSubmissionResponse(
    Guid DisputeId,
    Guid UserId,
    DateTimeOffset SubmittedAt,
    string DocumentFileHash,
    DateTimeOffset AverbationDate,
    string Message
);

/// <summary>
/// Resposta do encerramento de um litígio paritário.
/// </summary>
public record DisputeResolutionResponse(
    Guid DisputeId,
    Guid ChurchId,
    DisputeStatus Status,
    DisputeResolutionType ResolutionType,
    Guid? WinnerUserId,
    ChurchClaimState ChurchClaimStatus,
    VerificationTier ChurchTier,
    string Message
);

/// <summary>
/// Contrato do motor de governança de contestações e litígios paritários (AD-012, AD-016).
/// </summary>
public interface IDisputeResolutionEngine
{
    /// <summary>
    /// Processa a contestação de propriedade submetida por um solicitante (Resolução Automática N1 ou In_Dispute paritário).
    /// </summary>
    Task<Result<DisputeContestResponse>> ProcessContestAsync(
        Guid churchId,
        Guid challengerUserId,
        DisputeContestRequest request,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permite que qualquer um dos litigantes anexe certidão atualizada do RCPJ dentro do prazo de 5 dias úteis.
    /// </summary>
    Task<Result<DisputeEvidenceSubmissionResponse>> SubmitDisputeEvidenceAsync(
        Guid disputeId,
        Guid userId,
        DisputeEvidenceSubmissionRequest request,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Avalia e resolve formalmente a disputa paritária (por averbação mais recente, inércia processual ou anulação judicial).
    /// </summary>
    Task<Result<DisputeResolutionResponse>> ResolveParityDisputeAsync(
        Guid disputeId,
        DisputeManualDecision? manualDecision = null,
        ClaimConnectionMetadata? connectionMeta = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o perfil da congregação está sob litígio e portanto com edições, membros e PIX congelados (CLAIM-11).
    /// </summary>
    Task<bool> IsProfileFrozenAsync(Guid churchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula o prazo improrrogável de N dias úteis ignorando finais de semana (AD-016).
    /// </summary>
    DateTimeOffset CalculateBusinessDays(DateTimeOffset startDate, int businessDays);
}

/// <summary>
/// Implementação do motor de resolução de disputas com garantia de Prevalência Documental Legal de Nível 1.
/// </summary>
public class DisputeResolutionEngine : IDisputeResolutionEngine
{
    private readonly AppDbContext _dbContext;
    private readonly IClaimAuditLogService _auditLogService;
    private readonly IQsaValidationGateway _qsaGateway;
    private readonly IClaimNotificationService _notificationService;
    private readonly ILogger<DisputeResolutionEngine> _logger;
    private readonly TimeProvider _timeProvider;

    public DisputeResolutionEngine(
        AppDbContext dbContext,
        IClaimAuditLogService auditLogService,
        IQsaValidationGateway qsaGateway,
        IClaimNotificationService notificationService,
        ILogger<DisputeResolutionEngine> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _qsaGateway = qsaGateway ?? throw new ArgumentNullException(nameof(qsaGateway));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Result<DisputeContestResponse>> ProcessContestAsync(
        Guid churchId,
        Guid challengerUserId,
        DisputeContestRequest request,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connectionMeta);

        // 1. Validação de aceite de Termos de Uso sob art. 299 CP (AD-013)
        if (!request.TosAccepted)
        {
            return Result<DisputeContestResponse>.Failure(
                "TERMOS_NAO_ACEITOS",
                "O aceite dos termos sob as penas do art. 299 CP é obrigatório para abertura de contestação.");
        }

        // 2. Validação de hierarquia probatória: contestação exige obrigatoriamente Nível 1 (CLAIM-08, AD-012)
        if (request.SubmittedTier != VerificationTier.Tier1_Cartorio)
        {
            return Result<DisputeContestResponse>.Failure(
                "NIVEL_PROBATORIO_INSUFICIENTE",
                "Contestações de titularidade exigem documentação de Nível 1 registrada em Cartório (RCPJ).");
        }

        // 3. Documento cartorial obrigatório
        if (string.IsNullOrWhiteSpace(request.DocumentFileHash))
        {
            return Result<DisputeContestResponse>.Failure(
                "DOCUMENTO_RCPJ_OBRIGATORIO",
                "A apresentação de documento de Nível 1 (Ata RCPJ/Estatuto) é obrigatória para contestação.");
        }

        // 4. Validação matemática do CPF e CNPJ (QSA)
        if (!_qsaGateway.IsValidCpf(request.LegalRepresentativeCpf))
        {
            return Result<DisputeContestResponse>.Failure(
                "CPF_INVALIDO",
                "O CPF informado para o representante legal é inválido.");
        }

        if (!_qsaGateway.IsValidCnpj(request.ChurchCnpj))
        {
            return Result<DisputeContestResponse>.Failure(
                "CNPJ_INVALIDO",
                "O CNPJ informado para a congregação é inválido.");
        }

        // 5. Localizar a congregação no banco de dados
        var church = await _dbContext.Churches
            .Include(c => c.VerifiedByUser)
            .FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);

        if (church == null)
        {
            return Result<DisputeContestResponse>.Failure(
                "IGREJA_NAO_ENCONTRADA",
                "A igreja especificada não foi encontrada.");
        }

        // 6. Validar estado atual da igreja
        if (church.ClaimStatus == ChurchClaimState.In_Dispute)
        {
            return Result<DisputeContestResponse>.Failure(
                "DISPUTA_EM_ANDAMENTO",
                "A congregação já está com um processo de litígio paritário em andamento.");
        }

        if (church.ClaimStatus != ChurchClaimState.Verified)
        {
            return Result<DisputeContestResponse>.Failure(
                "CONTESTACAO_APENAS_PERFIS_VERIFICADOS",
                "Apenas perfis que estejam atualmente verificados podem ser objeto de contestação de propriedade.");
        }

        if (church.VerifiedByUserId == challengerUserId)
        {
            return Result<DisputeContestResponse>.Failure(
                "USUARIO_JA_E_TITULAR",
                "O solicitante já é o representante verificado desta congregação.");
        }

        var now = _timeProvider.GetUtcNow();

        // 7. CENÁRIO 1: RESOLUÇÃO AUTOMÁTICA POR PREVALÊNCIA DOCUMENTAL LEGAL (CLAIM-10, AD-012)
        // Se a congregação foi verificada por Nível 2 ou Nível 3, a prova de Nível 1 revoga sumariamente o vínculo anterior!
        if (church.VerificationTier != VerificationTier.Tier1_Cartorio)
        {
            var priorIncumbentId = church.VerifiedByUserId;
            var priorTier = church.VerificationTier;

            // Revogar claims aprovados anteriores desta congregação
            var activeClaims = await _dbContext.ChurchClaims
                .IgnoreQueryFilters()
                .Where(c => c.ChurchId == church.Id && c.Status == ClaimRecordStatus.Approved)
                .ToListAsync(cancellationToken);

            foreach (var activeClaim in activeClaims)
            {
                activeClaim.Status = ClaimRecordStatus.Revoked;
                activeClaim.UpdatedAt = now;
            }

            // Criar novo claim aprovado de Nível 1 para o contestante
            var newClaim = new ChurchClaim
            {
                Id = Guid.NewGuid(),
                ChurchId = church.Id,
                UserId = challengerUserId,
                Status = ClaimRecordStatus.Approved,
                TargetTier = VerificationTier.Tier1_Cartorio,
                ValidationMethod = ValidationMethod.CartorioRcpj,
                TosAccepted = true,
                TosVersion = request.TosVersion,
                TosAcceptedAt = now,
                AttemptCount = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            newClaim.Evidences.Add(new ClaimEvidence
            {
                Id = Guid.NewGuid(),
                ClaimId = newClaim.Id,
                EvidenceType = EvidenceType.DocumentPdf,
                FileHashSha256 = request.DocumentFileHash,
                IsApproved = true,
                SubmittedAt = now,
                ReviewedAt = now,
                Metadata = JsonSerializer.Serialize(new
                {
                    representativeName = request.LegalRepresentativeName,
                    representativeCpf = request.LegalRepresentativeCpf,
                    churchCnpj = request.ChurchCnpj,
                    averbationDate = request.DocumentAverbationDate,
                    justification = request.Justification,
                    revokedPriorTier = priorTier.ToString()
                })
            });

            _dbContext.ChurchClaims.Add(newClaim);

            // Atualizar o perfil da igreja para o novo titular de Nível 1
            church.ClaimStatus = ChurchClaimState.Verified;
            church.VerificationTier = VerificationTier.Tier1_Cartorio;
            church.VerifiedByUserId = challengerUserId;
            church.VerifiedAt = now;
            church.IsVerified = true;
            church.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Gravar trilha de auditoria append-only (Marco Civil art. 15 / AD-014)
            if (priorIncumbentId.HasValue)
            {
                await _auditLogService.RecordEventAsync(
                    "ClaimRevoked",
                    church.Id,
                    priorIncumbentId.Value,
                    connectionMeta,
                    new
                    {
                        reason = "Prevalência documental legal de Nível 1 (Cartório RCPJ)",
                        challengerUserId = challengerUserId,
                        priorTier = priorTier.ToString()
                    },
                    cancellationToken);

                // Notificar titular anterior informando que não cabe bloqueio unilateral (CLAIM-10)
                await _notificationService.NotifyRevocationAsync(
                    priorIncumbentId.Value,
                    church.Id,
                    church.Name,
                    "Seu vínculo administrativo foi revogado por prevalência documental legal de Nível 1 (Cartório RCPJ). Conforme as diretrizes da plataforma, não cabe retenção ou bloqueio unilateral.",
                    cancellationToken);
            }

            await _auditLogService.RecordEventAsync(
                "ClaimApprovedAutomaticOverride",
                church.Id,
                challengerUserId,
                connectionMeta,
                new
                {
                    documentHash = request.DocumentFileHash,
                    averbationDate = request.DocumentAverbationDate,
                    tier = "Tier1_Cartorio"
                },
                cancellationToken);

            _logger.LogInformation(
                "[DISPUTE] Resolução automática executada para a Igreja {ChurchId}. Titularidade transferida para {ChallengerUserId} (Nível 1).",
                church.Id, challengerUserId);

            return Result<DisputeContestResponse>.Success(new DisputeContestResponse(
                Success: true,
                ResolutionType: DisputeResolutionType.AutomaticOverrideN1,
                ChurchId: church.Id,
                DisputeId: null,
                DeadlineAt: null,
                Message: "Vínculo anterior revogado sumariamente por prevalência documental legal de Nível 1 (Cartório RCPJ). Titularidade transferida com sucesso.",
                CurrentTier: VerificationTier.Tier1_Cartorio,
                ActiveRepresentativeUserId: challengerUserId
            ));
        }

        // 8. CENÁRIO 2: DISPUTA PARITÁRIA DE MESMA HIERARQUIA N1 vs N1 (CLAIM-11, AD-016)
        // Quando a congregação já possui titular em Nível 1, instaura-se In_Dispute e abre-se o contraditório de 5 dias úteis.
        var deadlineAt = CalculateBusinessDays(now, 5);

        church.ClaimStatus = ChurchClaimState.In_Dispute;
        church.UpdatedAt = now;

        var disputeCase = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challengerUserId,
            IncumbentUserId = church.VerifiedByUserId,
            Status = DisputeStatus.Open,
            OpenedAt = now,
            DeadlineAt = deadlineAt,
            ChallengerDocumentHash = request.DocumentFileHash,
            ChallengerAverbationDate = request.DocumentAverbationDate,
            ResolutionNotes = request.Justification
        };

        _dbContext.DisputeCases.Add(disputeCase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Registrar abertura no log append-only
        await _auditLogService.RecordEventAsync(
            "DisputeOpened",
            church.Id,
            challengerUserId,
            connectionMeta,
            new
            {
                disputeId = disputeCase.Id,
                incumbentUserId = disputeCase.IncumbentUserId,
                deadlineAt = deadlineAt,
                challengerDocumentHash = request.DocumentFileHash,
                challengerAverbationDate = request.DocumentAverbationDate
            },
            cancellationToken);

        // Notificar simultaneamente ambas as partes com prazo improrrogável de 5 dias úteis
        await _notificationService.NotifyDisputeOpenedAsync(
            challengerUserId,
            church.Id,
            church.Name,
            disputeCase.Id,
            deadlineAt,
            isChallenger: true,
            cancellationToken);

        if (disputeCase.IncumbentUserId.HasValue)
        {
            await _notificationService.NotifyDisputeOpenedAsync(
                disputeCase.IncumbentUserId.Value,
                church.Id,
                church.Name,
                disputeCase.Id,
                deadlineAt,
                isChallenger: false,
                cancellationToken);
        }

        _logger.LogInformation(
            "[DISPUTE] Litígio paritário instaurado {DisputeId} para Igreja {ChurchId}. Prazo: {DeadlineAt}",
            disputeCase.Id, church.Id, deadlineAt);

        return Result<DisputeContestResponse>.Success(new DisputeContestResponse(
            Success: true,
            ResolutionType: DisputeResolutionType.ParityDisputeOpened,
            ChurchId: church.Id,
            DisputeId: disputeCase.Id,
            DeadlineAt: deadlineAt,
            Message: "Litígio paritário instaurado com sucesso. Perfil congelado em In_Dispute por 5 dias úteis para juntada de certidão atualizada do RCPJ.",
            CurrentTier: church.VerificationTier,
            ActiveRepresentativeUserId: church.VerifiedByUserId
        ));
    }

    /// <inheritdoc />
    public async Task<Result<DisputeEvidenceSubmissionResponse>> SubmitDisputeEvidenceAsync(
        Guid disputeId,
        Guid userId,
        DisputeEvidenceSubmissionRequest request,
        ClaimConnectionMetadata connectionMeta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(connectionMeta);

        if (string.IsNullOrWhiteSpace(request.DocumentFileHash))
        {
            return Result<DisputeEvidenceSubmissionResponse>.Failure(
                "HASH_DOCUMENTO_OBRIGATORIO",
                "O hash SHA-256 do documento cartorial é obrigatório.");
        }

        var dispute = await _dbContext.DisputeCases
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute == null)
        {
            return Result<DisputeEvidenceSubmissionResponse>.Failure(
                "DISPUTA_NAO_ENCONTRADA",
                "Processo de disputa não encontrado.");
        }

        if (dispute.Status != DisputeStatus.Open && dispute.Status != DisputeStatus.InReview)
        {
            return Result<DisputeEvidenceSubmissionResponse>.Failure(
                "DISPUTA_JA_ENCERRADA",
                "Este processo de disputa já foi concluído.");
        }

        var now = _timeProvider.GetUtcNow();

        // Verificar janela improrrogável de 5 dias úteis (AD-016)
        if (now > dispute.DeadlineAt)
        {
            return Result<DisputeEvidenceSubmissionResponse>.Failure(
                "PRAZO_PRECLUSO",
                "O prazo improrrogável de 5 dias úteis para juntada de certidão comprobatória expirou.");
        }

        // Validar legitimidade da parte requerente
        if (userId != dispute.ChallengerUserId && userId != dispute.IncumbentUserId)
        {
            return Result<DisputeEvidenceSubmissionResponse>.Failure(
                "USUARIO_NAO_AUTORIZADO",
                "O usuário não é parte legítima vinculada a este processo de disputa.");
        }

        if (userId == dispute.ChallengerUserId)
        {
            dispute.ChallengerDocumentHash = request.DocumentFileHash;
            dispute.ChallengerAverbationDate = request.AverbationDate;
        }
        else
        {
            dispute.IncumbentDocumentHash = request.DocumentFileHash;
            dispute.IncumbentAverbationDate = request.AverbationDate;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            dispute.ResolutionNotes = string.IsNullOrWhiteSpace(dispute.ResolutionNotes)
                ? request.Notes
                : $"{dispute.ResolutionNotes} | {request.Notes}";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trilha de auditoria append-only
        await _auditLogService.RecordEventAsync(
            "DisputeEvidenceSubmitted",
            dispute.ChurchId,
            userId,
            connectionMeta,
            new
            {
                disputeId = dispute.Id,
                documentHash = request.DocumentFileHash,
                averbationDate = request.AverbationDate
            },
            cancellationToken);

        return Result<DisputeEvidenceSubmissionResponse>.Success(new DisputeEvidenceSubmissionResponse(
            DisputeId: dispute.Id,
            UserId: userId,
            SubmittedAt: now,
            DocumentFileHash: request.DocumentFileHash,
            AverbationDate: request.AverbationDate,
            Message: "Certidão comprobatória anexada com sucesso aos autos da disputa."
        ));
    }

    /// <inheritdoc />
    public async Task<Result<DisputeResolutionResponse>> ResolveParityDisputeAsync(
        Guid disputeId,
        DisputeManualDecision? manualDecision = null,
        ClaimConnectionMetadata? connectionMeta = null,
        CancellationToken cancellationToken = default)
    {
        var dispute = await _dbContext.DisputeCases
            .Include(d => d.Church)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute == null)
        {
            return Result<DisputeResolutionResponse>.Failure(
                "DISPUTA_NAO_ENCONTRADA",
                "Processo de disputa não encontrado.");
        }

        if (dispute.Status != DisputeStatus.Open && dispute.Status != DisputeStatus.InReview)
        {
            return Result<DisputeResolutionResponse>.Failure(
                "DISPUTA_JA_ENCERRADA",
                "Este processo de disputa já foi finalizado anteriormente.");
        }

        var now = _timeProvider.GetUtcNow();
        var meta = connectionMeta ?? new ClaimConnectionMetadata("127.0.0.1", 443, "SearchAChurch.DisputeEngine/1.0", now);

        // CASO 1: LITÍGIO IRRESOLVÍVEL / DECISÃO JUDICIAL MANUAL (CLAIM-12, Cenário 17)
        if (manualDecision == DisputeManualDecision.JudicialCancel)
        {
            return await ExecuteJudicialCancellationAsync(dispute, meta, "Decisão de cancelamento judicial por dúvida insanável.", cancellationToken);
        }

        // CASO 2: PREVALÊNCIA REGISTRAL (AMBAS AS PARTES ANEXARAM AVERBAÇÕES) (CLAIM-12, Cenário 15)
        if (dispute.ChallengerAverbationDate.HasValue && dispute.IncumbentAverbationDate.HasValue)
        {
            if (dispute.ChallengerAverbationDate.Value > dispute.IncumbentAverbationDate.Value)
            {
                return await AwardToChallengerAsync(
                    dispute,
                    meta,
                    DisputeResolutionType.ResolvedByAverbationPrevalence,
                    $"Prevalência Registral: averbação do contestante ({dispute.ChallengerAverbationDate:yyyy-MM-dd}) é mais recente que a do incumbente ({dispute.IncumbentAverbationDate:yyyy-MM-dd}).",
                    cancellationToken);
            }

            if (dispute.IncumbentAverbationDate.Value > dispute.ChallengerAverbationDate.Value)
            {
                return await AwardToIncumbentAsync(
                    dispute,
                    meta,
                    DisputeResolutionType.ResolvedByAverbationPrevalence,
                    $"Prevalência Registral: averbação do incumbente ({dispute.IncumbentAverbationDate:yyyy-MM-dd}) é mais recente que a do contestante ({dispute.ChallengerAverbationDate:yyyy-MM-dd}).",
                    cancellationToken);
            }

            // Datas idênticas ou contraditórias -> dúvida insanável -> cancelamento judicial
            return await ExecuteJudicialCancellationAsync(
                dispute,
                meta,
                "Datas de averbação idênticas ou inconclusivas entre as partes gerando dúvida registral insanável.",
                cancellationToken);
        }

        // CASO 3: DECISÃO POR FORÇAMENTO MANUAL DE INÉRCIA
        if (manualDecision == DisputeManualDecision.InertiaChallengerWins)
        {
            return await AwardToChallengerAsync(
                dispute,
                meta,
                DisputeResolutionType.ResolvedByInertia,
                "Titularidade conferida ao contestante por inércia processual do incumbente.",
                cancellationToken);
        }

        if (manualDecision == DisputeManualDecision.InertiaIncumbentWins)
        {
            return await AwardToIncumbentAsync(
                dispute,
                meta,
                DisputeResolutionType.ResolvedByInertia,
                "Titularidade mantida com o incumbente por inércia processual do contestante.",
                cancellationToken);
        }

        // CASO 4: DESCLASSIFICAÇÃO AUTOMÁTICA POR INÉRCIA APÓS PRAZO DE 5 DIAS ÚTEIS (CLAIM-12, Cenário 16)
        if (now >= dispute.DeadlineAt)
        {
            bool challengerProvided = !string.IsNullOrWhiteSpace(dispute.ChallengerDocumentHash) || dispute.ChallengerAverbationDate.HasValue;
            bool incumbentProvided = !string.IsNullOrWhiteSpace(dispute.IncumbentDocumentHash) || dispute.IncumbentAverbationDate.HasValue;

            if (challengerProvided && !incumbentProvided)
            {
                return await AwardToChallengerAsync(
                    dispute,
                    meta,
                    DisputeResolutionType.ResolvedByInertia,
                    "Incumbente desclassificado sumariamente por inércia processual ao término do prazo de 5 dias úteis.",
                    cancellationToken);
            }

            if (incumbentProvided && !challengerProvided)
            {
                return await AwardToIncumbentAsync(
                    dispute,
                    meta,
                    DisputeResolutionType.ResolvedByInertia,
                    "Contestante desclassificado sumariamente por inércia processual ao término do prazo de 5 dias úteis.",
                    cancellationToken);
            }

            // Ambas as partes permaneceram inertes após os 5 dias úteis -> cancelamento de ofício
            return await ExecuteJudicialCancellationAsync(
                dispute,
                meta,
                "Ambas as partes permaneceram inertes ao término do prazo de 5 dias úteis.",
                cancellationToken);
        }

        // Se o prazo ainda não transcorreu e nem ambas as partes juntaram documentos, permanece aguardando
        return Result<DisputeResolutionResponse>.Failure(
            "DISPUTA_EM_ANDAMENTO",
            "A disputa segue em andamento aguardando o decurso do prazo de 5 dias úteis ou juntada de certidões por ambas as partes.");
    }

    /// <inheritdoc />
    public async Task<bool> IsProfileFrozenAsync(Guid churchId, CancellationToken cancellationToken = default)
    {
        var church = await _dbContext.Churches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);

        return church != null && church.ClaimStatus == ChurchClaimState.In_Dispute;
    }

    /// <inheritdoc />
    public DateTimeOffset CalculateBusinessDays(DateTimeOffset startDate, int businessDays)
    {
        if (businessDays <= 0) return startDate;

        var current = startDate;
        int addedBusinessDays = 0;

        while (addedBusinessDays < businessDays)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                addedBusinessDays++;
            }
        }

        return current;
    }

    private async Task<Result<DisputeResolutionResponse>> AwardToChallengerAsync(
        DisputeCase dispute,
        ClaimConnectionMetadata meta,
        DisputeResolutionType resolutionType,
        string notes,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        dispute.Status = DisputeStatus.ResolvedChallenger;
        dispute.ResolvedAt = now;
        dispute.ResolutionNotes = notes;

        // Revogar vínculos anteriores da congregação
        var priorApprovedClaims = await _dbContext.ChurchClaims
            .IgnoreQueryFilters()
            .Where(c => c.ChurchId == dispute.ChurchId && c.Status == ClaimRecordStatus.Approved)
            .ToListAsync(cancellationToken);

        foreach (var c in priorApprovedClaims)
        {
            c.Status = ClaimRecordStatus.Revoked;
            c.UpdatedAt = now;
        }

        // Criar ou aprovar claim para o challenger
        var challengerClaim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = dispute.ChurchId,
            UserId = dispute.ChallengerUserId,
            Status = ClaimRecordStatus.Approved,
            TargetTier = VerificationTier.Tier1_Cartorio,
            ValidationMethod = ValidationMethod.CartorioRcpj,
            TosAccepted = true,
            TosVersion = "1.0",
            TosAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.ChurchClaims.Add(challengerClaim);

        // Descongelar a igreja e atribuir titularidade ao vencedor
        dispute.Church.ClaimStatus = ChurchClaimState.Verified;
        dispute.Church.VerificationTier = VerificationTier.Tier1_Cartorio;
        dispute.Church.VerifiedByUserId = dispute.ChallengerUserId;
        dispute.Church.VerifiedAt = now;
        dispute.Church.IsVerified = true;
        dispute.Church.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trilha de auditoria
        await _auditLogService.RecordEventAsync(
            "DisputeResolved",
            dispute.ChurchId,
            dispute.ChallengerUserId,
            meta,
            new { disputeId = dispute.Id, winner = "Challenger", resolutionType = resolutionType.ToString(), notes },
            cancellationToken);

        // Notificações
        await _notificationService.NotifyDisputeResolvedAsync(
            dispute.ChallengerUserId,
            dispute.ChurchId,
            dispute.Church.Name,
            dispute.Id,
            DisputeStatus.ResolvedChallenger,
            isWinner: true,
            notes,
            cancellationToken);

        if (dispute.IncumbentUserId.HasValue)
        {
            await _notificationService.NotifyDisputeResolvedAsync(
                dispute.IncumbentUserId.Value,
                dispute.ChurchId,
                dispute.Church.Name,
                dispute.Id,
                DisputeStatus.ResolvedChallenger,
                isWinner: false,
                notes,
                cancellationToken);
        }

        return Result<DisputeResolutionResponse>.Success(new DisputeResolutionResponse(
            DisputeId: dispute.Id,
            ChurchId: dispute.ChurchId,
            Status: DisputeStatus.ResolvedChallenger,
            ResolutionType: resolutionType,
            WinnerUserId: dispute.ChallengerUserId,
            ChurchClaimStatus: ChurchClaimState.Verified,
            ChurchTier: VerificationTier.Tier1_Cartorio,
            Message: notes
        ));
    }

    private async Task<Result<DisputeResolutionResponse>> AwardToIncumbentAsync(
        DisputeCase dispute,
        ClaimConnectionMetadata meta,
        DisputeResolutionType resolutionType,
        string notes,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        dispute.Status = DisputeStatus.ResolvedIncumbent;
        dispute.ResolvedAt = now;
        dispute.ResolutionNotes = notes;

        // Descongelar a igreja e restaurar status verificado do incumbente
        dispute.Church.ClaimStatus = ChurchClaimState.Verified;
        dispute.Church.VerificationTier = VerificationTier.Tier1_Cartorio;
        dispute.Church.VerifiedByUserId = dispute.IncumbentUserId;
        dispute.Church.VerifiedAt = now;
        dispute.Church.IsVerified = true;
        dispute.Church.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trilha de auditoria
        await _auditLogService.RecordEventAsync(
            "DisputeResolved",
            dispute.ChurchId,
            dispute.IncumbentUserId,
            meta,
            new { disputeId = dispute.Id, winner = "Incumbent", resolutionType = resolutionType.ToString(), notes },
            cancellationToken);

        // Notificações
        if (dispute.IncumbentUserId.HasValue)
        {
            await _notificationService.NotifyDisputeResolvedAsync(
                dispute.IncumbentUserId.Value,
                dispute.ChurchId,
                dispute.Church.Name,
                dispute.Id,
                DisputeStatus.ResolvedIncumbent,
                isWinner: true,
                notes,
                cancellationToken);
        }

        await _notificationService.NotifyDisputeResolvedAsync(
            dispute.ChallengerUserId,
            dispute.ChurchId,
            dispute.Church.Name,
            dispute.Id,
            DisputeStatus.ResolvedIncumbent,
            isWinner: false,
            notes,
            cancellationToken);

        return Result<DisputeResolutionResponse>.Success(new DisputeResolutionResponse(
            DisputeId: dispute.Id,
            ChurchId: dispute.ChurchId,
            Status: DisputeStatus.ResolvedIncumbent,
            ResolutionType: resolutionType,
            WinnerUserId: dispute.IncumbentUserId,
            ChurchClaimStatus: ChurchClaimState.Verified,
            ChurchTier: VerificationTier.Tier1_Cartorio,
            Message: notes
        ));
    }

    private async Task<Result<DisputeResolutionResponse>> ExecuteJudicialCancellationAsync(
        DisputeCase dispute,
        ClaimConnectionMetadata meta,
        string notes,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        dispute.Status = DisputeStatus.CanceledJudicial;
        dispute.ResolvedAt = now;
        dispute.ResolutionNotes = notes;

        // Revogar todos os acessos de ambos os litigantes
        var activeClaims = await _dbContext.ChurchClaims
            .IgnoreQueryFilters()
            .Where(c => c.ChurchId == dispute.ChurchId && c.Status == ClaimRecordStatus.Approved)
            .ToListAsync(cancellationToken);

        foreach (var c in activeClaims)
        {
            c.Status = ClaimRecordStatus.Revoked;
            c.UpdatedAt = now;
        }

        // Retornar a congregação ao status inicial Unclaimed (CLAIM-12, Cenário 17)
        dispute.Church.ClaimStatus = ChurchClaimState.Unclaimed;
        dispute.Church.VerificationTier = VerificationTier.None;
        dispute.Church.VerifiedByUserId = null;
        dispute.Church.VerifiedAt = null;
        dispute.Church.IsVerified = false;
        dispute.Church.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Auditoria
        await _auditLogService.RecordEventAsync(
            "DisputeCanceledJudicial",
            dispute.ChurchId,
            null,
            meta,
            new { disputeId = dispute.Id, notes },
            cancellationToken);

        // Notificação orientando resolução judicial
        await _notificationService.NotifyJudicialCancellationAsync(
            dispute.ChallengerUserId,
            dispute.ChurchId,
            dispute.Church.Name,
            dispute.Id,
            notes,
            cancellationToken);

        if (dispute.IncumbentUserId.HasValue)
        {
            await _notificationService.NotifyJudicialCancellationAsync(
                dispute.IncumbentUserId.Value,
                dispute.ChurchId,
                dispute.Church.Name,
                dispute.Id,
                notes,
                cancellationToken);
        }

        return Result<DisputeResolutionResponse>.Success(new DisputeResolutionResponse(
            DisputeId: dispute.Id,
            ChurchId: dispute.ChurchId,
            Status: DisputeStatus.CanceledJudicial,
            ResolutionType: DisputeResolutionType.CanceledJudicialFallback,
            WinnerUserId: null,
            ChurchClaimStatus: ChurchClaimState.Unclaimed,
            ChurchTier: VerificationTier.None,
            Message: notes
        ));
    }
}
