namespace SearchAChurch.Api.Features.Profile.Models;

/// <summary>
/// DTO representando um horário ou reunião da congregação.
/// </summary>
public record MeetingScheduleDto(
    Guid? Id,
    DayOfWeek DayOfWeek,
    string StartTime,
    string Description,
    string Language
);

/// <summary>
/// DTO de resposta completo com dados cadastrais, cultos e recursos da congregação.
/// </summary>
public record ChurchProfileResponse(
    Guid Id,
    string? PlaceId,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    string? Denomination,
    string? WorshipStyle,
    IReadOnlyList<string> Languages,
    string? Phone,
    string? Email,
    string? Website,
    string? SocialInstagram,
    string? SocialFacebook,
    string ClaimState,
    Guid? VerifiedRepresentativeUserId,
    bool IsActive,
    string ConcurrencyStamp,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MeetingScheduleDto> Schedules
);

/// <summary>
/// DTO de payload para atualização de dados cadastrais, eclesiásticos e cultos da igreja.
/// </summary>
public record UpdateChurchProfileRequest(
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    string? PlaceId = null,
    string? Denomination = null,
    string? WorshipStyle = null,
    List<string>? Languages = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? SocialInstagram = null,
    string? SocialFacebook = null,
    string? ConcurrencyStamp = null,
    List<string>? TagCodes = null,
    List<MeetingScheduleDto>? Schedules = null
);

/// <summary>
/// DTO para atualização do status de atividade da congregação (IsActive).
/// </summary>
public record UpdateChurchStatusRequest(
    bool IsActive,
    string? ConcurrencyStamp = null
);

/// <summary>
/// DTO de confirmação de alteração de status de atividade.
/// </summary>
public record ChurchStatusResponse(
    Guid ChurchId,
    bool IsActive,
    string Message
);
