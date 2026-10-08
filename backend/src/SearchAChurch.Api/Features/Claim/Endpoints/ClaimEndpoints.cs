using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Claim.Gateways;
using SearchAChurch.Api.Features.Claim.Services;

namespace SearchAChurch.Api.Features.Claim.Endpoints;

#region Request Payloads

public record ApiInitiateClaimRequest(
    Guid ChurchId,
    string TosVersion,
    bool Art299Accepted,
    bool TechnicalIntermediaryAccepted,
    ValidationMethod ValidationMethod,
    VerificationTier TargetTier
);

public record ApiGeofenceVerificationRequest(
    Guid ClaimId,
    double DeviceLatitude,
    double DeviceLongitude,
    double HorizontalAccuracyMeters,
    bool IsMockLocation,
    string? PhotoUrl = null,
    string? PhotoHashSha256 = null
);

public record ApiGenerateSocialTokenRequest(
    Guid ClaimId,
    string SocialNetwork,
    string ProfileHandle
);

public record ApiConfirmSocialBioRequest(
    Guid ClaimId,
    string SocialNetwork,
    string ProfileHandle,
    string ExpectedToken,
    string? BioContent = null
);

public record ApiSendDomainOtpRequest(
    Guid ClaimId,
    string CorporateEmail,
    string ExpectedDomain
);

public record ApiConfirmDomainOtpRequest(
    Guid ClaimId,
    string CorporateEmail,
    string OtpCode
);

public record ApiSubmitCartorioDocumentRequest(
    Guid ClaimId,
    string DocumentFileName,
    string DocumentFileHashSha256,
    DateTimeOffset AverbationDate,
    string? DocumentUrl = null
);

public record ApiVerifyQsaRequest(
    Guid ClaimId,
    string RepresentativeName,
    string RepresentativeCpf,
    string ChurchCnpj
);

public record ApiOpenDisputeRequest(
    Guid ChurchId,
    string TosVersion,
    bool TosAccepted,
    string LegalRepresentativeName,
    string LegalRepresentativeCpf,
    string ChurchCnpj,
    VerificationTier SubmittedTier,
    string DocumentFileHash,
    DateTimeOffset DocumentAverbationDate,
    string? Justification = null
);

public record ApiSubmitDisputeCertificateRequest(
    string DocumentFileHash,
    DateTimeOffset AverbationDate,
    string? Notes = null
);

#endregion

/// <summary>
/// Mapeamento de endpoints Minimal API para o ciclo de vida de reivindicação, validações e disputas (/claim/*).
/// Todos os endpoints exigem autenticação Bearer JWT obrigatória (AD-006, AD-009, CLAIM-01 a CLAIM-12).
/// </summary>
public static class ClaimEndpoints
{
    public static RouteGroupBuilder MapClaimEndpoints(this RouteGroupBuilder group)
    {
        // 1. Início de Reivindicação com aceite obrigatório de ToS
        group.MapPost("/initiate", InitiateClaimAsync)
             .RequireAuthorization()
             .Produces<InitiateClaimResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status409Conflict)
             .WithName("InitiateClaim")
             .WithSummary("Inicia reivindicação com aceite obrigatório de ToS e auditoria do Marco Civil");

        // 2. Validação Nível 3 - Presença Física (Geofencing Haversine + Foto ao vivo)
        group.MapPost("/verify/geofence", VerifyGeofenceAsync)
             .RequireAuthorization()
             .Produces<VerificationResultResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("VerifyGeofence")
             .WithSummary("Valida presença física por Haversine server-side (raio <= 100m, accuracy <= 50m) e foto ao vivo");

        // 3. Validação Nível 3 - Redes Sociais (Gera código de bio ou valida presença na bio)
        group.MapPost("/verify/social-bio/generate", GenerateSocialTokenAsync)
             .RequireAuthorization()
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("GenerateSocialToken")
             .WithSummary("Gera token temporário SAC-XXXX-VERIFY com validade de 48 horas");

        group.MapPost("/verify/social-bio/confirm", ConfirmSocialBioAsync)
             .RequireAuthorization()
             .Produces<VerificationResultResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("ConfirmSocialBio")
             .WithSummary("Verifica a presença do token na bio da conta oficial da congregação");

        // 4. Validação Nível 2 - Domínio Institucional (E-mail OTP)
        group.MapPost("/verify/domain/send-otp", SendDomainOtpAsync)
             .RequireAuthorization()
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("SendDomainOtp")
             .WithSummary("Envia código OTP para e-mail com domínio institucional próprio da congregação");

        group.MapPost("/verify/domain/confirm-otp", ConfirmDomainOtpAsync)
             .RequireAuthorization()
             .Produces<VerificationResultResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("ConfirmDomainOtp")
             .WithSummary("Confirma código OTP numérico de 6 dígitos em domínio institucional");

        // 5. Validação Nível 1 - Cartório RCPJ e Consulta QSA (Receita Federal)
        group.MapPost("/verify/document/rcpj", SubmitCartorioDocumentAsync)
             .RequireAuthorization()
             .Produces<VerificationResultResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("SubmitCartorioDocument")
             .WithSummary("Submete Ata de Posse registrada em RCPJ e Estatuto Social para validação de Nível 1");

        group.MapPost("/verify/document/qsa", VerifyQsaAsync)
             .RequireAuthorization()
             .Produces<VerificationResultResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .WithName("VerifyQsa")
             .WithSummary("Valida representação legal via cruzamento de CPF com QSA da Receita Federal");

        // 6. Contestações e Resolução de Disputas
        group.MapPost("/dispute/contest", OpenDisputeAsync)
             .RequireAuthorization()
             .Produces<DisputeContestResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status409Conflict)
             .WithName("OpenDispute")
             .WithSummary("Abre contestação pública contra perfil; aplica sobreposição sumária N1 ou congela em In_Dispute");

        group.MapPost("/dispute/{id:guid}/submit-certificate", SubmitDisputeCertificateAsync)
             .RequireAuthorization()
             .Produces<DisputeEvidenceSubmissionResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("SubmitDisputeCertificate")
             .WithSummary("Submete Certidão de Breve Relato do RCPJ durante a janela de 5 dias úteis de In_Dispute");

        // 7. Consulta de Status e Permissões
        group.MapGet("/status/{churchId:guid}", GetClaimStatusAsync)
             .RequireAuthorization()
             .Produces<ChurchClaimStatusResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("GetClaimStatus")
             .WithSummary("Consulta estado do ciclo de vida, permissões ativas e histórico de reivindicação");

        return group;
    }

    private static Guid? ExtractUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.User.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static async Task<IResult> InitiateClaimAsync(
        [FromBody] ApiInitiateClaimRequest request,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var serviceRequest = new InitiateClaimRequest(
            ChurchId: request.ChurchId,
            TosVersion: request.TosVersion,
            Art299Accepted: request.Art299Accepted,
            TechnicalIntermediaryAccepted: request.TechnicalIntermediaryAccepted,
            ValidationMethod: request.ValidationMethod,
            TargetTier: request.TargetTier
        );

        var result = await orchestrator.InitiateClaimAsync(serviceRequest, userId.Value, connection, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "IGREJA_JA_REIVINDICADA" ||
                result.ErrorCode == "DISPUTA_EM_ANDAMENTO" ||
                result.ErrorCode == "CLAIM_JA_EM_ANDAMENTO" ||
                result.ErrorCode == "CLAIM_PENDENTE_OUTRO_USUARIO")
            {
                return Results.Conflict(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> VerifyGeofenceAsync(
        [FromBody] ApiGeofenceVerificationRequest request,
        AppDbContext dbContext,
        IGeofencingService geofencingService,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var claim = await dbContext.ChurchClaims
            .Include(c => c.Church)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, ct);

        if (claim == null)
        {
            return Results.BadRequest(new { error = "CLAIM_NAO_ENCONTRADO", message = "Reivindicação não encontrada." });
        }

        if (claim.Church == null)
        {
            return Results.BadRequest(new { error = "IGREJA_NAO_ENCONTRADA", message = "Igreja associada não encontrada." });
        }

        // 1. Validação geodésica server-side
        var geofenceResult = geofencingService.ValidatePresence(
            churchLat: claim.Church.Latitude,
            churchLng: claim.Church.Longitude,
            deviceLat: request.DeviceLatitude,
            deviceLng: request.DeviceLongitude,
            horizontalAccuracyMeters: request.HorizontalAccuracyMeters,
            isMockLocation: request.IsMockLocation
        );

        if (!geofenceResult.IsValid)
        {
            return Results.BadRequest(new { error = geofenceResult.ErrorCode, message = geofenceResult.ErrorMessage });
        }

        // 2. Submissão de evidência e homologação
        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var metadataJson = JsonSerializer.Serialize(new
        {
            accuracyMeters = request.HorizontalAccuracyMeters,
            deviceLat = request.DeviceLatitude,
            deviceLng = request.DeviceLongitude
        });

        var submitRequest = new SubmitEvidenceRequest(
            ClaimId: request.ClaimId,
            EvidenceType: EvidenceType.PhotoGps,
            RawDataOrUrl: request.PhotoUrl,
            FileHashSha256: request.PhotoHashSha256,
            MetadataJson: metadataJson
        );

        var submitResult = await orchestrator.SubmitEvidenceAsync(submitRequest, userId.Value, connection, ct);

        if (!submitResult.IsSuccess)
        {
            return Results.BadRequest(new { error = submitResult.ErrorCode, message = submitResult.ErrorMessage });
        }

        return Results.Ok(submitResult.Value);
    }

    private static async Task<IResult> GenerateSocialTokenAsync(
        [FromBody] ApiGenerateSocialTokenRequest request,
        ISocialVerificationGateway socialGateway,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var tokenResult = await socialGateway.GenerateTokenAsync(request.ClaimId, request.ProfileHandle, ct);

        return Results.Ok(new
        {
            claimId = request.ClaimId,
            token = tokenResult.Token,
            expiresAt = tokenResult.ExpiresAt,
            instructions = $"Insira o token '{tokenResult.Token}' na bio oficial do @{request.ProfileHandle} na rede social {request.SocialNetwork}."
        });
    }

    private static async Task<IResult> ConfirmSocialBioAsync(
        [FromBody] ApiConfirmSocialBioRequest request,
        ISocialVerificationGateway socialGateway,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        if (!string.IsNullOrWhiteSpace(request.BioContent) &&
            !request.BioContent.Contains(request.ExpectedToken, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                error = "TOKEN_BIO_NAO_ENCONTRADO",
                message = "O token de validação não foi encontrado na bio da conta informada."
            });
        }

        var bioResult = await socialGateway.ValidateBioTokenAsync(request.ClaimId, request.ProfileHandle, request.ExpectedToken, ct);

        if (!bioResult.IsValid)
        {
            return Results.BadRequest(new
            {
                error = "TOKEN_BIO_NAO_ENCONTRADO",
                message = bioResult.ErrorMessage ?? "O token de validação não foi encontrado na bio da conta informada."
            });
        }

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var submitRequest = new SubmitEvidenceRequest(
            ClaimId: request.ClaimId,
            EvidenceType: EvidenceType.BioToken,
            RawDataOrUrl: $"@{request.ProfileHandle}",
            MetadataJson: JsonSerializer.Serialize(new { socialNetwork = request.SocialNetwork, token = request.ExpectedToken })
        );

        var submitResult = await orchestrator.SubmitEvidenceAsync(submitRequest, userId.Value, connection, ct);

        if (!submitResult.IsSuccess)
        {
            return Results.BadRequest(new { error = submitResult.ErrorCode, message = submitResult.ErrorMessage });
        }

        return Results.Ok(submitResult.Value);
    }

    private static async Task<IResult> SendDomainOtpAsync(
        [FromBody] ApiSendDomainOtpRequest request,
        IDomainEmailGateway emailGateway,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        if (!emailGateway.IsInstitutionalDomain(request.CorporateEmail))
        {
            return Results.BadRequest(new { error = "DOMINIO_PUBLICO_INVALIDO", message = "O e-mail informado deve pertencer a um domínio institucional próprio da igreja." });
        }

        var result = await emailGateway.SendOtpAsync(request.ClaimId, request.CorporateEmail, ct);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = "FALHA_ENVIO_OTP", message = result.Message ?? "Falha ao enviar código OTP institucional." });
        }

        return Results.Ok(new
        {
            claimId = request.ClaimId,
            email = request.CorporateEmail,
            expiresAt = result.ExpiresAtUtc,
            message = result.Message ?? "Código OTP de verificação enviado com sucesso (validade 15 min)."
        });
    }

    private static async Task<IResult> ConfirmDomainOtpAsync(
        [FromBody] ApiConfirmDomainOtpRequest request,
        IDomainEmailGateway emailGateway,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var isValidOtp = await emailGateway.ValidateOtpAsync(request.ClaimId, request.OtpCode, ct);

        if (!isValidOtp)
        {
            return Results.BadRequest(new { error = "OTP_INVALIDO_OU_EXPIRADO", message = "O código OTP informado é inválido ou expirou." });
        }

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var submitRequest = new SubmitEvidenceRequest(
            ClaimId: request.ClaimId,
            EvidenceType: EvidenceType.EmailOtp,
            RawDataOrUrl: request.CorporateEmail,
            MetadataJson: JsonSerializer.Serialize(new { email = request.CorporateEmail })
        );

        var submitResult = await orchestrator.SubmitEvidenceAsync(submitRequest, userId.Value, connection, ct);

        if (!submitResult.IsSuccess)
        {
            return Results.BadRequest(new { error = submitResult.ErrorCode, message = submitResult.ErrorMessage });
        }

        return Results.Ok(submitResult.Value);
    }

    private static async Task<IResult> SubmitCartorioDocumentAsync(
        [FromBody] ApiSubmitCartorioDocumentRequest request,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.DocumentFileHashSha256))
        {
            return Results.BadRequest(new { error = "HASH_DOCUMENTO_OBRIGATORIO", message = "O hash SHA-256 do documento cartorial é obrigatório." });
        }

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var submitRequest = new SubmitEvidenceRequest(
            ClaimId: request.ClaimId,
            EvidenceType: EvidenceType.DocumentPdf,
            RawDataOrUrl: request.DocumentUrl ?? request.DocumentFileName,
            FileHashSha256: request.DocumentFileHashSha256,
            MetadataJson: JsonSerializer.Serialize(new
            {
                fileName = request.DocumentFileName,
                averbationDate = request.AverbationDate
            })
        );

        var submitResult = await orchestrator.SubmitEvidenceAsync(submitRequest, userId.Value, connection, ct);

        if (!submitResult.IsSuccess)
        {
            return Results.BadRequest(new { error = submitResult.ErrorCode, message = submitResult.ErrorMessage });
        }

        return Results.Ok(submitResult.Value);
    }

    private static async Task<IResult> VerifyQsaAsync(
        [FromBody] ApiVerifyQsaRequest request,
        IQsaValidationGateway qsaGateway,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        if (!qsaGateway.IsValidCpf(request.RepresentativeCpf))
        {
            return Results.BadRequest(new { error = "CPF_INVALIDO", message = "O CPF informado para o representante é inválido." });
        }

        if (!qsaGateway.IsValidCnpj(request.ChurchCnpj))
        {
            return Results.BadRequest(new { error = "CNPJ_INVALIDO", message = "O CNPJ informado para a congregação é inválido." });
        }

        var qsaResult = await qsaGateway.ValidateRepresentativeAsync(request.ChurchCnpj, request.RepresentativeCpf, ct);

        if (!qsaResult.IsQualified)
        {
            return Results.BadRequest(new { error = qsaResult.ErrorCode ?? "REPRESENTANTE_NAO_QUALIFICADO_QSA", message = qsaResult.ErrorMessage ?? "Representante não qualificado no QSA da Receita Federal." });
        }

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var submitRequest = new SubmitEvidenceRequest(
            ClaimId: request.ClaimId,
            EvidenceType: EvidenceType.QsaCrossCheck,
            RawDataOrUrl: request.ChurchCnpj,
            MetadataJson: JsonSerializer.Serialize(new
            {
                representativeName = request.RepresentativeName,
                representativeCpf = request.RepresentativeCpf,
                churchCnpj = request.ChurchCnpj
            })
        );

        var submitResult = await orchestrator.SubmitEvidenceAsync(submitRequest, userId.Value, connection, ct);

        if (!submitResult.IsSuccess)
        {
            return Results.BadRequest(new { error = submitResult.ErrorCode, message = submitResult.ErrorMessage });
        }

        return Results.Ok(submitResult.Value);
    }

    private static async Task<IResult> OpenDisputeAsync(
        [FromBody] ApiOpenDisputeRequest request,
        IDisputeResolutionEngine disputeEngine,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var contestRequest = new DisputeContestRequest(
            TosVersion: request.TosVersion,
            TosAccepted: request.TosAccepted,
            LegalRepresentativeName: request.LegalRepresentativeName,
            LegalRepresentativeCpf: request.LegalRepresentativeCpf,
            ChurchCnpj: request.ChurchCnpj,
            SubmittedTier: request.SubmittedTier,
            DocumentFileHash: request.DocumentFileHash,
            DocumentAverbationDate: request.DocumentAverbationDate,
            Justification: request.Justification
        );

        var result = await disputeEngine.ProcessContestAsync(request.ChurchId, userId.Value, contestRequest, connection, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "IGREJA_NAO_ENCONTRADA")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            if (result.ErrorCode == "DISPUTA_EM_ANDAMENTO" || result.ErrorCode == "USUARIO_JA_E_TITULAR")
            {
                return Results.Conflict(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> SubmitDisputeCertificateAsync(
        [FromRoute] Guid id,
        [FromBody] ApiSubmitDisputeCertificateRequest request,
        IDisputeResolutionEngine disputeEngine,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var connection = ClaimConnectionMetadata.FromHttpContext(httpContext);
        var evidenceRequest = new DisputeEvidenceSubmissionRequest(
            DocumentFileHash: request.DocumentFileHash,
            AverbationDate: request.AverbationDate,
            Notes: request.Notes
        );

        var result = await disputeEngine.SubmitDisputeEvidenceAsync(id, userId.Value, evidenceRequest, connection, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "DISPUTA_NAO_ENCONTRADA")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetClaimStatusAsync(
        [FromRoute] Guid churchId,
        IClaimOrchestratorService orchestrator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue) return Results.Unauthorized();

        var result = await orchestrator.GetStatusAsync(churchId, userId.Value, ct);

        if (!result.IsSuccess)
        {
            return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }
}
