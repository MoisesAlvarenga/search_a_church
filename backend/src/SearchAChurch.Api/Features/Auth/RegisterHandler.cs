using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth;

public interface IRegisterHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        RegisterRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);
}

public class RegisterHandler : IRegisterHandler
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogger;
    private readonly IValidator<RegisterRequest> _validator;
    private readonly ILogger<RegisterHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RegisterHandler(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogService auditLogger,
        IValidator<RegisterRequest> validator,
        ILogger<RegisterHandler> logger,
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
        RegisterRequest request,
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

        // 2. Normalização de e-mail e verificação de duplicidade (sem vazar dados de perfil existente)
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyExists = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyExists)
        {
            _logger.LogWarning("Tentativa de registro com e-mail já existente: {Email}", normalizedEmail);
            return Result<AuthResponse>.Failure(
                "EMAIL_ALREADY_EXISTS",
                "O e-mail informado já está em uso.");
        }

        // 3. Hashing da senha com BCrypt (Work Factor 12)
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var now = _timeProvider.GetUtcNow();

        // 4. Criação do novo usuário
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Name = request.Name.Trim(),
            Role = UserRole.User,
            IsVerifiedRepresentative = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 5. Emissão do par inicial de tokens e início da cadeia de rotação (FamilyId)
        var familyId = Guid.NewGuid();
        var accessToken = _tokenService.GenerateAccessToken(user, familyId);
        var (rawRefreshToken, refreshTokenHash) = _tokenService.GenerateRefreshToken();

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
            ExpiresAt = now.AddDays(60),
            CreatedAt = now,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        // 6. Persistência atômica no banco de dados
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 7. Registro de log de auditoria conforme Marco Civil da Internet (art. 15)
        await _auditLogger.LogEventAsync(
            "REGISTER",
            user.Id,
            context,
            new
            {
                email = user.Email,
                deviceId = refreshTokenEntity.DeviceId
            },
            cancellationToken);

        _logger.LogInformation("Usuário registrado com sucesso: {UserId}", user.Id);

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
