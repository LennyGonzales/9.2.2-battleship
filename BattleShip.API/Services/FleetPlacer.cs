using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;

namespace BattleShip.API.Services;

public sealed class FleetPlacer(Random random)
{
    public void PlaceFleetRandomly(Board board)
    {
        foreach (var (name, length) in GameOptions.DefaultFleet)
        {
            var ship = new Ship { Name = name, Length = length };
            var placed = false;

            for (var attempt = 0; attempt < GameOptions.MaxPlacementAttemptsPerShip; attempt++)
            {
                var x = random.Next(board.Size);
                var y = random.Next(board.Size);
                var horizontal = random.Next(2) == 0;

                if (!board.CanPlaceShip(ship, x, y, horizontal))
                    continue;

                board.PlaceShip(ship, x, y, horizontal);
                placed = true;
                break;
            }

            if (!placed)
                throw new InvalidOperationException($"Impossible de placer la flotte sur une grille {board.Size}x{board.Size}.");
        }
    }

    public void PlaceFleetFromRequest(Board board, IReadOnlyList<PlaceShipRequest> ships)
    {
        ValidateFleetComposition(ships);

        foreach (var placement in ships)
        {
            var spec = GameOptions.DefaultFleet.First(s => s.Name == placement.Name);
            var ship = new Ship { Name = spec.Name, Length = spec.Length };

            if (!board.CanPlaceShip(ship, placement.X, placement.Y, placement.Horizontal))
                throw new FleetPlacementException(
                    $"Impossible de placer {placement.Name} en ({placement.X},{placement.Y}).");

            board.PlaceShip(ship, placement.X, placement.Y, placement.Horizontal);
        }
    }

    private static void ValidateFleetComposition(IReadOnlyList<PlaceShipRequest> ships)
    {
        if (ships.Count != GameOptions.DefaultFleet.Length)
            throw new FleetPlacementException(
                $"La flotte doit contenir exactement {GameOptions.DefaultFleet.Length} navires.");

        var expectedNames = GameOptions.DefaultFleet.Select(s => s.Name).ToHashSet();
        var receivedNames = new HashSet<string>();

        foreach (var ship in ships)
        {
            if (!expectedNames.Contains(ship.Name))
                throw new FleetPlacementException($"Navire inconnu : {ship.Name}.");

            if (!receivedNames.Add(ship.Name))
                throw new FleetPlacementException($"Navire en double : {ship.Name}.");
        }

        foreach (var expected in expectedNames)
        {
            if (!receivedNames.Contains(expected))
                throw new FleetPlacementException($"Navire manquant : {expected}.");
        }
    }
}
