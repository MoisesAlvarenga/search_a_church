using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SearchAChurch.Api.Configurations;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Services;

[Trait("Category", "Unit")]
public class TokenServiceTests
{
    private const string ValidSecret = "SuperSecretKeyForSearchAChurchProjectWithMin256BitsLength!";
    private const string ValidIssuer = "SearchAChurch.Api";
    private const string ValidAudience = "SearchAChurch.Client";

    private readonly JwtOptions _validOptions = new()
    {
        Secret = ValidSecret,
        Issuer = ValidIssuer,
        Audience = ValidAudience,
        AccessTokenExpiryMinutes = 15,
        RefreshTokenExpiryDays = 60
    };

    private TokenService CreateSut(JwtOptions? options = null)
    {
        var opt = options ?? _validOptions;
        return new TokenService(Options.Create(opt));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TokenService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("short_secret_less_than_32_bytes")]
    public void Constructor_ShortOrEmptySecret_ThrowsInvalidOperationException(string secret)
    {
        // Arrange
        var options = new JwtOptions
        {
            Secret = secret,
            Issuer = ValidIssuer,
            Audience = ValidAudience
        };

        // Act
        var act = () => new TokenService(Options.Create(options));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*256 bits (32 bytes)*");
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ReturnsValidJwtWithAllClaims()
    {
        // Arrange
        var sut = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "pastor@example.com",
            Name = "Pr. João Silva",
            Role = UserRole.ChurchRep,
            IsVerifiedRepresentative = true
        };
        var familyId = Guid.NewGuid();

        // Act
        var tokenString = sut.GenerateAccessToken(user, familyId);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(tokenString).Should().BeTrue();

        var jwt = handler.ReadJwtToken(tokenString);
        jwt.Issuer.Should().Be(ValidIssuer);
        jwt.Audiences.Should().Contain(ValidAudience);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "family_id" && c.Value == familyId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == UserRole.ChurchRep.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "is_verified_representative" && c.Value == "true");
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == user.Name);

        // Expiration check: should be ~15 min in the future
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
        jwt.ValidTo.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void GenerateAccessToken_NullUser_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GenerateAccessToken(null!, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsHighEntropyUrlSafeTokenAndMatchingHash()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var (rawToken, tokenHash) = sut.GenerateRefreshToken();

        // Assert
        rawToken.Should().NotBeNullOrWhiteSpace();
        tokenHash.Should().NotBeNullOrWhiteSpace();

        // Base64URL safe (no '+', '/', '=')
        rawToken.Should().NotContain("+");
        rawToken.Should().NotContain("/");
        rawToken.Should().NotContain("=");

        // Hash matches deterministic SHA-256
        var expectedHash = sut.HashToken(rawToken);
        tokenHash.Should().Be(expectedHash);
        tokenHash.Length.Should().Be(64); // SHA-256 hex length is 64 characters
    }

    [Fact]
    public void GenerateRefreshToken_MultipleCalls_ProduceUniqueTokens()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var (rawToken1, tokenHash1) = sut.GenerateRefreshToken();
        var (rawToken2, tokenHash2) = sut.GenerateRefreshToken();

        // Assert
        rawToken1.Should().NotBe(rawToken2);
        tokenHash1.Should().NotBe(tokenHash2);
    }

    [Fact]
    public void HashToken_ValidInput_ReturnsDeterministicSha256Hex()
    {
        // Arrange
        var sut = CreateSut();
        const string input = "known-test-token-value";
        var expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var expectedHex = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        // Act
        var actualHex = sut.HashToken(input);

        // Assert
        actualHex.Should().Be(expectedHex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashToken_NullOrWhitespace_ThrowsArgumentException(string? input)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.HashToken(input!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ExpiredValidToken_ReturnsPrincipalWithExpectedClaims()
    {
        // Arrange
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "user@example.com"),
            new Claim("family_id", familyId.ToString()),
            new Claim("role", UserRole.User.ToString())
        };

        var expiredJwt = new JwtSecurityToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-15), // Expired 15 min ago
            signingCredentials: creds
        );

        var expiredTokenString = new JwtSecurityTokenHandler().WriteToken(expiredJwt);

        // Act
        var principal = sut.GetPrincipalFromExpiredToken(expiredTokenString);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be(userId.ToString());
        principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value.Should().Be("user@example.com");
        principal.FindFirst("family_id")?.Value.Should().Be(familyId.ToString());
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_InvalidSignature_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();
        const string foreignSecret = "DifferentSecretKeyForForeignSignatureValidationWith32Bytes!";
        var foreignKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(foreignSecret));
        var creds = new SigningCredentials(foreignKey, SecurityAlgorithms.HmacSha256);

        var foreignJwt = new JwtSecurityToken(
            issuer: ValidIssuer,
            audience: ValidAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(foreignJwt);

        // Act
        var principal = sut.GetPrincipalFromExpiredToken(tokenString);

        // Assert
        principal.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("malformed.jwt.token")]
    [InlineData("completely_invalid_token")]
    public void GetPrincipalFromExpiredToken_InvalidOrEmptyString_ReturnsNull(string? token)
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var principal = sut.GetPrincipalFromExpiredToken(token!);

        // Assert
        principal.Should().BeNull();
    }
}
