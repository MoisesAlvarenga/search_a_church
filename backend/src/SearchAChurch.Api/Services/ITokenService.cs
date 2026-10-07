using System.Security.Claims;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user, Guid familyId);
    (string RawToken, string TokenHash) GenerateRefreshToken();
    string HashToken(string rawToken);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
