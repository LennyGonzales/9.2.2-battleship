namespace BattleShip.Models.Contracts;

public record CellDto(int X, int Y, VisibleCellState State);

public record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells);
