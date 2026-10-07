using FluentValidation;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(IPasswordHasher passwordHasher)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.")
            .MaximumLength(255).WithMessage("O e-mail deve ter no máximo 255 caracteres.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("O código OTP é obrigatório.")
            .Length(6).WithMessage("O código OTP deve conter exatamente 6 dígitos.")
            .Matches(@"^\d{6}$").WithMessage("O código OTP deve ser estritamente numérico.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A nova senha é obrigatória.")
            .Custom((password, context) =>
            {
                if (string.IsNullOrWhiteSpace(password)) return;

                if (!passwordHasher.ValidatePasswordPolicy(password, out var errorMessage))
                {
                    context.AddFailure("NewPassword", errorMessage ?? "A senha não atende aos requisitos mínimos de segurança.");
                }
            });
    }
}
