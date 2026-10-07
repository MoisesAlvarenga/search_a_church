namespace SearchAChurch.Api.Features.Auth.Models;

public record LogoutRequest(
    string? RefreshToken
);
