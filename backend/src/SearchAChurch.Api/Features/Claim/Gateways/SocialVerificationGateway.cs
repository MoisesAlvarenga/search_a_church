using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SearchAChurch.Api.Features.Claim.Gateways;

public record SocialVerificationToken(
    string Token,
    DateTimeOffset ExpiresAt
);

public record SocialBioValidationResult(
    bool IsValid,
    string? Token,
    string? ErrorMessage
);

public interface ISocialVerificationGateway
{
    Task<SocialVerificationToken> GenerateTokenAsync(
        Guid claimId,
        string socialHandle,
        CancellationToken ct = default);

    Task<SocialBioValidationResult> ValidateBioTokenAsync(
        Guid claimId,
        string socialHandle,
        string tokenToVerify,
        CancellationToken ct = default);
}

/// <summary>
/// Gateway de validação por código em bio de redes sociais oficiais (Instagram, Facebook, X) - AD-015, CLAIM-03.
/// TTL de 48 horas armazenado em Redis com fallback em memória.
/// </summary>
public class SocialVerificationGateway : ISocialVerificationGateway
{
    public static readonly TimeSpan TokenTtl = TimeSpan.FromHours(48);
    private const string KeyPrefix = "claim:social:";

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<SocialVerificationGateway> _logger;
    private readonly TimeProvider _timeProvider;

    // Fallback in-memory para testes unitários ou quando Redis não estiver conectado
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> _inMemoryFallback = new();

    public SocialVerificationGateway(
        ILogger<SocialVerificationGateway> logger,
        IConnectionMultiplexer? redis = null,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _redis = redis;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SocialVerificationToken> GenerateTokenAsync(
        Guid claimId,
        string socialHandle,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socialHandle);

        if (claimId == Guid.Empty)
        {
            throw new ArgumentException("ClaimId cannot be empty", nameof(claimId));
        }

        // Gera token alfanumérico no formato SAC-XXXX-VERIFY
        var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        var token = $"SAC-{randomHex}-VERIFY";
        var expiresAt = _timeProvider.GetUtcNow().Add(TokenTtl);

        var key = $"{KeyPrefix}{claimId}";

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(key, token, TokenTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist social bio token in Redis. Falling back to in-memory cache.");
                _inMemoryFallback[key] = (token, expiresAt);
            }
        }
        else
        {
            _inMemoryFallback[key] = (token, expiresAt);
        }

        _logger.LogInformation("Social bio verification token generated for claim {ClaimId} on handle {Handle}: {Token}",
            claimId, socialHandle, token);

        return new SocialVerificationToken(token, expiresAt);
    }

    public async Task<SocialBioValidationResult> ValidateBioTokenAsync(
        Guid claimId,
        string socialHandle,
        string tokenToVerify,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socialHandle);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenToVerify);

        if (claimId == Guid.Empty)
        {
            throw new ArgumentException("ClaimId cannot be empty", nameof(claimId));
        }

        var key = $"{KeyPrefix}{claimId}";
        string? storedToken = null;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(key);
                if (val.HasValue)
                {
                    storedToken = val.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis lookup failed for social token. Falling back to in-memory.");
                if (_inMemoryFallback.TryGetValue(key, out var fallback) && fallback.ExpiresAt > _timeProvider.GetUtcNow())
                {
                    storedToken = fallback.Token;
                }
            }
        }
        else if (_inMemoryFallback.TryGetValue(key, out var fallback) && fallback.ExpiresAt > _timeProvider.GetUtcNow())
        {
            storedToken = fallback.Token;
        }

        if (string.IsNullOrWhiteSpace(storedToken))
        {
            return new SocialBioValidationResult(
                IsValid: false,
                Token: null,
                ErrorMessage: "Token de validação não encontrado ou expirado (TTL de 48h esgotado).");
        }

        var normalizedEntered = tokenToVerify.Trim().ToUpperInvariant();
        var normalizedStored = storedToken.Trim().ToUpperInvariant();

        if (normalizedEntered != normalizedStored)
        {
            return new SocialBioValidationResult(
                IsValid: false,
                Token: normalizedEntered,
                ErrorMessage: "O token fornecido não corresponde ao token ativo registrado para esta reivindicação.");
        }

        return new SocialBioValidationResult(
            IsValid: true,
            Token: normalizedStored,
            ErrorMessage: null);
    }
}
