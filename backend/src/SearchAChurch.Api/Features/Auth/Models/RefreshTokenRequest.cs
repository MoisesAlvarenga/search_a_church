namespace SearchAChurch.Api.Features.Auth.Models;

public record RefreshTokenRequest(
    string RefreshToken,
    string DeviceId
);
