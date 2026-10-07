namespace SearchAChurch.Api.Features.Auth.Models;

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Role,
    bool IsVerifiedRepresentative
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    UserDto User
);
