using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Features.Auth;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Filters;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterAsync)
             .AddEndpointFilter<RateLimitFilter>("register")
             .Produces<AuthResponse>(StatusCodes.Status201Created)
             .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithName("Register")
             .WithSummary("Registra um novo usuário na plataforma")
             .WithOpenApi();

        group.MapPost("/login", LoginAsync)
             .AddEndpointFilter<RateLimitFilter>("login")
             .Produces<AuthResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithName("Login")
             .WithSummary("Autentica credenciais de usuário gerando par de tokens")
             .WithOpenApi();

        group.MapPost("/refresh", RefreshTokenAsync)
             .AddEndpointFilter<RateLimitFilter>("refresh")
             .Produces<AuthResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithName("RefreshToken")
             .WithSummary("Renova o par de tokens silenciosamente via rotação contínua (RTR)")
             .WithOpenApi();

        group.MapPost("/logout", LogoutAsync)
             .RequireAuthorization()
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status401Unauthorized)
             .WithName("Logout")
             .WithSummary("Encerra a sessão revogando o Refresh Token no banco de dados")
             .WithOpenApi();

        group.MapPost("/forgot-password", ForgotPasswordAsync)
             .AddEndpointFilter<RateLimitFilter>("forgot-password")
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithName("ForgotPassword")
             .WithSummary("Solicita código OTP de 6 dígitos para recuperação de senha")
             .WithOpenApi();

        group.MapPost("/reset-password", ResetPasswordAsync)
             .AddEndpointFilter<RateLimitFilter>("reset-password")
             .Produces(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithName("ResetPassword")
             .WithSummary("Redefine a senha utilizando o código OTP de 6 dígitos")
             .WithOpenApi();

        group.MapGet("/me", GetCurrentUserAsync)
             .RequireAuthorization()
             .Produces<UserSummaryResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status404NotFound)
             .WithName("GetCurrentUser")
             .WithSummary("Retorna o resumo do perfil do usuário autenticado")
             .WithOpenApi();

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        IRegisterHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        var result = await handler.HandleAsync(request, connectionContext, ct);

        if (!result.IsSuccess)
        {
            if (result.ValidationErrors != null)
            {
                return Results.ValidationProblem(result.ValidationErrors);
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Created("/auth/me", result.Value);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        ILoginHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        var result = await handler.HandleAsync(request, connectionContext, ct);

        if (!result.IsSuccess)
        {
            return Results.Json(
                new { error = result.ErrorCode, message = result.ErrorMessage },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> RefreshTokenAsync(
        [FromBody] RefreshTokenRequest request,
        IRefreshTokenHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        var result = await handler.HandleAsync(request, connectionContext, ct);

        if (!result.IsSuccess)
        {
            return Results.Json(
                new { error = result.ErrorCode, message = result.ErrorMessage },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest? request,
        ILogoutHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        await handler.HandleAsync(userId, request ?? new LogoutRequest(null), connectionContext, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        IPasswordResetHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        await handler.RequestOtpAsync(request, connectionContext, ct);

        return Results.Ok(new { message = "Se o e-mail estiver cadastrado, um código de verificação foi enviado." });
    }

    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        IPasswordResetHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var connectionContext = ClientConnectionContext.FromHttpContext(httpContext);
        var result = await handler.ResetPasswordAsync(request, connectionContext, ct);

        if (!result.IsSuccess)
        {
            if (result.ValidationErrors != null)
            {
                return Results.ValidationProblem(result.ValidationErrors);
            }
            return Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        }

        return Results.Ok(new { message = "Senha redefinida com sucesso." });
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        AppDbContext dbContext,
        CancellationToken ct)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return Results.NotFound(new { error = "USUARIO_NAO_ENCONTRADO", message = "Usuário não localizado." });
        }

        return Results.Ok(new UserSummaryResponse(
            user.Id,
            user.Email,
            user.Name,
            user.Role.ToString(),
            user.IsVerifiedRepresentative
        ));
    }
}
