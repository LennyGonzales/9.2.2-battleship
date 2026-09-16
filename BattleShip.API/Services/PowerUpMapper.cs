using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public static class PowerUpMapper
{
    public static PowerUpResultDto ToDto(Game game, PowerUpTurnResult result)
    {
        var actor = game.MapToPlayerSide(result.Actor)
            ?? throw new InvalidOperationException("Impossible de mapper l'acteur.");

        return new PowerUpResultDto(
            result.ShipName,
            result.Type,
            actor,
            result.Status,
            result.Recon is { } recon ? new ReconResultDto(recon.Orientation, recon.Index, recon.HasContact) : null,
            result.Torpedo is { } torpedo ? ToOutcomeDto(torpedo) : null,
            result.Cells?.Select(ToOutcomeDto).ToList());
    }

    public static ComputerTurnResultDto ToDto(Game game, ComputerTurnResult result) =>
        new(
            result.UsedPowerUp,
            result.Shot is { } shot ? ShotMapper.ToDto(game, shot) : null,
            result.PowerUp is { } powerUp ? ToDto(game, powerUp) : null);

    private static ShotOutcomeDto ToOutcomeDto(ShotResolution resolution) =>
        new(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);
}
