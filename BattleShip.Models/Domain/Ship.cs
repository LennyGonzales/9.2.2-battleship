namespace BattleShip.Models.Domain;

public sealed class Ship
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required int Length { get; init; }
    public IReadOnlyList<(int X, int Y)> Cells { get; private set; } = [];
    public bool IsSunk { get; private set; }
    public bool PowerUpUsed { get; private set; }

    public PowerUpType PowerUpType => GameOptions.PowerUpByShipName[Name];
    public bool CanUsePowerUp => !IsSunk && !PowerUpUsed;

    internal void SetCells(IReadOnlyList<(int X, int Y)> cells) => Cells = cells;

    internal void MarkSunk() => IsSunk = true;

    public void MarkPowerUpUsed() => PowerUpUsed = true;
}
