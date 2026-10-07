using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth;

public interface ILoginHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        LoginRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);
}

public class LoginHandler : ILoginHandler
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogger;
    private readonly IValidator<LoginRequest> _validator;
    private readonly ILogger<LoginHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public LoginHandler(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogService auditLogger,
        IValidator<LoginRequest> validator,
        ILogger<LoginHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
        _validator = validator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        LoginRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // 1. Validação de payload via FluentValidation
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Result<AuthResponse>.ValidationFailure(errors);
        }

        // 2. Normalização de e-mail e busca do usuário ativo
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // 3. Verificação de existência do usuário (sem vazar dados de existência)
        if (user == null)
        {
            _logger.LogWarning("Tentativa de login falhou: usuário inexistente para o e-mail {Email}", normalizedEmail);

            await _auditLogger.LogEventAsync(
                "LOGIN_FAILED",
                null,
                context,
                new { email = normalizedEmail, reason = "user_not_found" },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "INVALID_CREDENTIALS",
                "E-mail ou senha inválidos.");
        }

        // 4. Validação de senha com BCrypt
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Tentativa de login falhou: senha incorreta para o usuário {UserId}", user.Id);

            await _auditLogger.LogEventAsync(
                "LOGIN_FAILED",
                user.Id,
                context,
                new { email = user.Email, reason = "invalid_password" },
                cancellationToken);

            return Result<AuthResponse>.Failure(
                "INVALID_CREDENTIALS",
                "E-mail ou senha inválidos.");
        }

        // 5. Geração de nova cadeia de rotação (FamilyId) para a nova sessão
        var familyId = Guid.NewGuid();
        var accessToken = _tokenService.GenerateAccessToken(user, familyId);
        var (rawRefreshToken, refreshTokenHash) = _tokenService.GenerateRefreshToken();

        var now = _timeProvider.GetUtcNow();
        var clientIp = string.IsNullOrWhiteSpace(context.ClientIp)
            ? "127.0.0.1"
            : context.ClientIp.Trim();

        var userAgent = string.IsNullOrWhiteSpace(context.UserAgent)
            ? "Unknown"
            : context.UserAgent.Trim();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = refreshTokenHash,
            DeviceId = request.DeviceId.Trim(),
            Status = RefreshTokenStatus.Active,
            ExpiresAt = now.AddDays(60), // Expiração deslizante de 60 dias
            CreatedAt = now,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        // 6. Persistência do novo Refresh Token ativo
        await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 7. Registro de auditoria do evento de login bem-sucedido (Marco Civil art. 15)
        await _auditLogger.LogEventAsync(
            "LOGIN",
            user.Id,
            context,
            new
            {
                deviceId = refreshTokenEntity.DeviceId,
                familyId = familyId
            },
            cancellationToken);

        _logger.LogInformation("Login efetuado com sucesso para o usuário {UserId} com nova FamilyId {FamilyId}", user.Id, familyId);

        // 8. Resposta de autenticação formatada
        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: 900,
            TokenType: "Bearer",
            User: new UserDto(
                Id: user.Id,
                Email: user.Email,
                Name: user.Name,
                Role: user.Role.ToString(),
                IsVerifiedRepresentative: user.IsVerifiedRepresentative
            )
        );

        return Result<AuthResponse>.Success(response);
    }
}
