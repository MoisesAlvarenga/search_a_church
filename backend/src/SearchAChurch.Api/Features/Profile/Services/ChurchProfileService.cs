using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchAChurch.Api.Common;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Models;

namespace SearchAChurch.Api.Features.Profile.Services;

/// <summary>
/// Implementação do serviço de domínio para gestão eclesiástica de congregações (AD-004, AD-009, AD-026).
/// Assegura integridade e unicidade de PlaceId, controle de concorrência otimista e ciclo de vida de atividade.
/// </summary>
public class ChurchProfileService : IChurchProfileService
{
    private readonly AppDbContext _dbContext;
    private readonly ITagCatalogService _tagCatalogService;
    private readonly ILogger<ChurchProfileService> _logger;

    public ChurchProfileService(
        AppDbContext dbContext,
        ITagCatalogService tagCatalogService,
        ILogger<ChurchProfileService> logger)
    {
        _dbContext = dbContext;
        _tagCatalogService = tagCatalogService;
        _logger = logger;
    }

    public async Task<Result<ChurchProfileResponse>> GetChurchProfileAsync(Guid churchId, CancellationToken cancellationToken = default)
    {
        var church = await _dbContext.Churches
            .AsNoTracking()
            .Include(c => c.ChurchTags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.MeetingSchedules)
            .FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);

        if (church == null)
        {
            return Result<ChurchProfileResponse>.Failure("IGREJA_NAO_ENCONTRADA", "Igreja não encontrada.");
        }

        var tags = church.ChurchTags
            .Where(ct => ct.Tag != null && ct.Tag.IsActive)
            .OrderBy(ct => ct.Tag.DisplayOrder)
            .Select(ct => ct.Tag.Code)
            .ToList();

        var schedules = church.MeetingSchedules
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new MeetingScheduleDto(
                Id: s.Id,
                DayOfWeek: s.DayOfWeek,
                StartTime: s.StartTime,
                Description: s.Description,
                Language: s.Language))
            .ToList();

        return Result<ChurchProfileResponse>.Success(new ChurchProfileResponse(
            Id: church.Id,
            PlaceId: church.PlaceId,
            Name: church.Name,
            Address: church.FormattedAddress,
            Latitude: church.Latitude,
            Longitude: church.Longitude,
            Denomination: church.Denomination,
            WorshipStyle: church.WorshipStyle,
            Languages: church.Languages ?? new List<string> { "pt" },
            Phone: church.Phone,
            Email: church.Email,
            Website: church.Website,
            SocialInstagram: church.SocialInstagram,
            SocialFacebook: church.SocialFacebook,
            ClaimState: church.ClaimStatus.ToString(),
            VerifiedRepresentativeUserId: church.VerifiedByUserId,
            IsActive: church.IsActive,
            ConcurrencyStamp: church.ConcurrencyStamp,
            Tags: tags,
            Schedules: schedules
        ));
    }

    public async Task<Result<ChurchProfileResponse>> UpdateChurchProfileAsync(
        Guid churchId,
        UpdateChurchProfileRequest request,
        string? ifMatchHeader = null,
        CancellationToken cancellationToken = default)
    {
        var church = await _dbContext.Churches
            .Include(c => c.ChurchTags)
            .Include(c => c.MeetingSchedules)
            .FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);

        if (church == null)
        {
            return Result<ChurchProfileResponse>.Failure("IGREJA_NAO_ENCONTRADA", "Igreja não encontrada.");
        }

        // 1. Verificação de Concorrência Otimista (AD-026)
        var expectedStamp = ifMatchHeader?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(expectedStamp))
        {
            expectedStamp = request.ConcurrencyStamp?.Trim().Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(expectedStamp) &&
            !string.Equals(expectedStamp, church.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Result<ChurchProfileResponse>.Failure(
                "CONFLITO_CONCORRENCIA",
                "O perfil da congregação foi modificado concorrentemente por outro usuário. Atualize e tente novamente.");
        }

        // 2. Verificação de Colisão de PlaceId determinístico (AD-004, AD-026)
        if (!string.IsNullOrWhiteSpace(request.PlaceId))
        {
            var cleanPlaceId = request.PlaceId.Trim();
            if (!string.Equals(cleanPlaceId, church.PlaceId, StringComparison.OrdinalIgnoreCase))
            {
                var collisionExists = await _dbContext.Churches
                    .AnyAsync(c => c.Id != churchId && c.PlaceId == cleanPlaceId && c.IsActive, cancellationToken);

                if (collisionExists)
                {
                    return Result<ChurchProfileResponse>.Failure(
                        "PLACE_ID_JA_VINCULADO",
                        "O identificador place_id informado já está vinculado a outra congregação ativa no sistema.");
                }

                church.PlaceId = cleanPlaceId;
            }
        }

        // 3. Validação de Tags Oficiais via TagCatalogService
        if (request.TagCodes != null && request.TagCodes.Any())
        {
            var invalidTags = await _tagCatalogService.ValidateTagCodesAsync(request.TagCodes, cancellationToken);
            if (invalidTags.Any())
            {
                return Result<ChurchProfileResponse>.Failure(
                    "TAG_INVALIDA",
                    $"As seguintes tags são inválidas ou inativas: {string.Join(", ", invalidTags)}");
            }
        }

        // 4. Atualização dos atributos cadastrais e eclesiásticos
        church.Name = request.Name.Trim();
        church.FormattedAddress = request.Address.Trim();
        church.Latitude = request.Latitude;
        church.Longitude = request.Longitude;
        church.Denomination = request.Denomination?.Trim();
        church.WorshipStyle = request.WorshipStyle?.Trim();
        if (request.Languages != null && request.Languages.Count > 0)
        {
            church.Languages = request.Languages;
        }
        church.Phone = request.Phone?.Trim();
        church.Email = request.Email?.Trim();
        church.Website = request.Website?.Trim();
        church.SocialInstagram = request.SocialInstagram?.Trim();
        church.SocialFacebook = request.SocialFacebook?.Trim();
        church.UpdatedAt = DateTimeOffset.UtcNow;
        church.ConcurrencyStamp = Guid.NewGuid().ToString();

        // 5. Sincronização de Tags (ChurchTag)
        _dbContext.ChurchTags.RemoveRange(church.ChurchTags);

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
                _dbContext.ChurchTags.Add(new ChurchTag
                {
                    ChurchId = church.Id,
                    TagId = tag.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // 6. Sincronização de Horários de Culto (ChurchMeetingSchedule)
        _dbContext.ChurchMeetingSchedules.RemoveRange(church.MeetingSchedules);

        if (request.Schedules != null && request.Schedules.Any())
        {
            foreach (var schedule in request.Schedules)
            {
                _dbContext.ChurchMeetingSchedules.Add(new ChurchMeetingSchedule
                {
                    ChurchId = church.Id,
                    DayOfWeek = schedule.DayOfWeek,
                    StartTime = schedule.StartTime?.Trim() ?? string.Empty,
                    Description = schedule.Description?.Trim() ?? string.Empty,
                    Language = string.IsNullOrWhiteSpace(schedule.Language) ? "pt" : schedule.Language.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Perfil da congregação {ChurchId} ({Name}) atualizado com sucesso.", church.Id, church.Name);

        return await GetChurchProfileAsync(churchId, cancellationToken);
    }

    public async Task<Result<ChurchStatusResponse>> SetChurchStatusAsync(
        Guid churchId,
        bool isActive,
        string? concurrencyStamp = null,
        string? ifMatchHeader = null,
        CancellationToken cancellationToken = default)
    {
        var church = await _dbContext.Churches.FirstOrDefaultAsync(c => c.Id == churchId, cancellationToken);
        if (church == null)
        {
            return Result<ChurchStatusResponse>.Failure("IGREJA_NAO_ENCONTRADA", "Igreja não encontrada.");
        }

        var expectedStamp = ifMatchHeader?.Trim('\"', ' ') ?? concurrencyStamp;
        if (!string.IsNullOrWhiteSpace(expectedStamp) && !string.Equals(church.ConcurrencyStamp, expectedStamp, StringComparison.Ordinal))
        {
            return Result<ChurchStatusResponse>.Failure("CONFLITO_CONCORRENCIA", "O registro da congregação foi modificado concorrentemente.");
        }

        church.IsActive = isActive;
        church.UpdatedAt = DateTimeOffset.UtcNow;
        church.ConcurrencyStamp = Guid.NewGuid().ToString();

        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = isActive
            ? "Congregação ativada com sucesso."
            : "Congregação inativada temporariamente. Histórico comunitário preservado.";

        _logger.LogInformation("Status da congregação {ChurchId} alterado para IsActive={IsActive}.", church.Id, isActive);

        return Result<ChurchStatusResponse>.Success(new ChurchStatusResponse(
            ChurchId: church.Id,
            IsActive: church.IsActive,
            Message: message
        ));
    }
}
