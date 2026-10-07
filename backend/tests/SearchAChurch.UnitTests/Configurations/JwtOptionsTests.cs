using FluentAssertions;
using SearchAChurch.Api.Configurations;

namespace SearchAChurch.UnitTests.Configurations;

[Trait("Category", "Unit")]
public class JwtOptionsTests
{
    [Fact]
    public void JwtOptions_DefaultValues_ShouldMatchSpecification()
    {
        // Act
        var options = new JwtOptions();

        // Assert
        options.Secret.Should().BeEmpty();
        options.Issuer.Should().BeEmpty();
        options.Audience.Should().BeEmpty();
        options.AccessTokenExpiryMinutes.Should().Be(15);
        options.RefreshTokenExpiryDays.Should().Be(60);
    }

    [Fact]
    public void JwtOptions_CustomValues_ShouldBeSetCorrectly()
    {
        // Act
        var options = new JwtOptions
        {
            Secret = "CustomSecretKeyMin256BitsLengthForUnitTestingPurposesOnly!",
            Issuer = "CustomIssuer",
            Audience = "CustomAudience",
            AccessTokenExpiryMinutes = 30,
            RefreshTokenExpiryDays = 90
        };

        // Assert
        options.Secret.Should().Be("CustomSecretKeyMin256BitsLengthForUnitTestingPurposesOnly!");
        options.Issuer.Should().Be("CustomIssuer");
        options.Audience.Should().Be("CustomAudience");
        options.AccessTokenExpiryMinutes.Should().Be(30);
        options.RefreshTokenExpiryDays.Should().Be(90);
    }
}
