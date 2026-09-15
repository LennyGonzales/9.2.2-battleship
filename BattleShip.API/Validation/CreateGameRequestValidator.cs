using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.BoardSize)
            .InclusiveBetween(GameOptions.MinBoardSize, GameOptions.MaxBoardSize)
            .When(x => x.BoardSize.HasValue)
            .WithMessage($"La taille de grille doit etre comprise entre {GameOptions.MinBoardSize} et {GameOptions.MaxBoardSize}.");

        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .When(x => x.Difficulty.HasValue && x.Mode != GameMode.VsPlayer)
            .WithMessage("La difficulte doit etre Easy, Normal ou Hard.");

        RuleFor(x => x.Mode)
            .IsInEnum()
            .When(x => x.Mode.HasValue)
            .WithMessage("Le mode doit etre VsComputer ou VsPlayer.");
    }
}
