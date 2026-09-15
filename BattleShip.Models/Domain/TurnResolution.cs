namespace BattleShip.Models.Domain;

public sealed record TurnResolution(
    ShotResolution? ShooterShot,
    ShotResolution? OpponentShot,
    GameStatus Status);

public sealed record ShotResolution(
    int X,
    int Y,
    ShotOutcome Outcome,
    string? SunkShipName);

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk
}
