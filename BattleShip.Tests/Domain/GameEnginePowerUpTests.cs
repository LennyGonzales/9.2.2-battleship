using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEnginePowerUpTests
{
    [Fact]
    public async Task UsePowerUpAsync_Recon_ReturnsContactWithoutRevealingCell()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 3, y: 4, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Porte-avions", Orientation.Row, 4, null, null);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Recon);
        Assert.True(result.Recon!.HasContact);
        Assert.Equal(GameStatus.ComputerTurn, result.Status);
    }

    [Fact]
    public async Task UsePowerUpAsync_Torpedo_ResolvesFirstCellFromChosenEdge()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 5, y: 3, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Torpilleur", Orientation.Row, 3, Edge.Low, null);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Torpedo);
        Assert.Equal(5, result.Torpedo!.X);
        Assert.Equal(ShotOutcome.Hit, result.Torpedo.Outcome);
    }

    [Fact]
    public async Task UsePowerUpAsync_TwinStrike_ResolvesBothCells()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(1, 0)]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Cells);
        Assert.Equal(2, result.Cells!.Count);
    }

    [Fact]
    public async Task UsePowerUpAsync_TwinStrike_NonAdjacentCells_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(5, 5)]);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.UsePowerUpAsync(game.Id, Participant.Player1, request));
    }

    [Fact]
    public async Task UsePowerUpAsync_DoubleStrike_ResolvesBothCellsAnywhere()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Croiseur", null, null, null, [new CellTarget(0, 0), new CellTarget(9, 9)]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.NotNull(result.Cells);
        Assert.Equal(2, result.Cells!.Count);
    }

    [Fact]
    public async Task UsePowerUpAsync_Decoy_PlacesDecoyOnOwnBoard()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        var ownShip = saved.Player1Board!.Ships.First(s => s.Name == "Contre-torpilleur");
        var (shipX, shipY) = ownShip.Cells[^1];
        var decoyTarget = new CellTarget(shipX + 1, shipY);

        var request = new UsePowerUpRequest("Contre-torpilleur", null, null, null, [decoyTarget]);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.Null(result.Recon);
        Assert.Null(result.Torpedo);
        Assert.Null(result.Cells);
        Assert.Equal(CellState.Decoy, saved.Player1Board.GetCell(decoyTarget.X, decoyTarget.Y));
    }

    [Fact]
    public async Task UsePowerUpAsync_AlreadyUsed_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest("Porte-avions", Orientation.Row, 0, null, null);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Status = GameStatus.PlayerTurn;
        await repository.SaveAsync(saved);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.UsePowerUpAsync(game.Id, Participant.Player1, request));
    }

    [Fact]
    public async Task UsePowerUpAsync_TwinStrike_IncrementsShotCountByTwo()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var request = new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(1, 0)]);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.ShotCount);
    }

    [Fact]
    public async Task UsePowerUpAsync_Torpedo_Hit_IncrementsShotCountByOne()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 5, y: 3, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Torpilleur", Orientation.Row, 3, Edge.Low, null);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.ShotCount);
    }

    [Fact]
    public async Task UsePowerUpAsync_Torpedo_EmptyLine_DoesNotIncrementShotCount()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = new Board(10);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Torpilleur", Orientation.Row, 3, Edge.Low, null);
        var result = await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        Assert.Null(result.Torpedo);

        saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(0, saved.ShotCount);
    }

    [Fact]
    public async Task UsePowerUpAsync_Recon_DoesNotIncrementShotCount()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        saved.Player2Board = CreateBoardWithShip(10, "Torpilleur", 2, x: 3, y: 4, horizontal: true);
        await repository.SaveAsync(saved);

        var request = new UsePowerUpRequest("Porte-avions", Orientation.Row, 4, null, null);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(0, saved.ShotCount);
    }

    [Fact]
    public async Task UsePowerUpAsync_Decoy_DoesNotIncrementShotCount()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));
        var game = await CreateReadyPveGameAsync(engine);

        var saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        var ownShip = saved.Player1Board!.Ships.First(s => s.Name == "Contre-torpilleur");
        var (shipX, shipY) = ownShip.Cells[^1];
        var decoyTarget = new CellTarget(shipX + 1, shipY);

        var request = new UsePowerUpRequest("Contre-torpilleur", null, null, null, [decoyTarget]);
        await engine.UsePowerUpAsync(game.Id, Participant.Player1, request);

        saved = await repository.GetByIdAsync(game.Id);
        Assert.NotNull(saved);
        Assert.Equal(0, saved.ShotCount);
    }

    private static async Task<Game> CreateReadyPveGameAsync(GameEngine engine)
    {
        var game = await engine.CreateGameAsync(null);
        return await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService(), new DifficultyComputerOpponent(random), random, ObstacleGenerationOptions.None);

    private static Board CreateBoardWithShip(int size, string name, int length, int x, int y, bool horizontal)
    {
        var board = new Board(size);
        var ship = new Ship { Name = name, Length = length };
        board.PlaceShip(ship, x, y, horizontal);
        return board;
    }
}
