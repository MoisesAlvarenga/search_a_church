using FluentValidation;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Services;

namespace SearchAChurch.Api.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IPasswordHasher passwordHasher)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.")
            .MaximumLength(255).WithMessage("O e-mail deve ter no máximo 255 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");

        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("O identificador do dispositivo (deviceId) é obrigatório.")
            .MaximumLength(100).WithMessage("O deviceId deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .Custom((password, context) =>
            {
                if (string.IsNullOrWhiteSpace(password)) return;

                if (!passwordHasher.ValidatePasswordPolicy(password, out var errorMessage))
                {
                    context.AddFailure("Password", errorMessage ?? "A senha não atende aos requisitos mínimos de segurança.");
                }
            });
    }
}
