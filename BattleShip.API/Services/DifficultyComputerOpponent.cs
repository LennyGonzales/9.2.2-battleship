using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class DifficultyComputerOpponent(Random random) : IComputerOpponent
{
    private const double CasualPowerUpChance = 0.15;
    private const double HardPowerUpChance = 0.35;

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

    public UsePowerUpRequest? ChoosePowerUp(Game game)
    {
        var ownBoard = game.GetBoard(Participant.Player2);
        var opponentBoard = game.GetOpponentBoard(Participant.Player2);
        var eligible = ownBoard.Ships.Where(s => s.CanUsePowerUp).ToList();
        if (eligible.Count == 0)
            return null;

        var chance = game.Difficulty == Difficulty.Hard ? HardPowerUpChance : CasualPowerUpChance;
        if (random.NextDouble() >= chance)
            return null;

        var decoy = eligible.FirstOrDefault(s => s.PowerUpType == PowerUpType.Decoy);
        if (decoy is not null && random.Next(3) == 0)
        {
            var decoyRequest = BuildDecoyRequest(decoy, ownBoard);
            if (decoyRequest is not null)
                return decoyRequest;
        }

        var attackers = eligible.Where(s => s.PowerUpType != PowerUpType.Decoy).ToList();
        if (attackers.Count == 0)
            return null;

        var ship = attackers[random.Next(attackers.Count)];
        return ship.PowerUpType switch
        {
            PowerUpType.Recon => BuildReconRequest(ship, opponentBoard, random),
            PowerUpType.Torpedo => BuildTorpedoRequest(ship, opponentBoard, random),
            PowerUpType.TwinStrike => BuildTwinStrikeRequest(ship, opponentBoard, random),
            PowerUpType.DoubleStrike => BuildDoubleStrikeRequest(ship, opponentBoard, random),
            _ => null,
        };
    }

    private static UsePowerUpRequest? BuildDecoyRequest(Ship ship, Board ownBoard)
    {
        foreach (var (x, y) in ship.Cells)
        {
            if (ownBoard.GetCell(x, y) != CellState.Ship)
                continue; // damaged cell — TryPlaceDecoy requires adjacency to a still-intact Ship cell

            foreach (var (nx, ny) in GetNeighbors(x, y))
            {
                if (ownBoard.IsWithinBounds(nx, ny) && ownBoard.GetCell(nx, ny) == CellState.Empty)
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(nx, ny)]);
            }
        }
        return null;
    }

    private static UsePowerUpRequest BuildReconRequest(Ship ship, Board opponentBoard, Random random)
    {
        var orientation = random.Next(2) == 0 ? Orientation.Row : Orientation.Column;
        var index = random.Next(opponentBoard.Size);
        return new UsePowerUpRequest(ship.Name, orientation, index, null, null);
    }

    private static UsePowerUpRequest BuildTorpedoRequest(Ship ship, Board opponentBoard, Random random)
    {
        var orientation = random.Next(2) == 0 ? Orientation.Row : Orientation.Column;
        var index = random.Next(opponentBoard.Size);
        var edge = random.Next(2) == 0 ? Edge.Low : Edge.High;
        return new UsePowerUpRequest(ship.Name, orientation, index, edge, null);
    }

    private static UsePowerUpRequest? BuildTwinStrikeRequest(Ship ship, Board opponentBoard, Random random)
    {
        var pair = PickAdjacentUntargetedPair(opponentBoard, random);
        return pair is null
            ? null
            : new UsePowerUpRequest(ship.Name, null, null, null, [pair.Value.A, pair.Value.B]);
    }

    private static UsePowerUpRequest? BuildDoubleStrikeRequest(Ship ship, Board opponentBoard, Random random)
    {
        var candidates = EnumerateUntargeted(opponentBoard);
        if (candidates.Count < 2)
            return null;

        var first = candidates[random.Next(candidates.Count)];
        candidates.Remove(first);
        var second = candidates[random.Next(candidates.Count)];

        return new UsePowerUpRequest(
            ship.Name, null, null, null,
            [new CellTarget(first.X, first.Y), new CellTarget(second.X, second.Y)]);
    }

    private static (CellTarget A, CellTarget B)? PickAdjacentUntargetedPair(Board board, Random random)
    {
        var candidates = new List<(CellTarget A, CellTarget B)>();
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.IsAlreadyTargeted(x, y))
                    continue;

                if (board.IsWithinBounds(x + 1, y) && !board.IsAlreadyTargeted(x + 1, y))
                    candidates.Add((new CellTarget(x, y), new CellTarget(x + 1, y)));

                if (board.IsWithinBounds(x, y + 1) && !board.IsAlreadyTargeted(x, y + 1))
                    candidates.Add((new CellTarget(x, y), new CellTarget(x, y + 1)));
            }
        }

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    private static (int X, int Y)? PickHuntTarget(Board board, Random random)
    {
        var candidates = new List<(int X, int Y)>();

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.GetCell(x, y) is not (CellState.Hit or CellState.DecoyHit))
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
