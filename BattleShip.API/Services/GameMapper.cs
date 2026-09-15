using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

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

        return new BoardDto(owner, board.Size, cells);
    }

    private static VisibleCellState ToVisibleCellState(CellState state, BoardOwner owner) => (owner, state) switch
    {
        (BoardOwner.Opponent, CellState.Ship) => VisibleCellState.Unknown,
        (BoardOwner.Opponent, CellState.Empty) => VisibleCellState.Unknown,
        (_, CellState.Empty) => VisibleCellState.Empty,
        (_, CellState.Ship) => VisibleCellState.Ship,
        (_, CellState.Hit) => VisibleCellState.Hit,
        (_, CellState.Sunk) => VisibleCellState.Sunk,
        _ => VisibleCellState.Unknown,
    };
}
