namespace SearchAChurch.Api.Services;

public class PasswordHasher : IPasswordHasher
{
    public const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty or whitespace.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    public bool ValidatePasswordPolicy(string password, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "A senha não pode ser vazia.";
            return false;
        }

        if (password.Length < 8)
        {
            errorMessage = "A senha deve conter no mínimo 8 caracteres.";
            return false;
        }

        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            errorMessage = "A senha deve conter ao menos uma letra e um número.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
