using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Features.Profile.Authorization;

/// <summary>
/// Handler de autorização estrita baseada em propriedade (Ownership) do perfil de usuário (AD-008, PROFILE-02).
/// Valida a correspondência entre a reivindicação 'sub' do JWT e o recurso acessado ou modificado.
/// </summary>
public class OwnershipAuthorizationHandler : AuthorizationHandler<ProfileOwnershipRequirement>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<OwnershipAuthorizationHandler>? _logger;

    public OwnershipAuthorizationHandler(
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<OwnershipAuthorizationHandler>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProfileOwnershipRequirement requirement)
    {
        var subClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subClaim) || !Guid.TryParse(subClaim, out var currentUserId))
        {
            _logger?.LogWarning("Ownership check failed: unauthenticated or invalid sub claim.");
            SetFailure(context, "ACESSO_NEGADO_PROPRIEDADE", "Usuário não autenticado ou identificador de usuário ausente.");
            return Task.CompletedTask;
        }

        Guid? targetUserId = null;

        if (context.Resource is Guid resourceGuid)
        {
            targetUserId = resourceGuid;
        }
        else if (context.Resource is string resourceStr && Guid.TryParse(resourceStr, out var parsedGuid))
        {
            targetUserId = parsedGuid;
        }
        else if (context.Resource is UserProfile userProfile)
        {
            targetUserId = userProfile.UserId;
        }
        else if (context.Resource is HttpContext httpContext)
        {
            targetUserId = ExtractUserIdFromHttpContext(httpContext) ?? currentUserId;
        }
        else if (_httpContextAccessor?.HttpContext != null)
        {
            targetUserId = ExtractUserIdFromHttpContext(_httpContextAccessor.HttpContext) ?? currentUserId;
        }
        else if (context.Resource == null)
        {
            // Sem recurso explícito (ex: rota /profile/user atuando sobre o próprio perfil autenticado)
            targetUserId = currentUserId;
        }

        if (targetUserId.HasValue && currentUserId == targetUserId.Value)
        {
            _logger?.LogDebug("Ownership verified successfully for user {UserId}", currentUserId);
            context.Succeed(requirement);
        }
        else
        {
            _logger?.LogWarning(
                "Ownership check failed: current user {CurrentUserId} does not match resource user {TargetUserId}",
                currentUserId, targetUserId);
            SetFailure(context, "ACESSO_NEGADO_PROPRIEDADE", "Acesso negado. Você só pode gerenciar seu próprio perfil.");
        }

        return Task.CompletedTask;
    }

    private static Guid? ExtractUserIdFromHttpContext(HttpContext httpContext)
    {
        if (httpContext.Request.RouteValues.TryGetValue("userId", out var uIdVal) &&
            Guid.TryParse(uIdVal?.ToString(), out var uId))
        {
            return uId;
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
