using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Integration;

[Trait("Category", "Integration")]
public class AuthenticationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateExpiredToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKeyForSearchAChurchProjectWithMin256BitsLength!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("email", "expired@test.com")
            }),
            NotBefore = DateTime.UtcNow.AddMinutes(-60),
            IssuedAt = DateTime.UtcNow.AddMinutes(-60),
            Expires = DateTime.UtcNow.AddMinutes(-30), // Expired 30 minutes ago
            Issuer = "SearchAChurch.Api",
            Audience = "SearchAChurch.Client",
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static string GenerateTokenWithWrongKey()
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("AnotherDifferentSecretKeyWithAtLeast32BytesLengthForTesting!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("email", "hacker@test.com")
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "SearchAChurch.Api",
            Audience = "SearchAChurch.Client",
            SigningCredentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static string GenerateTokenWithWrongIssuer()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretKeyForSearchAChurchProjectWithMin256BitsLength!"));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "WrongIssuer.Api",
            Audience = "SearchAChurch.Client",
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    [Fact]
    public async Task MapSearch_WithoutToken_Returns401Unauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", body);
    }

    [Fact]
    public async Task MapSearch_WithInvalidToken_Returns401Unauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "malformed.jwt.token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", body);
    }

    [Fact]
    public async Task MapSearch_WithExpiredToken_Returns401Unauthorized()
    {
        var expiredToken = GenerateExpiredToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MapSearch_WithWrongSigningKey_Returns401Unauthorized()
    {
        var invalidKeyToken = GenerateTokenWithWrongKey();
        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", invalidKeyToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MapSearch_WithWrongIssuer_Returns401Unauthorized()
    {
        var invalidIssuerToken = GenerateTokenWithWrongIssuer();
        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", invalidIssuerToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MapSearch_WithValidToken_Returns200Ok()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "validuser@church.org",
            Name = "Valid User",
            PasswordHash = "hash",
            Role = UserRole.User,
            IsVerifiedRepresentative = false
        };

        var validToken = tokenService.GenerateAccessToken(user, Guid.NewGuid());

        var request = new HttpRequestMessage(HttpMethod.Get, "/map/search");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Map search results", body);
    }

    [Fact]
    public async Task ChurchUpdate_WithoutToken_Returns401Unauthorized()
    {
        var churchId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Put, $"/churches/{churchId}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChurchUpdate_WithRegularUserToken_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var regularUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "regular@church.org",
            Name = "Regular Member",
            PasswordHash = "hash",
            Role = UserRole.User,
            IsVerifiedRepresentative = false
        };

        var token = tokenService.GenerateAccessToken(regularUser, Guid.NewGuid());
        var churchId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/churches/{churchId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("REPRESENTANTE_NAO_VERIFICADO", body);
    }

    [Fact]
    public async Task ChurchUpdate_WithVerifiedRepresentativeToken_Returns200Ok()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var repUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "pastor@church.org",
            Name = "Verified Pastor",
            PasswordHash = "hash",
            Role = UserRole.ChurchRep,
            IsVerifiedRepresentative = true
        };

        var token = tokenService.GenerateAccessToken(repUser, Guid.NewGuid());
        var churchId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/churches/{churchId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Church updated", body);
    }
}
