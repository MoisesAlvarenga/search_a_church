using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SearchAChurch.Api.Features.Profile.Authorization;
using SearchAChurch.Api.Features.Profile.Models;
using SearchAChurch.Api.Features.Profile.Services;

namespace SearchAChurch.Api.Features.Profile.Endpoints;

/// <summary>
/// Mapeamento de endpoints Minimal API para gestão de perfis de usuário, igreja e catálogo de tags (/profile/* e /tags/catalog).
/// Implementa regras de segurança, políticas de autorização granular e conformidade com LGPD (AD-008, AD-009, AD-026).
/// </summary>
public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        // 1. GET /profile/user
        group.MapGet("/user", GetUserProfileAsync)
             .RequireAuthorization(ProfileAuthorizationPolicies.RequireProfileOwnership)
             .Produces<UserProfileResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("GetUserProfile")
             .WithSummary("Consulta perfil e preferências do usuário autenticado");

        // 2. PUT /profile/user
        group.MapPut("/user", UpdateUserProfileAsync)
             .RequireAuthorization(ProfileAuthorizationPolicies.RequireProfileOwnership)
             .Produces<UserProfileResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status403Forbidden)
             .WithName("UpdateUserProfile")
             .WithSummary("Cria ou atualiza preferências persistidas do usuário autenticado");

        // 3. DELETE /profile/user
        group.MapDelete("/user", DeleteUserAccountAsync)
             .RequireAuthorization(ProfileAuthorizationPolicies.RequireProfileOwnership)
             .Produces<DeleteAccountResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status403Forbidden)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("DeleteUserAccount")
             .WithSummary("Encerra conta do usuário e anonimiza dados cadastrais sob o art. 18 da LGPD");

        // 4. GET /profile/church/{id}
        group.MapGet("/church/{id:guid}", GetChurchProfileAsync)
             .AllowAnonymous()
             .Produces<ChurchProfileResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("GetChurchProfile")
             .WithSummary("Consulta perfil público da congregação ou dados completos para representante");

        // 5. PUT /profile/church/{id}
        group.MapPut("/church/{id:guid}", UpdateChurchProfileAsync)
             .RequireAuthorization(ProfileAuthorizationPolicies.RequireVerifiedRepresentative)
             .Produces<ChurchProfileResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status403Forbidden)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status409Conflict)
             .WithName("UpdateChurchProfile")
             .WithSummary("Atualiza dados cadastrais, cultos e tags por representante verificado");

        // 6. PATCH /profile/church/{id}/status
        group.MapPatch("/church/{id:guid}/status", UpdateChurchStatusAsync)
             .RequireAuthorization(ProfileAuthorizationPolicies.RequireVerifiedRepresentative)
             .Produces<ChurchStatusResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status403Forbidden)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status409Conflict)
             .WithName("UpdateChurchStatus")
             .WithSummary("Alterna status ativo/inativo da congregação por representante verificado");

        return group;
    }

    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        // 7. GET /tags/catalog
        group.MapGet("/catalog", GetTagCatalogAsync)
             .AllowAnonymous()
             .Produces<TagCatalogResponse>(StatusCodes.Status200OK)
             .WithName("GetTagCatalog")
             .WithSummary("Consulta catálogo oficial de tags ativas agrupadas por categoria");

        return group;
    }

    private static Guid? ExtractUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.User.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static async Task<IResult> GetUserProfileAsync(
        HttpContext httpContext,
        IUserProfileService userProfileService,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var result = await userProfileService.GetUserProfileAsync(userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "USUARIO_NAO_ENCONTRADO")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateUserProfileAsync(
        [FromBody] UpdateUserProfileRequest request,
        HttpContext httpContext,
        IUserProfileService userProfileService,
        IAuthorizationService authorizationService,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var targetUserId = request.TargetUserId ?? userId.Value;
        var authResult = await authorizationService.AuthorizeAsync(
            httpContext.User,
            targetUserId,
            ProfileAuthorizationPolicies.RequireProfileOwnership);

        if (!authResult.Succeeded)
        {
            return Results.Json(
                new { error = "ACESSO_NEGADO_PROPRIEDADE", message = "Acesso negado. Você só pode gerenciar seu próprio perfil." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await userProfileService.UpsertUserProfileAsync(userId.Value, request, ct);
        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteUserAccountAsync(
        HttpContext httpContext,
        IUserProfileService userProfileService,
        IAuthorizationService authorizationService,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var authResult = await authorizationService.AuthorizeAsync(
            httpContext.User,
            userId.Value,
            ProfileAuthorizationPolicies.RequireProfileOwnership);

        if (!authResult.Succeeded)
        {
            return Results.Json(
                new { error = "ACESSO_NEGADO_PROPRIEDADE", message = "Acesso negado. Você só pode gerenciar seu próprio perfil." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await userProfileService.DeleteUserAccountAsync(userId.Value, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "USUARIO_NAO_ENCONTRADO")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetChurchProfileAsync(
        Guid id,
        HttpContext httpContext,
        IChurchProfileService churchProfileService,
        CancellationToken ct)
    {
        var result = await churchProfileService.GetChurchProfileAsync(id, ct);
        if (!result.IsSuccess || result.Value == null)
        {
            return Results.NotFound(new { error = "IGREJA_NAO_ENCONTRADA", message = result.ErrorMessage ?? "Igreja não encontrada." });
        }

        var church = result.Value;
        if (!church.IsActive)
        {
            var currentUserId = ExtractUserId(httpContext);
            var isRep = currentUserId.HasValue && church.VerifiedRepresentativeUserId == currentUserId.Value;
            if (!isRep)
            {
                return Results.NotFound(new { error = "IGREJA_NAO_ENCONTRADA", message = "Igreja não encontrada ou inativa." });
            }
        }

        return Results.Ok(church);
    }

    private static async Task<IResult> UpdateChurchProfileAsync(
        Guid id,
        [FromBody] UpdateChurchProfileRequest request,
        HttpContext httpContext,
        IChurchProfileService churchProfileService,
        IAuthorizationService authorizationService,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var authResult = await authorizationService.AuthorizeAsync(
            httpContext.User,
            id,
            ProfileAuthorizationPolicies.RequireVerifiedRepresentative);

        if (!authResult.Succeeded)
        {
            return Results.Json(
                new { error = "REPRESENTANTE_NAO_VERIFICADO", message = "Acesso negado. Apenas representantes verificados podem alterar perfis de igreja." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var ifMatch = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        var result = await churchProfileService.UpdateChurchProfileAsync(id, request, ifMatch, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "PLACE_ID_JA_VINCULADO" || result.ErrorCode == "CONFLITO_CONCORRENCIA")
            {
                return Results.Conflict(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            if (result.ErrorCode == "IGREJA_NAO_ENCONTRADA")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateChurchStatusAsync(
        Guid id,
        [FromBody] UpdateChurchStatusRequest request,
        HttpContext httpContext,
        IChurchProfileService churchProfileService,
        IAuthorizationService authorizationService,
        CancellationToken ct)
    {
        var userId = ExtractUserId(httpContext);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var authResult = await authorizationService.AuthorizeAsync(
            httpContext.User,
            id,
            ProfileAuthorizationPolicies.RequireVerifiedRepresentative);

        if (!authResult.Succeeded)
        {
            return Results.Json(
                new { error = "REPRESENTANTE_NAO_VERIFICADO", message = "Acesso negado. Apenas representantes verificados podem alterar perfis de igreja." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var ifMatch = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        var result = await churchProfileService.SetChurchStatusAsync(id, request.IsActive, request.ConcurrencyStamp, ifMatch, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "CONFLITO_CONCORRENCIA")
            {
                return Results.Conflict(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            if (result.ErrorCode == "IGREJA_NAO_ENCONTRADA")
            {
                return Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
            }

            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetTagCatalogAsync(
        ITagCatalogService tagCatalogService,
        CancellationToken ct)
    {
        var catalog = await tagCatalogService.GetActiveCatalogAsync(ct);
        return Results.Ok(catalog);
    }
}
