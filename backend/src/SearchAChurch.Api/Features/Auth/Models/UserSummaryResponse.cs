namespace SearchAChurch.Api.Features.Auth.Models;

public record UserSummaryResponse(
    Guid Id,
    string Email,
    string Name,
    string Role,
    bool IsVerifiedRepresentative
);
