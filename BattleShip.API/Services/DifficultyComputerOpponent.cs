using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class DifficultyComputerOpponent(Random random) : IComputerOpponent
{
    public (int X, int Y) ChooseShot(Game game)
    {
        var board = game.GetBoard(Participant.Player1);

        return game.Difficulty switch
        {
            Difficulty.Hard => PickHuntTarget(board, random)
                               ?? PickParityTarget(board, random)
                               ?? PickRandomTarget(board, random),
            Difficulty.Normal => PickParityTarget(board, random)
                                 ?? PickRandomTarget(board, random),
            _ => PickRandomTarget(board, random),
        };
    }

    private static (int X, int Y)? PickHuntTarget(Board board, Random random)
    {
        var candidates = new List<(int X, int Y)>();

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.GetCell(x, y) != CellState.Hit)
                    continue;

                foreach (var neighbor in GetNeighbors(x, y))
                {
                    if (board.IsWithinBounds(neighbor.X, neighbor.Y)
                        && !board.IsAlreadyTargeted(neighbor.X, neighbor.Y)
                        && !candidates.Contains(neighbor))
                    {
                        candidates.Add(neighbor);
                    }
                }
            }
        }

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    private static (int X, int Y)? PickParityTarget(Board board, Random random)
    {
        var candidates = EnumerateUntargeted(board)
            .Where(cell => (cell.X + cell.Y) % 2 == 0)
            .ToList();

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    private static (int X, int Y) PickRandomTarget(Board board, Random random)
    {
        var candidates = EnumerateUntargeted(board);

        if (candidates.Count == 0)
            throw new InvalidOperationException("Aucune case disponible pour l'ordinateur.");

        return candidates[random.Next(candidates.Count)];
    }

    private static List<(int X, int Y)> EnumerateUntargeted(Board board)
    {
        var candidates = new List<(int X, int Y)>();

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (!board.IsAlreadyTargeted(x, y))
                    candidates.Add((x, y));
            }
        }

        return candidates;
    }

    private static IEnumerable<(int X, int Y)> GetNeighbors(int x, int y)
    {
        yield return (x + 1, y);
        yield return (x - 1, y);
        yield return (x, y + 1);
        yield return (x, y - 1);
    }
}
