namespace SearchAChurch.Api.Features.Auth.Models;

public record LoginRequest(
    string Email,
    string Password,
    string DeviceId
);
