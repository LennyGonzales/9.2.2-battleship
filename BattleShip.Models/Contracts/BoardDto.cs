using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record CellDto(int X, int Y, VisibleCellState State);

public record ShipStatusDto(string Name, int Length, PowerUpType PowerUpType, bool IsSunk, bool PowerUpUsed);

public record BoardDto(BoardOwner Owner, int Size, IReadOnlyList<CellDto> Cells, IReadOnlyList<ShipStatusDto>? Ships = null);
