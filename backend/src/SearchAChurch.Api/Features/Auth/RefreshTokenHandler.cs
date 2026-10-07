using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth;

public interface IRefreshTokenHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        RefreshTokenRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);
}

public class RefreshTokenHandler : IRefreshTokenHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogger;
    private readonly IValidator<RefreshTokenRequest> _validator;
    private readonly ILogger<RefreshTokenHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenHandler(
        AppDbContext dbContext,
        ITokenService tokenService,
        IAuditLogService auditLogger,
        IValidator<RefreshTokenRequest> validator,
        ILogger<RefreshTokenHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
        _validator = validator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RefreshTokenRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // 1. Validação de payload
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Result<AuthResponse>.ValidationFailure(errors);
        }

        // 2. Hash SHA-256 do token recebido para busca segura
        var tokenHash = _tokenService.HashToken(request.RefreshToken.Trim());

        var tokenEntity = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        // 3. Token não encontrado
        if (tokenEntity == null)
        {
            _logger.LogWarning("Tentativa de refresh com token inexistente.");

            await _auditLogger.LogEventAsync(
                "REFRESH_FAILED",
                null,
                context,
                new { reason = "token_not_found" },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "INVALID_TOKEN",
                "Refresh token inválido ou não encontrado.");
        }

        var now = _timeProvider.GetUtcNow();

        // 4. Verificação de expiração temporal
        if (tokenEntity.ExpiresAt < now)
        {
            _logger.LogInformation("Sessão expirada para o usuário {UserId} na FamilyId {FamilyId}", tokenEntity.UserId, tokenEntity.FamilyId);

            await _auditLogger.LogEventAsync(
                "REFRESH_EXPIRED",
                tokenEntity.UserId,
                context,
                new { familyId = tokenEntity.FamilyId },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "TOKEN_EXPIRED",
                "Sessão expirada por inatividade. Faça login novamente.");
        }

        // 5. Verificação de status Revogado
        if (tokenEntity.Status == RefreshTokenStatus.Revoked)
        {
            _logger.LogWarning("Tentativa de uso de token revogado na FamilyId {FamilyId}", tokenEntity.FamilyId);

            await _auditLogger.LogEventAsync(
                "REFRESH_FAILED",
                tokenEntity.UserId,
                context,
                new { familyId = tokenEntity.FamilyId, reason = "token_revoked" },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "TOKEN_REVOKED",
                "Sessão revogada. Faça login novamente.");
        }

        var clientIp = string.IsNullOrWhiteSpace(context.ClientIp)
            ? "127.0.0.1"
            : context.ClientIp.Trim();

        var userAgent = string.IsNullOrWhiteSpace(context.UserAgent)
            ? "Unknown"
            : context.UserAgent.Trim();

        // 6. DETECÇÃO DE VIOLAÇÃO (Breach Detection) vs TOLERÂNCIA DE 2 SEGUNDOS
        if (tokenEntity.Status == RefreshTokenStatus.Consumed)
        {
            var isWithinTolerance = tokenEntity.ConsumedAt.HasValue &&
                                    (now - tokenEntity.ConsumedAt.Value) <= TimeSpan.FromSeconds(2) &&
                                    tokenEntity.DeviceId == request.DeviceId.Trim() &&
                                    tokenEntity.ClientIp == clientIp;

            if (isWithinTolerance)
            {
                // Tolerância legítima para chamadas paralelas em trânsito no mesmo aparelho
                _logger.LogInformation(
                    "Requisição concorrente absorvida na tolerância de 2s para o usuário {UserId} no device {DeviceId}",
                    tokenEntity.UserId,
                    tokenEntity.DeviceId);

                var activeChildToken = await _dbContext.RefreshTokens
                    .Where(rt => rt.FamilyId == tokenEntity.FamilyId && rt.Status == RefreshTokenStatus.Active)
                    .OrderByDescending(rt => rt.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (activeChildToken != null)
                {
                    var replayAccessToken = _tokenService.GenerateAccessToken(tokenEntity.User, tokenEntity.FamilyId);

                    return Result<AuthResponse>.Success(new AuthResponse(
                        AccessToken: replayAccessToken,
                        RefreshToken: request.RefreshToken,
                        ExpiresIn: 900,
                        TokenType: "Bearer",
                        User: new UserDto(
                            Id: tokenEntity.User.Id,
                            Email: tokenEntity.User.Email,
                            Name: tokenEntity.User.Name,
                            Role: tokenEntity.User.Role.ToString(),
                            IsVerifiedRepresentative: tokenEntity.User.IsVerifiedRepresentative
                        )
                    ));
                }
            }

            // ALERTA DE SEGURANÇA: Reúso fora da janela de tolerância = VIOLAÇÃO (BREACH DETECTED)
            _logger.LogCritical(
                "ALERTA DE SEGURANÇA: Reúso de Refresh Token consumido detectado na FamilyId {FamilyId} do usuário {UserId}. Revogando toda a família!",
                tokenEntity.FamilyId,
                tokenEntity.UserId);

            // Revogação sumária de toda a cadeia de tokens vinculada à mesma FamilyId
            var familyTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.FamilyId == tokenEntity.FamilyId && rt.Status != RefreshTokenStatus.Revoked)
                .ToListAsync(cancellationToken);

            foreach (var token in familyTokens)
            {
                token.Status = RefreshTokenStatus.Revoked;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogEventAsync(
                "TOKEN_BREACH_DETECTED",
                tokenEntity.UserId,
                context,
                new
                {
                    familyId = tokenEntity.FamilyId,
                    deviceId = request.DeviceId,
                    attemptedTokenHash = tokenHash
                },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "TOKEN_BREACH_DETECTED",
                "Tentativa de violação detectada. Todas as sessões desta cadeia foram revogadas.");
        }

        // 7. ROTAÇÃO CONTÍNUA (RTR - Refresh Token Rotation) NO CAMINHO FELIZ
        // A) Queima o token atual
        tokenEntity.Status = RefreshTokenStatus.Consumed;
        tokenEntity.ConsumedAt = now;

        // B) Gera o novo par de tokens preservando a mesma FamilyId
        var familyId = tokenEntity.FamilyId;
        var accessToken = _tokenService.GenerateAccessToken(tokenEntity.User, familyId);
        var (rawNewRefreshToken, newRefreshTokenHash) = _tokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = tokenEntity.UserId,
            FamilyId = familyId,
            TokenHash = newRefreshTokenHash,
            DeviceId = request.DeviceId.Trim(),
            Status = RefreshTokenStatus.Active,
            ExpiresAt = now.AddDays(60), // Expiração deslizante renovada para +60 dias
            CreatedAt = now,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        await _dbContext.RefreshTokens.AddAsync(newRefreshTokenEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // C) Registro de auditoria do Marco Civil
        await _auditLogger.LogEventAsync(
            "REFRESH",
            tokenEntity.UserId,
            context,
            new
            {
                familyId = familyId,
                deviceId = newRefreshTokenEntity.DeviceId
            },
            cancellationToken);

        _logger.LogInformation("Refresh efetuado com sucesso para o usuário {UserId} na FamilyId {FamilyId}", tokenEntity.UserId, familyId);

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawNewRefreshToken,
            ExpiresIn: 900,
            TokenType: "Bearer",
            User: new UserDto(
                Id: tokenEntity.User.Id,
                Email: tokenEntity.User.Email,
                Name: tokenEntity.User.Name,
                Role: tokenEntity.User.Role.ToString(),
                IsVerifiedRepresentative: tokenEntity.User.IsVerifiedRepresentative
            )
        );

        return Result<AuthResponse>.Success(response);
    }
}
