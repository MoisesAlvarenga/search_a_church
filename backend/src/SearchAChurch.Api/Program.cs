using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditLogService, MarcoCivilAuditLogger>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.RegisterRequest>, SearchAChurch.Api.Features.Auth.Validators.RegisterRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.IRegisterHandler, SearchAChurch.Api.Features.Auth.RegisterHandler>();

builder.Services.AddScoped<FluentValidation.IValidator<SearchAChurch.Api.Features.Auth.Models.LoginRequest>, SearchAChurch.Api.Features.Auth.Validators.LoginRequestValidator>();
builder.Services.AddScoped<SearchAChurch.Api.Features.Auth.ILoginHandler, SearchAChurch.Api.Features.Auth.LoginHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck");

await app.RunAsync();
