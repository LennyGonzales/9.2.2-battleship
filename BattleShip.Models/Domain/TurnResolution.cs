namespace BattleShip.Models.Domain;

public sealed record ShotTurnResult(ShotResolution Shot, Participant Shooter, GameStatus Status);

public sealed record ShotResolution(
    int X,
    int Y,
    ShotOutcome Outcome,
    string? SunkShipName);

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk,
    Obstacle
}
