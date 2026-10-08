using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;

namespace SearchAChurch.Api.Features.Profile.Services;

/// <summary>
/// Implementação do serviço de domínio para gestão do perfil e preferências de usuário (AD-008, AD-020, AD-021, AD-026).
/// Garante persistência como baseline para a busca, controle estrito de limites de raio e conformidade com o art. 18 da LGPD.
/// </summary>
public class UserProfileService : IUserProfileService
{
    private const double MinRadiusKm = 1.0;
    private const double MaxRadiusKm = 100.0;
    private const double DefaultRadiusKmFallback = 10.0;

    private readonly AppDbContext _dbContext;
    private readonly ITagCatalogService _tagCatalogService;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        AppDbContext dbContext,
        ITagCatalogService tagCatalogService,
        ILogger<UserProfileService> logger)
    {
        _dbContext = dbContext;
        _tagCatalogService = tagCatalogService;
        _logger = logger;
    }

    public async Task<Result<UserProfileResponse>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return Result<UserProfileResponse>.Failure("USUARIO_NAO_ENCONTRADO", "Usuário não encontrado ou inativo.");
        }

        var profile = await _dbContext.UserProfiles
            .AsNoTracking()
            .Include(p => p.UserProfileTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return Result<UserProfileResponse>.Success(new UserProfileResponse(
                UserId: user.Id,
                Name: user.Name,
                Email: user.Email,
                Denomination: null,
                WorshipStyle: null,
                PreferredLanguages: new List<string> { "pt" },
                DefaultRadiusKm: DefaultRadiusKmFallback,
                SelectedTags: Array.Empty<string>(),
                IsConfigured: false
            ));
        }

        var selectedTags = profile.UserProfileTags
            .Where(pt => pt.Tag != null && pt.Tag.IsActive)
            .OrderBy(pt => pt.Tag.DisplayOrder)
            .Select(pt => pt.Tag.Code)
            .ToList();

        return Result<UserProfileResponse>.Success(new UserProfileResponse(
            UserId: user.Id,
            Name: user.Name,
            Email: user.Email,
            Denomination: profile.Denomination,
            WorshipStyle: profile.WorshipStyle,
            PreferredLanguages: profile.PreferredLanguages?.Count > 0 ? profile.PreferredLanguages : new List<string> { "pt" },
            DefaultRadiusKm: profile.DefaultRadiusKm,
            SelectedTags: selectedTags,
            IsConfigured: true
        ));
    }

    public async Task<Result<UserProfileResponse>> UpsertUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return Result<UserProfileResponse>.Failure("USUARIO_NAO_ENCONTRADO", "Usuário não encontrado.");
        }

        if (request.DefaultRadiusKm < MinRadiusKm || request.DefaultRadiusKm > MaxRadiusKm)
        {
            return Result<UserProfileResponse>.Failure(
                "RAIO_INVALIDO",
                $"O raio de busca padrão deve estar entre {MinRadiusKm:0.0} e {MaxRadiusKm:0.0} km.");
        }

        if (request.TagCodes != null && request.TagCodes.Any())
        {
            var invalidTags = await _tagCatalogService.ValidateTagCodesAsync(request.TagCodes, cancellationToken);
            if (invalidTags.Any())
            {
                return Result<UserProfileResponse>.Failure(
                    "TAG_INVALIDA",
                    $"As seguintes tags são inválidas ou inativas: {string.Join(", ", invalidTags)}");
            }
        }

        var profile = await _dbContext.UserProfiles
            .Include(p => p.UserProfileTags)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var languages = request.PreferredLanguages?.Count > 0
            ? request.PreferredLanguages
            : new List<string> { "pt" };

        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                Denomination = request.Denomination?.Trim(),
                WorshipStyle = request.WorshipStyle?.Trim(),
                PreferredLanguages = languages,
                DefaultRadiusKm = request.DefaultRadiusKm,
                IsAnonymous = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.UserProfiles.Add(profile);
        }
        else
        {
            profile.Denomination = request.Denomination?.Trim();
            profile.WorshipStyle = request.WorshipStyle?.Trim();
            profile.PreferredLanguages = languages;
            profile.DefaultRadiusKm = request.DefaultRadiusKm;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Sincroniza tags associadas
        _dbContext.UserProfileTags.RemoveRange(profile.UserProfileTags);

        if (request.TagCodes != null && request.TagCodes.Any())
        {
            var cleanCodes = request.TagCodes
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tagEntities = await _dbContext.TagCatalogs
                .Where(t => cleanCodes.Contains(t.Code) && t.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var tag in tagEntities)
            {
                _dbContext.UserProfileTags.Add(new UserProfileTag
                {
                    UserProfileId = profile.Id,
                    TagId = tag.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Perfil do usuário {UserId} atualizado com sucesso.", userId);

        return await GetUserProfileAsync(userId, cancellationToken);
    }

    public async Task<Result<DeleteAccountResponse>> DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return Result<DeleteAccountResponse>.Failure("USUARIO_NAO_ENCONTRADO", "Usuário não encontrado.");
        }

        var now = DateTimeOffset.UtcNow;

        // 1. Anonimização cadastral do usuário (LGPD art. 18)
        user.Name = "Usuário Anônimo";
        user.Email = $"deleted_{userId:N}@anonymized.searchachurch.org";
        user.PasswordHash = string.Empty;
        user.DeletedAt = now;
        user.UpdatedAt = now;

        // 2. Revogação e remoção imediata de Refresh Tokens e OTPs
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync(cancellationToken);
        _dbContext.RefreshTokens.RemoveRange(refreshTokens);

        var otps = await _dbContext.PasswordResetOtps
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);
        _dbContext.PasswordResetOtps.RemoveRange(otps);

        // 3. Despersonalização das preferências e soft delete do UserProfile
        var profile = await _dbContext.UserProfiles
            .Include(p => p.UserProfileTags)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile != null)
        {
            _dbContext.UserProfileTags.RemoveRange(profile.UserProfileTags);
            profile.DeletedAt = now;
            profile.UpdatedAt = now;
            profile.IsAnonymous = true;
            profile.Denomination = null;
            profile.WorshipStyle = null;
            profile.PreferredLanguages = new List<string>();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Conta do usuário {UserId} encerrada com sucesso sob conformidade com a LGPD.", userId);

        return Result<DeleteAccountResponse>.Success(new DeleteAccountResponse(
            Success: true,
            Message: "Conta encerrada com sucesso. Dados cadastrais anonimizados em conformidade com a LGPD."
        ));
    }
}
