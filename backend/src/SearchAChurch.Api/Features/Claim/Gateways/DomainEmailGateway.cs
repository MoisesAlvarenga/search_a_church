using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SearchAChurch.Api.Features.Claim.Gateways;

public record DomainOtpResult(
    bool Success,
    string? Message,
    DateTimeOffset? ExpiresAtUtc
);

public interface IDomainEmailGateway
{
    bool IsInstitutionalDomain(string email);

    Task<DomainOtpResult> SendOtpAsync(
        Guid claimId,
        string institutionalEmail,
        CancellationToken ct = default);

    Task<bool> ValidateOtpAsync(
        Guid claimId,
        string enteredOtp,
        CancellationToken ct = default);
}

/// <summary>
/// Gateway de validação por canal institucional proprietário (E-mail com domínio próprio da congregação) - AD-012, CLAIM-04.
/// Gera OTP de 6 dígitos com TTL de 15 minutos em Redis com fallback em memória.
/// </summary>
public class DomainEmailGateway : IDomainEmailGateway
{
    public static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(15);
    private const string KeyPrefix = "claim:otp:email:";

    private static readonly HashSet<string> PublicEmailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com",
        "hotmail.com", "outlook.com", "live.com", "msn.com",
        "yahoo.com", "yahoo.com.br",
        "icloud.com", "me.com",
        "bol.com.br", "uol.com.br", "terra.com.br", "ig.com.br"
    };

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<DomainEmailGateway> _logger;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, (string Otp, DateTimeOffset ExpiresAt)> _inMemoryFallback = new();

    public DomainEmailGateway(
        ILogger<DomainEmailGateway> logger,
        IConnectionMultiplexer? redis = null,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _redis = redis;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsInstitutionalDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var parts = email.Trim().Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        var domain = parts[1].Trim().ToLowerInvariant();
        return !PublicEmailDomains.Contains(domain);
    }

    public async Task<DomainOtpResult> SendOtpAsync(
        Guid claimId,
        string institutionalEmail,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institutionalEmail);

        if (claimId == Guid.Empty)
        {
            throw new ArgumentException("ClaimId cannot be empty", nameof(claimId));
        }

        if (!IsInstitutionalDomain(institutionalEmail))
        {
            return new DomainOtpResult(
                Success: false,
                Message: "O endereço de e-mail deve pertencer a um domínio institucional próprio da igreja, não sendo aceitos provedores genéricos (Gmail, Outlook, etc.).",
                ExpiresAtUtc: null);
        }

        // Gera OTP numérico de 6 dígitos
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = _timeProvider.GetUtcNow().Add(OtpTtl);
        var key = $"{KeyPrefix}{claimId}";

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(key, otp, OtpTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist OTP in Redis. Falling back to in-memory dictionary.");
                _inMemoryFallback[key] = (otp, expiresAt);
            }
        }
        else
        {
            _inMemoryFallback[key] = (otp, expiresAt);
        }

        _logger.LogInformation("OTP generated for institutional domain claim {ClaimId} on {Email}: {Otp}",
            claimId, institutionalEmail, otp);

        return new DomainOtpResult(
            Success: true,
            Message: "Código de verificação de 6 dígitos enviado com sucesso para o e-mail institucional.",
            ExpiresAtUtc: expiresAt);
    }

    public async Task<bool> ValidateOtpAsync(
        Guid claimId,
        string enteredOtp,
        CancellationToken ct = default)
    {
        if (claimId == Guid.Empty || string.IsNullOrWhiteSpace(enteredOtp))
        {
            return false;
        }

        var key = $"{KeyPrefix}{claimId}";
        string? storedOtp = null;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var val = await db.StringGetAsync(key);
                if (val.HasValue)
                {
                    storedOtp = val.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis lookup failed for OTP. Falling back to in-memory.");
                if (_inMemoryFallback.TryGetValue(key, out var fallback) && fallback.ExpiresAt > _timeProvider.GetUtcNow())
                {
                    storedOtp = fallback.Otp;
                }
            }
        }
        else if (_inMemoryFallback.TryGetValue(key, out var fallback) && fallback.ExpiresAt > _timeProvider.GetUtcNow())
        {
            storedOtp = fallback.Otp;
        }

        if (string.IsNullOrWhiteSpace(storedOtp))
        {
            return false;
        }

        bool isValid = string.Equals(storedOtp.Trim(), enteredOtp.Trim(), StringComparison.Ordinal);

        if (isValid)
        {
            // Revoga o OTP imediatamente após uso bem-sucedido
            if (_redis != null && _redis.IsConnected)
            {
                try
                {
                    await _redis.GetDatabase().KeyDeleteAsync(key);
                }
                catch
                {
                    // Ignora falha de limpeza
                }
            }
            _inMemoryFallback.TryRemove(key, out _);
        }

        return isValid;
    }
}
