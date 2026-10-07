namespace SearchAChurch.Api.Features.Auth.Models;

public record ResetPasswordRequest(
    string Email,
    string OtpCode,
    string NewPassword
);
