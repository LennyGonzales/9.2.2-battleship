using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record CellTarget(int X, int Y);

public record UsePowerUpRequest(
    string ShipName,
    Orientation? Orientation,
    int? Index,
    Edge? EntryEdge,
    IReadOnlyList<CellTarget>? Cells);

public record ReconResultDto(Orientation Orientation, int Index, bool HasContact);

public record PowerUpResultDto(
    string ShipName,
    PowerUpType Type,
    PlayerSide Actor,
    GameStatus Status,
    ReconResultDto? Recon,
    ShotOutcomeDto? Torpedo,
    IReadOnlyList<ShotOutcomeDto>? Cells);

public record ComputerTurnResultDto(
    bool UsedPowerUp,
    ShotResultDto? Shot,
    PowerUpResultDto? PowerUp);
