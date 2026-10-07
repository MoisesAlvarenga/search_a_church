using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth;

public interface ILogoutHandler
{
    Task<Result> HandleAsync(
        Guid userId,
        LogoutRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default);
}

public class LogoutHandler : ILogoutHandler
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogger;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        AppDbContext dbContext,
        ITokenService tokenService,
        IAuditLogService auditLogger,
        ILogger<LogoutHandler> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        Guid userId,
        LogoutRequest request,
        ClientConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        string? deviceId = null;

        // Se o Refresh Token foi enviado, revoga-o especificamente no banco de dados
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenHash = _tokenService.HashToken(request.RefreshToken.Trim());

            var tokenEntity = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(
                    rt => rt.TokenHash == tokenHash && rt.UserId == userId,
                    cancellationToken);

            if (tokenEntity != null)
            {
                deviceId = tokenEntity.DeviceId;
                if (tokenEntity.Status != RefreshTokenStatus.Revoked)
                {
                    tokenEntity.Status = RefreshTokenStatus.Revoked;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        // Registro de auditoria do encerramento voluntário da sessão (Marco Civil art. 15)
        await _auditLogger.LogEventAsync(
            "LOGOUT",
            userId,
            context,
            new { deviceId },
            cancellationToken);

        _logger.LogInformation("Logout efetuado com sucesso para o usuário {UserId}", userId);

        return Result.Success();
    }
}
