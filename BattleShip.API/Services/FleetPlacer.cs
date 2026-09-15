using BattleShip.Models.Domain;

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
}
