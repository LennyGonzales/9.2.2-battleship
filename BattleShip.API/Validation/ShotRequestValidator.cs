using BattleShip.Models.Contracts;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class ShotRequestValidator : AbstractValidator<ShotRequest>
{
    public ShotRequestValidator()
    {
        RuleFor(x => x.X)
            .GreaterThanOrEqualTo(0)
            .When(x => x.X.HasValue)
            .WithMessage("La colonne doit etre positive.");

        RuleFor(x => x.Y)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Y.HasValue)
            .WithMessage("La ligne doit etre positive.");
    }
}
