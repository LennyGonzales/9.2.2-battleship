using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public static class ShotMapper
{
    public static ShotResultDto ToDto(Game game, ShotTurnResult result)
    {
        var shooter = game.MapToPlayerSide(result.Shooter)
            ?? throw new InvalidOperationException("Impossible de mapper le tireur.");

        return new ShotResultDto(
            ToOutcomeDto(result.Shot),
            shooter,
            result.Status);
    }

    private static ShotOutcomeDto ToOutcomeDto(ShotResolution resolution) =>
        new(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);
}
