using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class GameEnginePlacementTests
{
    [Fact]
    public async Task CreateGameAsync_PlacesFleetWithoutOverlap()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(42);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal));

        AssertFleetIsValid(game.PlayerBoard);
        AssertFleetIsValid(game.ComputerBoard);
    }

    [Fact]
    public async Task CreateGameAsync_PlayerAndComputerBoardsAreDistinct()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(123);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(null);

        var playerCells = GetShipCells(game.PlayerBoard);
        var computerCells = GetShipCells(game.ComputerBoard);

        Assert.NotEqual(playerCells, computerCells);
    }

    [Fact]
    public async Task CreateGameAsync_InitialStateIsPlayerTurn()
    {
        var repository = new InMemoryGameRepository();
        var random = new Random(7);
        var engine = new GameEngine(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

        var game = await engine.CreateGameAsync(null);

        Assert.Equal(GameStatus.PlayerTurn, game.Status);
        Assert.Equal(PlayerSide.Player, game.CurrentTurn);
        Assert.Equal(0, game.ShotCount);
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
