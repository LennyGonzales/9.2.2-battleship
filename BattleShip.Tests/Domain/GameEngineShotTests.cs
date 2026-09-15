using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;

namespace BattleShip.Tests.Domain;

public class GameEngineShotTests
{
    [Fact]
    public async Task FireShotAsync_PlayerShot_SetsComputerTurn()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(42));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal, null));
        var result = await engine.FireShotAsync(game.Id, Participant.Player1, 0, 0);

        Assert.Equal(Participant.Player1, result.Shooter);
        Assert.Equal(GameStatus.ComputerTurn, result.Status);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.ShotCount);
        Assert.Equal(GameStatus.ComputerTurn, saved.Status);
    }

    [Fact]
    public async Task FireShotAsync_ComputerShot_ReturnsToPlayerTurn()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(7));

        var game = await engine.CreateGameAsync(null);
        await engine.FireShotAsync(game.Id, Participant.Player1, 0, 0);

        var result = await engine.FireShotAsync(game.Id, null, null, null);

        Assert.Equal(Participant.Player2, result.Shooter);
        Assert.Equal(GameStatus.PlayerTurn, result.Status);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.ShotCount);
    }

    [Fact]
    public async Task FireShotAsync_OnAlreadyTargetedCell_ThrowsConflictWithoutChangingShotCount()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));

        var game = await engine.CreateGameAsync(null);
        await engine.FireShotAsync(game.Id, Participant.Player1, 0, 0);
        await engine.FireShotAsync(game.Id, null, null, null);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        var shotCountBeforeRetry = saved.ShotCount;

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.FireShotAsync(game.Id, Participant.Player1, 0, 0));

        saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(shotCountBeforeRetry, saved.ShotCount);
    }

    [Fact]
    public async Task FireShotAsync_PlayerWins_OnSunkFleet()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new FixedComputerOpponent(9, 9);
        var engine = new GameEngine(
            repository,
            new FleetPlacer(new Random(1)),
            new PlayerTokenService(),
            opponent);

        var game = await engine.CreateGameAsync(new CreateGameRequest(5, Difficulty.Easy, null));
        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);

        saved.Player2Board = CreateSingleCellBoard();
        saved.Player1Board = new Board(5);
        await repository.SaveAsync(saved);

        var result = await engine.FireShotAsync(game.Id, Participant.Player1, 0, 0);

        Assert.Equal(GameStatus.PlayerWon, result.Status);
        Assert.Equal(ShotOutcome.Sunk, result.Shot.Outcome);
    }

    [Fact]
    public async Task FireShotAsync_ComputerTurnWithCoordinates_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(3));

        var game = await engine.CreateGameAsync(null);
        await engine.FireShotAsync(game.Id, Participant.Player1, 0, 0);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.FireShotAsync(game.Id, null, 1, 1));
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));

    private static Board CreateSingleCellBoard()
    {
        var board = new Board(5);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        return board;
    }

    private sealed class FixedComputerOpponent(int x, int y) : IComputerOpponent
    {
        public (int X, int Y) ChooseShot(Game game) => (x, y);
    }
}
