namespace BattleShip.Models.Domain;

public sealed class Ship
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required int Length { get; init; }
    public IReadOnlyList<(int X, int Y)> Cells { get; private set; } = [];

    internal void SetCells(IReadOnlyList<(int X, int Y)> cells) => Cells = cells;
}
