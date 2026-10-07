namespace SearchAChurch.Api.Features.Auth.Models;

public record RegisterRequest(
    string Email,
    string Password,
    string Name,
    string DeviceId
);
