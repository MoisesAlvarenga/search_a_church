using FluentValidation;
using SearchAChurch.Api.Features.Auth.Models;

namespace SearchAChurch.Api.Features.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.")
            .MaximumLength(255).WithMessage("O e-mail deve ter no máximo 255 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.");

        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("O identificador do dispositivo (deviceId) é obrigatório.")
            .MaximumLength(100).WithMessage("O deviceId deve ter no máximo 100 caracteres.");
    }
}
