using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SearchAChurch.Api.Authorization;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Features.Profile.Authorization;

namespace SearchAChurch.Api.Extensions;

public static class AuthenticationExtensions
{
    public const string ChurchRepresentativePolicy = "ChurchRepresentative";
    public const string VerifiedRepresentativePolicy = ProfileAuthorizationPolicies.RequireVerifiedRepresentative;
    public const string UserOwnershipPolicy = ProfileAuthorizationPolicies.RequireProfileOwnership;

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
        {
            throw new InvalidOperationException("JWT Secret must be at least 256 bits (32 bytes) long.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync("{\"error\":\"UNAUTHORIZED\",\"message\":\"Autenticação necessária.\"}");
                },
                OnForbidden = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var failureCode = context.HttpContext.Items["AuthorizationFailureCode"] as string;
                    var failureMsg = context.HttpContext.Items["AuthorizationFailureMessage"] as string;

                    if (failureCode == "ACESSO_NEGADO_PROPRIEDADE")
                    {
                        var msg = failureMsg ?? "Acesso negado. Você só pode gerenciar seu próprio perfil.";
                        return context.Response.WriteAsync($"{{\"error\":\"ACESSO_NEGADO_PROPRIEDADE\",\"message\":\"{msg}\"}}");
                    }

                    var defaultMsg = failureMsg ?? "Acesso negado. Apenas representantes verificados podem alterar perfis de igreja.";
                    var code = failureCode ?? "REPRESENTANTE_NAO_VERIFICADO";
                    return context.Response.WriteAsync($"{{\"error\":\"{code}\",\"message\":\"{defaultMsg}\"}}");
                }
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(ChurchRepresentativePolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ChurchRepresentativeRequirement());
            })
            .AddPolicy(ProfileAuthorizationPolicies.RequireVerifiedRepresentative, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new VerifiedRepresentativeRequirement());
            })
            .AddPolicy("VerifiedRepresentative", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new VerifiedRepresentativeRequirement());
            })
            .AddPolicy(ProfileAuthorizationPolicies.RequireProfileOwnership, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ProfileOwnershipRequirement());
            })
            .AddPolicy("UserOwnership", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ProfileOwnershipRequirement());
            });

        services.AddSingleton<IAuthorizationHandler, ChurchRepresentativeAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, OwnershipAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, VerifiedRepresentativeAuthorizationHandler>();

        return services;
    }
}
