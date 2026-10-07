using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenService(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Secret) || Encoding.UTF8.GetByteCount(_options.Secret) < 32)
        {
            throw new InvalidOperationException("JWT Secret must be at least 256 bits (32 bytes) long.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateAccessToken(User user, Guid familyId)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Name),
            new("role", user.Role.ToString()),
            new("family_id", familyId.ToString()),
            new("is_verified_representative", user.IsVerifiedRepresentative.ToString().ToLowerInvariant())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public (string RawToken, string TokenHash) GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);

        var rawToken = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var tokenHash = HashToken(rawToken);

        return (rawToken, tokenHash);
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Token cannot be null or whitespace.", nameof(rawToken));
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuer = !string.IsNullOrWhiteSpace(_options.Issuer),
            ValidIssuer = _options.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_options.Audience),
            // nosemgrep: csharp.lang.security.ad.jwt-tokenvalidationparameters-no-expiry-validation.jwt-tokenvalidationparameters-no-expiry-validation
            ValidateLifetime = false, // Intentional: extracting claims from expired token during refresh
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
