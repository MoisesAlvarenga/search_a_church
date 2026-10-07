using FluentValidation;
using SearchAChurch.Api.Features.Auth.Models;

namespace SearchAChurch.Api.Features.Auth.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("O refreshToken é obrigatório.");

        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("O identificador do dispositivo (deviceId) é obrigatório.")
            .MaximumLength(100).WithMessage("O deviceId deve ter no máximo 100 caracteres.");
    }
}
