using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class FleetPlacerTests
{
    private readonly FleetPlacer _placer = new(new Random(1));

    [Fact]
    public void PlaceFleetFromRequest_ValidFleet_PlacesAllShips()
    {
        var board = new Board(10);
        _placer.PlaceFleetFromRequest(board, FleetTestData.ValidFleet.Ships);

        Assert.Equal(GameOptions.DefaultFleet.Length, board.Ships.Count);
    }

    [Fact]
    public void PlaceFleetFromRequest_OverlappingShips_ThrowsFleetPlacementException()
    {
        var board = new Board(10);
        var overlapping = new PlaceFleetRequest(
        [
            new PlaceShipRequest("Porte-avions", 0, 0, true),
            new PlaceShipRequest("Croiseur", 1, 0, true),
            new PlaceShipRequest("Contre-torpilleur", 0, 2, true),
            new PlaceShipRequest("Sous-marin", 0, 3, true),
            new PlaceShipRequest("Torpilleur", 0, 4, true),
        ]);

        Assert.Throws<FleetPlacementException>(() =>
            _placer.PlaceFleetFromRequest(board, overlapping.Ships));
    }

    [Fact]
    public void PlaceFleetFromRequest_MissingShip_ThrowsFleetPlacementException()
    {
        var board = new Board(10);
        var incomplete = new PlaceFleetRequest(
        [
            new PlaceShipRequest("Porte-avions", 0, 0, true),
        ]);

        Assert.Throws<FleetPlacementException>(() =>
            _placer.PlaceFleetFromRequest(board, incomplete.Ships));
    }

    [Fact]
    public void PlaceFleetFromRequest_UnknownShip_ThrowsFleetPlacementException()
    {
        var board = new Board(10);
        var invalid = new PlaceFleetRequest(
        [
            new PlaceShipRequest("Navire-fantome", 0, 0, true),
            new PlaceShipRequest("Croiseur", 0, 1, true),
            new PlaceShipRequest("Contre-torpilleur", 0, 2, true),
            new PlaceShipRequest("Sous-marin", 0, 3, true),
            new PlaceShipRequest("Torpilleur", 0, 4, true),
        ]);

        Assert.Throws<FleetPlacementException>(() =>
            _placer.PlaceFleetFromRequest(board, invalid.Ships));
    }

    [Fact]
    public void PlaceFleetFromRequest_OutOfBounds_ThrowsFleetPlacementException()
    {
        var board = new Board(10);
        var outOfBounds = new PlaceFleetRequest(
        [
            new PlaceShipRequest("Porte-avions", 8, 0, true),
            new PlaceShipRequest("Croiseur", 0, 1, true),
            new PlaceShipRequest("Contre-torpilleur", 0, 2, true),
            new PlaceShipRequest("Sous-marin", 0, 3, true),
            new PlaceShipRequest("Torpilleur", 0, 4, true),
        ]);

        Assert.Throws<FleetPlacementException>(() =>
            _placer.PlaceFleetFromRequest(board, outOfBounds.Ships));
    }
}
