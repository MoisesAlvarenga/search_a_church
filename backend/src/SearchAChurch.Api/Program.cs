using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Endpoints;
using SearchAChurch.Api.Extensions;
using SearchAChurch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IRateLimiterService, RedisRateLimiter>();
builder.Services.AddScoped<IAuditLogService, MarcoCivilAuditLogger>();
builder.Services.AddTransient<SearchAChurch.Api.Filters.RateLimitFilter>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.RegisterRequest>, SearchAChurch.Api.Features.Auth.Validators.RegisterRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.IRegisterHandler, SearchAChurch.Api.Features.Auth.RegisterHandler>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.LoginRequest>, SearchAChurch.Api.Features.Auth.Validators.LoginRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.ILoginHandler, SearchAChurch.Api.Features.Auth.LoginHandler>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.RefreshTokenRequest>, SearchAChurch.Api.Features.Auth.Validators.RefreshTokenRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.IRefreshTokenHandler, SearchAChurch.Api.Features.Auth.RefreshTokenHandler>();

builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.ILogoutHandler, SearchAChurch.Api.Features.Auth.LogoutHandler>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.ForgotPasswordRequest>, SearchAChurch.Api.Features.Auth.Validators.ForgotPasswordRequestValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.ResetPasswordRequest>, SearchAChurch.Api.Features.Auth.Validators.ResetPasswordRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.IPasswordResetHandler, SearchAChurch.Api.Features.Auth.PasswordResetHandler>();

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck");

// Protected endpoints according to AUTH-01 (AD-007) and AUTH-04 (AD-009)
app.MapGet("/map/search", () => Results.Ok(new { message = "Map search results" }))
   .RequireAuthorization();

app.MapPut("/churches/{id}", (Guid id) => Results.Ok(new { message = "Church updated" }))
   .RequireAuthorization(SearchAChurch.Api.Extensions.AuthenticationExtensions.ChurchRepresentativePolicy);

// Map Auth Endpoints
app.MapGroup("/auth").MapAuthEndpoints();

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
