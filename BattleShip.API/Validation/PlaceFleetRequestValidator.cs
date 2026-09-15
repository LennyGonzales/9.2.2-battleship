using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class PlaceFleetRequestValidator : AbstractValidator<PlaceFleetRequest>
{
    private static readonly HashSet<string> AllowedShipNames =
        GameOptions.DefaultFleet.Select(s => s.Name).ToHashSet();

    public PlaceFleetRequestValidator()
    {
        RuleFor(x => x.Ships)
            .NotNull()
            .Must(ships => ships.Count == GameOptions.DefaultFleet.Length)
            .WithMessage($"La flotte doit contenir exactement {GameOptions.DefaultFleet.Length} navires.");

        RuleForEach(x => x.Ships).ChildRules(ship =>
        {
            ship.RuleFor(s => s.Name)
                .NotEmpty()
                .Must(name => AllowedShipNames.Contains(name))
                .WithMessage("Nom de navire invalide.");

            ship.RuleFor(s => s.X)
                .GreaterThanOrEqualTo(0)
                .WithMessage("La colonne doit etre positive.");

            ship.RuleFor(s => s.Y)
                .GreaterThanOrEqualTo(0)
                .WithMessage("La ligne doit etre positive.");
        });
    }
}
