using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SearchAChurch.Api.Authorization;
using SearchAChurch.Api.Configurations;

namespace SearchAChurch.Api.Extensions;

public static class AuthenticationExtensions
{
    public const string ChurchRepresentativePolicy = "ChurchRepresentative";
    public const string VerifiedRepresentativePolicy = "VerifiedRepresentative";
    public const string UserOwnershipPolicy = "UserOwnership";

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
                    return context.Response.WriteAsync("{\"error\":\"REPRESENTANTE_NAO_VERIFICADO\",\"message\":\"Acesso negado. Apenas representantes verificados podem alterar perfis de igreja.\"}");
                }
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(ChurchRepresentativePolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ChurchRepresentativeRequirement());
            })
            .AddPolicy(VerifiedRepresentativePolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new ChurchRepresentativeRequirement());
            })
            .AddPolicy(UserOwnershipPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
            });

        services.AddSingleton<IAuthorizationHandler, ChurchRepresentativeAuthorizationHandler>();

        return services;
    }
}
