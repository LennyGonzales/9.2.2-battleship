using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using System.Linq;

namespace BattleShip.API.Services;

public static class GameMapper
{
    public static GameDto ToDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt);

    public static GameCreatedDto ToCreatedDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt,
            game.Mode == GameMode.VsPlayer ? game.Player1Token : null);

    public static JoinGameDto ToJoinDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt,
            game.Player2Token!);

    public static BoardDto ToBoardDto(Board board, BoardOwner owner)
    {
        var cells = new List<CellDto>(board.Size * board.Size);
        for (var y = 0; y < board.Size; y++)
        {
            for (var x = 0; x < board.Size; x++)
            {
                cells.Add(new CellDto(x, y, ToVisibleCellState(board.GetCell(x, y), owner)));
            }
        }

        var ships = owner == BoardOwner.Player ? ToShipStatusDtos(board) : null;
        return new BoardDto(owner, board.Size, cells, ships);
    }

    private static IReadOnlyList<ShipStatusDto> ToShipStatusDtos(Board board) =>
        board.Ships.Select(s => new ShipStatusDto(s.Name, s.Length, s.PowerUpType, s.IsSunk, s.PowerUpUsed)).ToList();

    private static VisibleCellState ToVisibleCellState(CellState state, BoardOwner owner) => (owner, state) switch
    {
        (BoardOwner.Opponent, CellState.Ship) => VisibleCellState.Unknown,
        (BoardOwner.Opponent, CellState.Empty) => VisibleCellState.Unknown,
        (BoardOwner.Opponent, CellState.Miss) => VisibleCellState.Miss,
        // Le contrat de la grille joueur (swagger.yaml) n'expose pas d'etat Miss :
        // un tir adverse rate reste indiscernable d'une case jamais visee.
        (BoardOwner.Player, CellState.Miss) => VisibleCellState.Empty,
        (BoardOwner.Opponent, CellState.Obstacle) => VisibleCellState.Unknown,
        (_, CellState.Obstacle) => VisibleCellState.Obstacle,
        (_, CellState.ObstacleHit) => VisibleCellState.ObstacleHit,
        (BoardOwner.Player, CellState.Decoy) => VisibleCellState.Ship,
        (BoardOwner.Opponent, CellState.Decoy) => VisibleCellState.Unknown,
        (_, CellState.DecoyHit) => VisibleCellState.Hit,
        (_, CellState.Empty) => VisibleCellState.Empty,
        (_, CellState.Ship) => VisibleCellState.Ship,
        (_, CellState.Hit) => VisibleCellState.Hit,
        (_, CellState.Sunk) => VisibleCellState.Sunk,
        _ => VisibleCellState.Unknown,
    };
}
