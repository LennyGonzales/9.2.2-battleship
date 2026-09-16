using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class UsePowerUpRequestValidator : AbstractValidator<UsePowerUpRequest>
{
    private static readonly HashSet<string> AllowedShipNames =
        GameOptions.DefaultFleet.Select(s => s.Name).ToHashSet();

    public UsePowerUpRequestValidator()
    {
        RuleFor(x => x.ShipName)
            .NotEmpty()
            .Must(name => AllowedShipNames.Contains(name))
            .WithMessage("Nom de navire invalide.");

        When(x => x.ShipName is "Porte-avions" or "Torpilleur", () =>
        {
            RuleFor(x => x.Orientation).NotNull().WithMessage("Orientation requise.");
            RuleFor(x => x.Index).NotNull().GreaterThanOrEqualTo(0).WithMessage("Index de ligne/colonne requis.");
        });

        When(x => x.ShipName == "Torpilleur", () =>
        {
            RuleFor(x => x.EntryEdge).NotNull().WithMessage("Bord d'entree requis pour la torpille.");
        });

        When(x => x.ShipName == "Sous-marin", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 2 })
                .WithMessage("Le sous-marin cible exactement deux cases.");
        });

        When(x => x.ShipName == "Croiseur", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 2 })
                .WithMessage("Le croiseur cible exactement deux cases.");
        });

        When(x => x.ShipName == "Contre-torpilleur", () =>
        {
            RuleFor(x => x.Cells)
                .Must(cells => cells is { Count: 1 })
                .WithMessage("Le leurre cible exactement une case.");
        });

        RuleForEach(x => x.Cells).ChildRules(cell =>
        {
            cell.RuleFor(c => c.X).GreaterThanOrEqualTo(0).WithMessage("La colonne doit etre positive.");
            cell.RuleFor(c => c.Y).GreaterThanOrEqualTo(0).WithMessage("La ligne doit etre positive.");
        });
    }
}
