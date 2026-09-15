namespace BattleShip.Models.Contracts;

public record PlaceShipRequest(string Name, int X, int Y, bool Horizontal);

public record PlaceFleetRequest(IReadOnlyList<PlaceShipRequest> Ships);
