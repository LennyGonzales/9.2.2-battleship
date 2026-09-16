using BattleShip.Grpc;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class GameStatsQueryValidator : AbstractValidator<GameStatsQuery>
{
    public GameStatsQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("L'identifiant de partie est requis.");

        RuleFor(x => x.GameId)
            .Must(BeValidGuid)
            .When(x => !string.IsNullOrWhiteSpace(x.GameId))
            .WithMessage("L'identifiant de partie doit etre un GUID valide.");
    }

    private static bool BeValidGuid(string gameId) => Guid.TryParse(gameId, out _);
}
