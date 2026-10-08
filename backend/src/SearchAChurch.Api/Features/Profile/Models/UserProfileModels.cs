namespace SearchAChurch.Api.Features.Profile.Models;

/// <summary>
/// DTO de resposta com os dados e preferências consolidadas do perfil de usuário.
/// </summary>
public record UserProfileResponse(
    Guid UserId,
    string Name,
    string Email,
    string? Denomination,
    string? WorshipStyle,
    IReadOnlyList<string> PreferredLanguages,
    double DefaultRadiusKm,
    IReadOnlyList<string> SelectedTags,
    bool IsConfigured
);

/// <summary>
/// DTO de payload para criação ou atualização de preferências de perfil de usuário.
/// </summary>
public record UpdateUserProfileRequest(
    string? Denomination,
    string? WorshipStyle,
    List<string>? PreferredLanguages,
    double DefaultRadiusKm,
    List<string>? TagCodes
);

/// <summary>
/// DTO de confirmação de encerramento de conta com anonimização sob a LGPD.
/// </summary>
public record DeleteAccountResponse(
    bool Success,
    string Message
);
