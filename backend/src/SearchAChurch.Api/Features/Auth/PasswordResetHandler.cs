using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth;

public interface IPasswordResetHandler
{
    Task<Result> RequestOtpAsync(
        ForgotPasswordRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);
}

public class PasswordResetHandler : IPasswordResetHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogger;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly ILogger<PasswordResetHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public PasswordResetHandler(
        AppDbContext dbContext,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogger,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        ILogger<PasswordResetHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result> RequestOtpAsync(
        ForgotPasswordRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // 1. Validação de payload
        var validationResult = await _forgotValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.ValidationFailure(validationResult.ToDictionary());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // 2. Mitigação de enumeração: resposta genérica de sucesso mesmo se e-mail não existir
        if (user == null)
        {
            _logger.LogInformation("Solicitação de OTP para e-mail não cadastrado: {Email}", normalizedEmail);

            await _auditLogger.LogEventAsync(
                "FORGOT_PASSWORD_REQUEST",
                null,
                context,
                new { email = normalizedEmail, found = false },
                cancellationToken);

            return Result.Success();
        }

        // 3. Invalida códigos OTP anteriores ainda não consumidos
        var existingOtps = await _dbContext.PasswordResetOtps
            .Where(o => o.UserId == user.Id && !o.IsConsumed)
            .ToListAsync(cancellationToken);

        foreach (var oldOtp in existingOtps)
        {
            oldOtp.IsConsumed = true;
        }

        // 4. Geração de código numérico aleatório de 6 dígitos
        var otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var otpCode = otpNumber.ToString("D6");
        var otpHash = _tokenService.HashToken(otpCode);
        var now = _timeProvider.GetUtcNow();

        var otpEntity = new PasswordResetOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = otpHash,
            AttemptCount = 0,
            IsConsumed = false,
            ExpiresAt = now.AddMinutes(15), // TTL de 15 minutos
            CreatedAt = now
        };

        await _dbContext.PasswordResetOtps.AddAsync(otpEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Em produção, este código seria despachado via serviço SMTP/SMS
        _logger.LogInformation("Código OTP de 6 dígitos gerado para {Email}: {OtpCode} (TTL 15 min)", user.Email, otpCode);

        // 5. Auditoria do evento
        await _auditLogger.LogEventAsync(
            "PASSWORD_RESET_OTP_REQUESTED",
            user.Id,
            context,
            new { email = user.Email },
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // 1. Validação de payload e força da nova senha
        var validationResult = await _resetValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.ValidationFailure(validationResult.ToDictionary());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return Result.Failure("INVALID_OTP", "Código de verificação inválido ou expirado.");
        }

        // 2. Busca o último OTP ativo não consumido
        var otpEntity = await _dbContext.PasswordResetOtps
            .Where(o => o.UserId == user.Id && !o.IsConsumed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpEntity == null)
        {
            return Result.Failure("INVALID_OTP", "Código de verificação inválido ou expirado.");
        }

        var now = _timeProvider.GetUtcNow();

        // 3. Verificação de expiração temporal (TTL 15 min)
        if (otpEntity.ExpiresAt < now)
        {
            otpEntity.IsConsumed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogEventAsync(
                "PASSWORD_RESET_FAILED",
                user.Id,
                context,
                new { reason = "otp_expired" },
                cancellationToken);

            return Result.Failure("OTP_EXPIRED", "O código de verificação expirou. Solicite um novo código.");
        }

        // 4. Verificação de limite máximo de 3 tentativas
        if (otpEntity.AttemptCount >= 3)
        {
            otpEntity.IsConsumed = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogEventAsync(
                "PASSWORD_RESET_BLOCKED",
                user.Id,
                context,
                new { reason = "max_attempts_exceeded" },
                cancellationToken);

            return Result.Failure("MAX_ATTEMPTS_EXCEEDED", "Limite de tentativas excedido para este código. Solicite um novo código.");
        }

        // 5. Incremento de tentativas e validação do hash
        otpEntity.AttemptCount++;
        var incomingHash = _tokenService.HashToken(request.OtpCode.Trim());

        if (incomingHash != otpEntity.OtpHash)
        {
            if (otpEntity.AttemptCount >= 3)
            {
                otpEntity.IsConsumed = true;
                await _dbContext.SaveChangesAsync(cancellationToken);

                await _auditLogger.LogEventAsync(
                    "PASSWORD_RESET_BLOCKED",
                    user.Id,
                    context,
                    new { reason = "max_attempts_reached_on_guess" },
                    cancellationToken);

                return Result.Failure("MAX_ATTEMPTS_EXCEEDED", "Limite de tentativas excedido para este código. Solicite um novo código.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogEventAsync(
                "PASSWORD_RESET_FAILED",
                user.Id,
                context,
                new { reason = "invalid_code", attempts = otpEntity.AttemptCount },
                cancellationToken);

            return Result.Failure("INVALID_OTP", $"Código de verificação inválido. Tentativa {otpEntity.AttemptCount} de 3.");
        }

        // 6. SUCESSO: Consome o OTP e atualiza a senha com BCrypt
        otpEntity.IsConsumed = true;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = now;

        // 7. DESCONECTAR DE OUTROS DISPOSITIVOS: Revogação sumária de TODAS as famílias de tokens do usuário
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.Status != RefreshTokenStatus.Revoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Status = RefreshTokenStatus.Revoked;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 8. Registro de auditoria do Marco Civil
        await _auditLogger.LogEventAsync(
            "PASSWORD_RESET_SUCCESS",
            user.Id,
            context,
            new { revokedSessionsCount = activeTokens.Count },
            cancellationToken);

        _logger.LogInformation("Senha redefinida com sucesso para o usuário {UserId}. {Count} sessões revogadas.", user.Id, activeTokens.Count);

        return Result.Success();
    }
}
