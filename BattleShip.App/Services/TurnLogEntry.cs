// BattleShip.App/Services/TurnLogEntry.cs
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed record TurnLogEntry(ShotResultDto? Shot, PowerUpResultDto? PowerUp)
{
    public static TurnLogEntry FromShot(ShotResultDto shot) => new(shot, null);
    public static TurnLogEntry FromPowerUp(PowerUpResultDto powerUp) => new(null, powerUp);
}
