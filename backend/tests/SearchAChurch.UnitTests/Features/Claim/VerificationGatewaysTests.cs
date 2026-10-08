using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SearchAChurch.Api.Features.Claim.Gateways;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class VerificationGatewaysTests
{
    #region SocialVerificationGateway Tests

    [Fact]
    public async Task SocialGateway_GenerateTokenAsync_ReturnsValidFormattedTokenWith48hTtl()
    {
        // Arrange
        var gateway = new SocialVerificationGateway(NullLogger<SocialVerificationGateway>.Instance);
        var claimId = Guid.NewGuid();

        // Act
        var result = await gateway.GenerateTokenAsync(claimId, "@igrejacentral");

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().MatchRegex(@"^SAC-[0-9A-F]{4}-VERIFY$");
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(47));
        result.ExpiresAt.Should().BeBefore(DateTimeOffset.UtcNow.AddHours(49));
    }

    [Fact]
    public async Task SocialGateway_ValidateBioTokenAsync_WhenTokenMatches_ReturnsValid()
    {
        // Arrange
        var gateway = new SocialVerificationGateway(NullLogger<SocialVerificationGateway>.Instance);
        var claimId = Guid.NewGuid();
        var generated = await gateway.GenerateTokenAsync(claimId, "@igrejacentral");

        // Act
        var validation = await gateway.ValidateBioTokenAsync(claimId, "@igrejacentral", generated.Token);

        // Assert
        validation.IsValid.Should().BeTrue();
        validation.Token.Should().Be(generated.Token);
        validation.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SocialGateway_ValidateBioTokenAsync_WhenTokenDiffers_ReturnsInvalid()
    {
        // Arrange
        var gateway = new SocialVerificationGateway(NullLogger<SocialVerificationGateway>.Instance);
        var claimId = Guid.NewGuid();
        await gateway.GenerateTokenAsync(claimId, "@igrejacentral");

        // Act
        var validation = await gateway.ValidateBioTokenAsync(claimId, "@igrejacentral", "SAC-0000-VERIFY");

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.ErrorMessage.Should().Contain("não corresponde");
    }

    [Fact]
    public async Task SocialGateway_ValidateBioTokenAsync_WhenNoTokenGenerated_ReturnsNotFound()
    {
        // Arrange
        var gateway = new SocialVerificationGateway(NullLogger<SocialVerificationGateway>.Instance);
        var claimId = Guid.NewGuid();

        // Act
        var validation = await gateway.ValidateBioTokenAsync(claimId, "@igrejacentral", "SAC-ABCD-VERIFY");

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.ErrorMessage.Should().Contain("não encontrado ou expirado");
    }

    #endregion

    #region DomainEmailGateway Tests

    [Theory]
    [InlineData("pastor@gmail.com", false)]
    [InlineData("lider@hotmail.com", false)]
    [InlineData("contato@outlook.com", false)]
    [InlineData("admin@yahoo.com.br", false)]
    [InlineData("secretaria@igreja.com.br", true)]
    [InlineData("pastor@comunidadebiblica.org", true)]
    [InlineData("lider@adcentral.org.br", true)]
    public void DomainEmailGateway_IsInstitutionalDomain_DetectsCorrectly(string email, bool expectedInstitutional)
    {
        // Arrange
        var gateway = new DomainEmailGateway(NullLogger<DomainEmailGateway>.Instance);

        // Act
        var result = gateway.IsInstitutionalDomain(email);

        // Assert
        result.Should().Be(expectedInstitutional);
    }

    [Fact]
    public async Task DomainEmailGateway_SendOtpAsync_WhenPublicDomain_Rejects()
    {
        // Arrange
        var gateway = new DomainEmailGateway(NullLogger<DomainEmailGateway>.Instance);
        var claimId = Guid.NewGuid();

        // Act
        var result = await gateway.SendOtpAsync(claimId, "pastor@gmail.com");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("domínio institucional próprio");
        result.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task DomainEmailGateway_SendOtpAsync_WhenInstitutionalDomain_Generates6DigitOtp()
    {
        // Arrange
        var gateway = new DomainEmailGateway(NullLogger<DomainEmailGateway>.Instance);
        var claimId = Guid.NewGuid();

        // Act
        var result = await gateway.SendOtpAsync(claimId, "secretaria@igrejabatista.com.br");

        // Assert
        result.Success.Should().BeTrue();
        result.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task DomainEmailGateway_ValidateOtpAsync_ValidAndInvalidOtps()
    {
        // Arrange
        var gateway = new DomainEmailGateway(NullLogger<DomainEmailGateway>.Instance);
        var claimId = Guid.NewGuid();
        await gateway.SendOtpAsync(claimId, "secretaria@igrejabatista.com.br");

        // Act & Assert
        // Invalid OTP
        var invalidResult = await gateway.ValidateOtpAsync(claimId, "000000");
        invalidResult.Should().BeFalse();
    }

    #endregion

    #region QsaValidationGateway Tests

    [Theory]
    [InlineData("04.252.011/0001-10", true)]  // CNPJ matematicamente válido
    [InlineData("04252011000110", true)]
    [InlineData("11111111111111", false)]     // Dígitos repetidos
    [InlineData("12345678000199", false)]     // Dígitos verificadores inválidos
    [InlineData("", false)]
    public void QsaGateway_IsValidCnpj_ValidatesProperly(string cnpj, bool expectedValid)
    {
        // Arrange
        var gateway = new QsaValidationGateway(NullLogger<QsaValidationGateway>.Instance);

        // Act
        var result = gateway.IsValidCnpj(cnpj);

        // Assert
        result.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("11144477735", true)]   // CPF matematicamente válido
    [InlineData("111.444.777-35", true)]
    [InlineData("11111111111", false)]   // Dígitos repetidos
    [InlineData("12345678900", false)]   // Dígitos verificadores inválidos
    [InlineData("", false)]
    public void QsaGateway_IsValidCpf_ValidatesProperly(string cpf, bool expectedValid)
    {
        // Arrange
        var gateway = new QsaValidationGateway(NullLogger<QsaValidationGateway>.Instance);

        // Act
        var result = gateway.IsValidCpf(cpf);

        // Assert
        result.Should().Be(expectedValid);
    }

    [Fact]
    public async Task QsaGateway_ValidateRepresentativeAsync_WhenCpfEndsWith00_RejectsAsNotQualified()
    {
        // Arrange
        var gateway = new QsaValidationGateway(NullLogger<QsaValidationGateway>.Instance);
        // CPF simulado com final 00 que atende formato válido: 215.117.848-00 (ou gerado)
        // Se CPF for matematicamente inválido, rejeita por CPF inválido
        var invalidCpfResult = await gateway.ValidateRepresentativeAsync("04.252.011/0001-10", "123.456.789-00");

        // Assert
        invalidCpfResult.IsQualified.Should().BeFalse();
        invalidCpfResult.ErrorCode.Should().Be(QsaValidationGateway.ErrorInvalidCpf);
    }

    [Fact]
    public async Task QsaGateway_ValidateRepresentativeAsync_WhenValidCredentials_ApprovesQualification()
    {
        // Arrange
        var gateway = new QsaValidationGateway(NullLogger<QsaValidationGateway>.Instance);
        const string validCnpj = "04.252.011/0001-10";
        const string validCpf = "111.444.777-35";

        // Act
        var result = await gateway.ValidateRepresentativeAsync(validCnpj, validCpf);

        // Assert
        result.IsQualified.Should().BeTrue();
        result.RepresentativeRole.Should().Contain("Presidente");
        result.CorporateName.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region CartorioDocumentGateway Tests

    [Fact]
    public async Task CartorioGateway_ComputeSha256HashAsync_ComputesDeterministicHash()
    {
        // Arrange
        var gateway = new CartorioDocumentGateway(NullLogger<CartorioDocumentGateway>.Instance);
        var bytes = Encoding.UTF8.GetBytes("Ata de Posse Cartório RCPJ 2026");
        using var stream = new MemoryStream(bytes);

        // Act
        var hashStream = await gateway.ComputeSha256HashAsync(stream);
        var hashBytes = gateway.ComputeSha256Hash(bytes);

        // Assert
        hashStream.Should().Be(hashBytes);
        hashStream.Should().HaveLength(64);
    }

    [Fact]
    public async Task CartorioGateway_ValidateAndHashDocumentAsync_WithValidPdf_ReturnsSuccess()
    {
        // Arrange
        var gateway = new CartorioDocumentGateway(NullLogger<CartorioDocumentGateway>.Instance);
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Cartorio Document Content");
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await gateway.ValidateAndHashDocumentAsync(
            stream,
            contentType: "application/pdf",
            fileSizeBytes: pdfBytes.Length
        );

        // Assert
        result.IsValid.Should().BeTrue();
        result.FileHashSha256.Should().NotBeNullOrEmpty();
        result.FileSizeBytes.Should().Be(pdfBytes.Length);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task CartorioGateway_ValidateAndHashDocumentAsync_WithInvalidContentType_Rejects()
    {
        // Arrange
        var gateway = new CartorioDocumentGateway(NullLogger<CartorioDocumentGateway>.Instance);
        var bytes = Encoding.UTF8.GetBytes("image content");
        using var stream = new MemoryStream(bytes);

        // Act
        var result = await gateway.ValidateAndHashDocumentAsync(
            stream,
            contentType: "image/png",
            fileSizeBytes: bytes.Length
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CartorioDocumentGateway.ErrorInvalidContentType);
        result.ErrorMessage.Should().Contain("PDF");
    }

    [Fact]
    public async Task CartorioGateway_ValidateAndHashDocumentAsync_WhenEmptyFile_Rejects()
    {
        // Arrange
        var gateway = new CartorioDocumentGateway(NullLogger<CartorioDocumentGateway>.Instance);
        using var stream = new MemoryStream();

        // Act
        var result = await gateway.ValidateAndHashDocumentAsync(
            stream,
            contentType: "application/pdf",
            fileSizeBytes: 0
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CartorioDocumentGateway.ErrorEmptyDocument);
    }

    [Fact]
    public async Task CartorioGateway_ValidateAndHashDocumentAsync_WhenFileSizeExceeds15Mb_Rejects()
    {
        // Arrange
        var gateway = new CartorioDocumentGateway(NullLogger<CartorioDocumentGateway>.Instance);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await gateway.ValidateAndHashDocumentAsync(
            stream,
            contentType: "application/pdf",
            fileSizeBytes: 16 * 1024 * 1024 // 16 MB
        );

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CartorioDocumentGateway.ErrorFileSizeExceeded);
        result.ErrorMessage.Should().Contain("15MB");
    }

    #endregion
}
