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

public sealed record ReconOutcome(Orientation Orientation, int Index, bool HasContact);

public sealed record PowerUpTurnResult(
    string ShipName,
    PowerUpType Type,
    Participant Actor,
    GameStatus Status,
    ReconOutcome? Recon,
    ShotResolution? Torpedo,
    IReadOnlyList<ShotResolution>? Cells);

public sealed record ComputerTurnResult(bool UsedPowerUp, ShotTurnResult? Shot, PowerUpTurnResult? PowerUp);
