using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEnginePlacementTests
{
    [Fact]
    public async Task CreateGameAsync_PvE_DoesNotPlaceFleets()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(42);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal));

        Assert.Equal(GameStatus.PlacingFleet, game.Status);
        Assert.Empty(game.Player1Board!.Ships);
        Assert.Null(game.Player2Board);
    }

    [Fact]
    public async Task PlaceFleetAsync_PvE_PlacesValidFleetsWithoutOverlap()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(42);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal));
        var updated = await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        AssertFleetIsValid(updated.Player1Board);
        AssertFleetIsValid(updated.Player2Board!);
    }

    [Fact]
    public async Task PlaceFleetAsync_PvE_PlayerAndComputerBoardsAreDistinct()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(123);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(null);
        var updated = await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        var playerCells = GetShipCells(updated.Player1Board);
        var computerCells = GetShipCells(updated.Player2Board!);

        Assert.NotEqual(playerCells, computerCells);
    }

    [Fact]
    public async Task PlaceFleetAsync_PvE_InitialStateIsPlayerTurn()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(7);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(null);
        var updated = await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        Assert.Equal(GameStatus.PlayerTurn, updated.Status);
        Assert.Equal(PlayerSide.Player, updated.CurrentTurn);
        Assert.Equal(0, updated.ShotCount);
    }

    private static void AssertFleetIsValid(Board board)
    {
        Assert.Equal(GameOptions.DefaultFleet.Length, board.Ships.Count);

        var occupied = new HashSet<(int X, int Y)>();
        foreach (var ship in board.Ships)
        {
            Assert.All(ship.Cells, cell =>
            {
                Assert.InRange(cell.X, 0, board.Size - 1);
                Assert.InRange(cell.Y, 0, board.Size - 1);
                Assert.True(occupied.Add(cell), $"Chevauchement detecte en ({cell.X},{cell.Y}).");
            });
        }
    }

    private static string GetShipCells(Board board) =>
        string.Join("|", board.Ships
            .SelectMany(s => s.Cells)
            .OrderBy(c => c.X)
            .ThenBy(c => c.Y)
            .Select(c => $"{c.X},{c.Y}"));
}
