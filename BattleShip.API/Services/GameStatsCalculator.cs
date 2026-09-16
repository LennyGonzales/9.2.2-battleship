using BattleShip.Grpc;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public sealed class GameStatsCalculator
{
    public GameStatsReply ToReply(Game game) =>
        new()
        {
            GameId = game.Id.ToString(),
            PlayerShots = CountTargetedShots(game.Player2Board),
            ComputerShots = CountTargetedShots(game.Player1Board),
            PlayerHits = CountHits(game.Player2Board),
            ComputerHits = CountHits(game.Player1Board),
            Status = game.Status.ToString(),
        };

    private static int CountTargetedShots(Board? board)
    {
        if (board is null)
            return 0;

        var count = 0;
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.IsAlreadyTargeted(x, y))
                    count++;
            }
        }

        return count;
    }

    private static int CountHits(Board? board)
    {
        if (board is null)
            return 0;

        var count = 0;
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                var state = board.GetCell(x, y);
                if (state is CellState.Hit or CellState.Sunk)
                    count++;
            }
        }

        return count;
    }
}
