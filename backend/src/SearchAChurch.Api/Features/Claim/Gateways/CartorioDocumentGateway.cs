using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace SearchAChurch.Api.Features.Claim.Gateways;

public record CartorioDocumentValidationResult(
    bool IsValid,
    string? FileHashSha256,
    long FileSizeBytes,
    string? ErrorCode = null,
    string? ErrorMessage = null
)
{
    public static CartorioDocumentValidationResult Success(string hash, long sizeBytes) =>
        new(true, hash, sizeBytes);

    public static CartorioDocumentValidationResult Failure(string errorCode, string errorMessage) =>
        new(false, null, 0, errorCode, errorMessage);
}

public interface ICartorioDocumentGateway
{
    Task<string> ComputeSha256HashAsync(Stream stream, CancellationToken ct = default);
    string ComputeSha256Hash(byte[] bytes);

    Task<CartorioDocumentValidationResult> ValidateAndHashDocumentAsync(
        Stream stream,
        string contentType,
        long fileSizeBytes,
        CancellationToken ct = default);
}

/// <summary>
/// Gateway para processamento e conferência de integridade de documentos cartorários do RCPJ (AD-012, CLAIM-05).
/// Valida formato PDF, limite de tamanho e calcula hash criptográfico SHA-256.
/// </summary>
public class CartorioDocumentGateway : ICartorioDocumentGateway
{
    public const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB
    public const string RequiredContentType = "application/pdf";

    public const string ErrorInvalidContentType = "FORMATO_DOCUMENTO_INVALIDO";
    public const string ErrorFileSizeExceeded = "TAMANHO_DOCUMENTO_EXCEDIDO";
    public const string ErrorEmptyDocument = "DOCUMENTO_VAZIO";

    private readonly ILogger<CartorioDocumentGateway> _logger;

    public CartorioDocumentGateway(ILogger<CartorioDocumentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<string> ComputeSha256HashAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();
        byte[] hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public string ComputeSha256Hash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<CartorioDocumentValidationResult> ValidateAndHashDocumentAsync(
        Stream stream,
        string contentType,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (fileSizeBytes <= 0)
        {
            return CartorioDocumentValidationResult.Failure(
                ErrorEmptyDocument,
                "O arquivo do documento cartorial enviado está vazio.");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return CartorioDocumentValidationResult.Failure(
                ErrorFileSizeExceeded,
                $"O tamanho do arquivo ({fileSizeBytes / (1024.0 * 1024.0):F2}MB) excede o limite máximo permitido de 15MB.");
        }

        var normalizedType = contentType?.Trim().ToLowerInvariant() ?? "";
        if (!normalizedType.Contains("pdf") && normalizedType != RequiredContentType)
        {
            return CartorioDocumentValidationResult.Failure(
                ErrorInvalidContentType,
                "Apenas documentos comprobatórios em formato PDF são aceitos.");
        }

        var hash = await ComputeSha256HashAsync(stream, ct);

        _logger.LogInformation("Document successfully processed with SHA-256 {Hash} (Size: {Size} bytes)", hash, fileSizeBytes);

        return CartorioDocumentValidationResult.Success(hash, fileSizeBytes);
    }
}
