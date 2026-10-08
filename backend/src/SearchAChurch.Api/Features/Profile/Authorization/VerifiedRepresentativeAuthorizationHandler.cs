using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Profile.Authorization;

/// <summary>
/// Handler de autorização para representantes verificados de congregações (AD-009, AD-026, PROFILE-03).
/// Impõe que alterações e operações eclesiásticas sejam restritas ao VerifiedRepresentativeUserId
/// homologado com status Verified via church-profile-claim.
/// </summary>
public class VerifiedRepresentativeAuthorizationHandler : AuthorizationHandler<VerifiedRepresentativeRequirement>
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<VerifiedRepresentativeAuthorizationHandler>? _logger;

    public VerifiedRepresentativeAuthorizationHandler(
        AppDbContext dbContext,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<VerifiedRepresentativeAuthorizationHandler>? logger = null)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerifiedRepresentativeRequirement requirement)
    {
        var subClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out var currentUserId))
        {
            _logger?.LogWarning("Verified representative check failed: unauthenticated or invalid sub claim.");
            SetFailure(context, "REPRESENTANTE_NAO_VERIFICADO", "Usuário não autenticado ou identificador ausente.");
            return;
        }

        Church? church = null;

        if (context.Resource is Church resourceChurch)
        {
            church = resourceChurch;
        }
        else if (context.Resource is Guid churchId)
        {
            church = await _dbContext.Churches
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == churchId);
        }
        else if (context.Resource is string churchIdStr && Guid.TryParse(churchIdStr, out var parsedChurchId))
        {
            church = await _dbContext.Churches
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parsedChurchId);
        }
        else
        {
            var httpContext = _httpContextAccessor?.HttpContext ?? (context.Resource as HttpContext);
            if (httpContext != null)
            {
                var routeChurchId = ExtractChurchIdFromHttpContext(httpContext);
                if (routeChurchId.HasValue)
                {
                    church = await _dbContext.Churches
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == routeChurchId.Value);
                }
            }
        }

        // Caso uma igreja específica seja alvo da operação
        if (church != null)
        {
            var isVerified = church.DeletedAt == null &&
                             (church.ClaimStatus == ChurchClaimState.Verified || church.IsVerified);
            var isRepresentative = church.VerifiedByUserId == currentUserId;

            if (isVerified && isRepresentative)
            {
                _logger?.LogDebug(
                    "User {UserId} verified as legitimate representative of church {ChurchId}",
                    currentUserId, church.Id);
                context.Succeed(requirement);
                return;
            }

            _logger?.LogWarning(
                "User {UserId} rejected for church {ChurchId}. IsVerified={IsVerified}, IsRepresentative={IsRepresentative}",
                currentUserId, church.Id, isVerified, isRepresentative);

            SetFailure(
                context,
                "REPRESENTANTE_NAO_VERIFICADO",
                "Acesso negado. Apenas o representante verificado desta congregação pode realizar alterações.");
            return;
        }

        // Se o recurso foi um ID inexistente no banco, a autorização não pode ter sucesso
        if (context.Resource is Guid || context.Resource is string)
        {
            _logger?.LogWarning("Verified representative check failed: church resource not found in database.");
            SetFailure(context, "REPRESENTANTE_NAO_VERIFICADO", "Congregação não encontrada ou inativa.");
            return;
        }

        // Caso sem igreja específica identificada (ex: criação inicial de perfil de igreja ou checagem de papel)
        var isVerifiedRepClaim = context.User.FindFirst("is_verified_representative");
        if (isVerifiedRepClaim != null && bool.TryParse(isVerifiedRepClaim.Value, out var hasClaim) && hasClaim)
        {
            _logger?.LogDebug("User {UserId} authorized via is_verified_representative token claim", currentUserId);
            context.Succeed(requirement);
            return;
        }

        // Checar se o usuário possui ao menos uma igreja verificada como titular
        var userHasAnyVerifiedChurch = await _dbContext.Churches
            .AsNoTracking()
            .AnyAsync(c => c.VerifiedByUserId == currentUserId &&
                           c.DeletedAt == null &&
                           (c.ClaimStatus == ChurchClaimState.Verified || c.IsVerified));

        if (userHasAnyVerifiedChurch)
        {
            _logger?.LogDebug("User {UserId} authorized via verified church relationship in database", currentUserId);
            context.Succeed(requirement);
            return;
        }

        _logger?.LogWarning("User {UserId} is not a verified representative of any congregation", currentUserId);
        SetFailure(
            context,
            "REPRESENTANTE_NAO_VERIFICADO",
            "Acesso negado. Apenas representantes verificados podem alterar perfis de igreja.");
    }

    private static Guid? ExtractChurchIdFromHttpContext(HttpContext httpContext)
    {
        if (httpContext.Request.RouteValues.TryGetValue("churchId", out var cIdVal) &&
            Guid.TryParse(cIdVal?.ToString(), out var cId))
        {
            return cId;
        }

        if (httpContext.Request.RouteValues.TryGetValue("id", out var idVal) &&
            Guid.TryParse(idVal?.ToString(), out var id))
        {
            return id;
        }

        return null;
    }

    private void SetFailure(AuthorizationHandlerContext context, string code, string message)
    {
        var httpContext = _httpContextAccessor?.HttpContext ?? (context.Resource as HttpContext);
        if (httpContext != null)
        {
            httpContext.Items["AuthorizationFailureCode"] = code;
            httpContext.Items["AuthorizationFailureMessage"] = message;
        }
    }
}
