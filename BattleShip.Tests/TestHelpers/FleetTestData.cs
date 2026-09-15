using BattleShip.Models.Contracts;

namespace BattleShip.Tests.TestHelpers;

public static class FleetTestData
{
    public static PlaceFleetRequest ValidFleet => new(
    [
        new PlaceShipRequest("Porte-avions", 0, 0, true),
        new PlaceShipRequest("Croiseur", 0, 1, true),
        new PlaceShipRequest("Contre-torpilleur", 0, 2, true),
        new PlaceShipRequest("Sous-marin", 0, 3, true),
        new PlaceShipRequest("Torpilleur", 0, 4, true),
    ]);

    public static object ValidFleetJson => new
    {
        ships = new[]
        {
            new { name = "Porte-avions", x = 0, y = 0, horizontal = true },
            new { name = "Croiseur", x = 0, y = 1, horizontal = true },
            new { name = "Contre-torpilleur", x = 0, y = 2, horizontal = true },
            new { name = "Sous-marin", x = 0, y = 3, horizontal = true },
            new { name = "Torpilleur", x = 0, y = 4, horizontal = true },
        }
    };
}
