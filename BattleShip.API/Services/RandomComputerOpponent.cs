using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class RandomComputerOpponent(Random random) : IComputerOpponent
{
    public (int X, int Y) ChooseShot(Game game)
    {
        var board = game.GetOpponentBoard(Participant.Player2);
        var candidates = new List<(int X, int Y)>();

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (!board.IsAlreadyTargeted(x, y))
                    candidates.Add((x, y));
            }
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("Aucune case disponible pour l'ordinateur.");

        return candidates[random.Next(candidates.Count)];
    }
}
