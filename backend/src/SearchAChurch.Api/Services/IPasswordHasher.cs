namespace SearchAChurch.Api.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    bool ValidatePasswordPolicy(string password, out string? errorMessage);
}
