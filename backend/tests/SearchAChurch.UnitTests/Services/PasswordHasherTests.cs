using FluentAssertions;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Services;

[Trait("Category", "Unit")]
public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void HashPassword_ValidPassword_GeneratesValidBCryptHash()
    {
        // Arrange
        const string password = "StrongPassword123";

        // Act
        var hash = _sut.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2"); // BCrypt prefix ($2a$ or $2b$)
        hash.Should().Contain("$12$"); // Work Factor 12
        _sut.VerifyPassword(password, hash).Should().BeTrue();
    }

    [Fact]
    public void HashPassword_DifferentCalls_GeneratesUniqueSalts()
    {
        // Arrange
        const string password = "PasswordWithSalt123";

        // Act
        var hash1 = _sut.HashPassword(password);
        var hash2 = _sut.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2);
        _sut.VerifyPassword(password, hash1).Should().BeTrue();
        _sut.VerifyPassword(password, hash2).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void HashPassword_NullOrWhitespace_ThrowsArgumentException(string? invalidPassword)
    {
        // Act
        var act = () => _sut.HashPassword(invalidPassword!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VerifyPassword_MatchingPassword_ReturnsTrue()
    {
        // Arrange
        const string password = "CorrectPassword123";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        // Arrange
        const string password = "CorrectPassword123";
        var hash = _sut.HashPassword(password);

        // Act
        var result = _sut.VerifyPassword("WrongPassword123", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("SomePassword123", "not-a-valid-bcrypt-hash")]
    [InlineData("SomePassword123", "")]
    [InlineData("SomePassword123", null)]
    [InlineData("", "$2a$12$somevalidhashsample1234567890")]
    public void VerifyPassword_InvalidHashOrEmptyInput_ReturnsFalseWithoutThrowing(string password, string? hash)
    {
        // Act
        var result = _sut.VerifyPassword(password, hash!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("short1", "A senha deve conter no mínimo 8 caracteres.")]
    [InlineData("1234567", "A senha deve conter no mínimo 8 caracteres.")]
    [InlineData("pureletters", "A senha deve conter ao menos uma letra e um número.")]
    [InlineData("123456789", "A senha deve conter ao menos uma letra e um número.")]
    [InlineData("!@#$%^&*()", "A senha deve conter ao menos uma letra e um número.")]
    [InlineData("", "A senha não pode ser vazia.")]
    [InlineData("   ", "A senha não pode ser vazia.")]
    public void ValidatePasswordPolicy_InvalidPasswords_ReturnsFalseWithDescriptiveErrorMessage(string password, string expectedError)
    {
        // Act
        var isValid = _sut.ValidatePasswordPolicy(password, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("ValidPassword1")]
    [InlineData("Abcdefg8")]
    [InlineData("SuperSecureP@ssw0rd!")]
    [InlineData("1234567a")]
    public void ValidatePasswordPolicy_ValidPasswords_ReturnsTrueAndNullErrorMessage(string validPassword)
    {
        // Act
        var isValid = _sut.ValidatePasswordPolicy(validPassword, out var errorMessage);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }
}
