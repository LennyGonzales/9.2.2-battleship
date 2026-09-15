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
        var engine = CreateEngine(repository, new Random(42));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal, null));

        AssertFleetIsValid(game.Player1Board!);
        AssertFleetIsValid(game.Player2Board!);
    }

    [Fact]
    public async Task CreateGameAsync_PlayerAndComputerBoardsAreDistinct()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(123));

        var game = await engine.CreateGameAsync(null);

        var playerCells = GetShipCells(game.Player1Board!);
        var computerCells = GetShipCells(game.Player2Board!);

        Assert.NotEqual(playerCells, computerCells);
    }

    [Fact]
    public async Task CreateGameAsync_InitialStateIsPlayerTurn()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(7));

        var game = await engine.CreateGameAsync(null);

        Assert.Equal(GameStatus.PlayerTurn, game.Status);
        Assert.Equal(Participant.Player1, game.ActiveParticipant);
        Assert.Equal(0, game.ShotCount);
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService());

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
