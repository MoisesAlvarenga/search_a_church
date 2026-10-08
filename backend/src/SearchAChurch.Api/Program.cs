using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Endpoints;
using SearchAChurch.Api.Extensions;
using SearchAChurch.Api.Features.Claim.Endpoints;
using SearchAChurch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SearchAChurch.Api.Features.Maps.Configurations.GoogleMapsOptions>(
    builder.Configuration.GetSection(SearchAChurch.Api.Features.Maps.Configurations.GoogleMapsOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IRateLimiterService, RedisRateLimiter>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Maps.Services.IPlacesCacheService, SearchAChurch.Api.Features.Maps.Services.PlacesCacheService>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Maps.Services.IDeduplicationEngine, SearchAChurch.Api.Features.Maps.Services.DeduplicationEngine>();
builder.Services.AddHttpClient<SearchAChurch.Api.Features.Maps.Gateways.IGooglePlacesGateway, SearchAChurch.Api.Features.Maps.Gateways.GooglePlacesGateway>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Maps.Services.IMapOrchestratorService, SearchAChurch.Api.Features.Maps.Services.MapOrchestratorService>();
builder.Services.AddScoped<IAuditLogService, MarcoCivilAuditLogger>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Claim.Services.IClaimAuditLogService, SearchAChurch.Api.Features.Claim.Services.ClaimAuditLogService>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Services.IGeofencingService, SearchAChurch.Api.Features.Claim.Services.GeofencingService>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Gateways.ISocialVerificationGateway, SearchAChurch.Api.Features.Claim.Gateways.SocialVerificationGateway>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Gateways.IDomainEmailGateway, SearchAChurch.Api.Features.Claim.Gateways.DomainEmailGateway>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Gateways.IQsaValidationGateway, SearchAChurch.Api.Features.Claim.Gateways.QsaValidationGateway>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Gateways.ICartorioDocumentGateway, SearchAChurch.Api.Features.Claim.Gateways.CartorioDocumentGateway>();
builder.Services.AddSingleton<SearchAChurch.Api.Features.Claim.Services.IClaimNotificationService, SearchAChurch.Api.Features.Claim.Services.ClaimNotificationService>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Claim.Services.IDisputeResolutionEngine, SearchAChurch.Api.Features.Claim.Services.DisputeResolutionEngine>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Claim.Services.IClaimOrchestratorService, SearchAChurch.Api.Features.Claim.Services.ClaimOrchestratorService>();
builder.Services.AddHostedService<SearchAChurch.Api.Features.Claim.Services.ClaimTtlBackgroundService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<SearchAChurch.Api.Features.Profile.Services.ITagCatalogService, SearchAChurch.Api.Features.Profile.Services.TagCatalogService>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Profile.Services.IUserProfileService, SearchAChurch.Api.Features.Profile.Services.UserProfileService>();
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

app.MapPut("/churches/{id}", (Guid id) => Results.Ok(new { message = "Church updated" }))
   .RequireAuthorization(SearchAChurch.Api.Extensions.AuthenticationExtensions.ChurchRepresentativePolicy);

// Map Feature Endpoints
app.MapGroup("/auth").MapAuthEndpoints();
app.MapGroup("/map").MapMapEndpoints();
app.MapGroup("/claim").MapClaimEndpoints();

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
