using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEngineFleetTests
{
    [Fact]
    public async Task CreateGameAsync_PvE_ReturnsPlacingFleetWithEmptyPlayerBoard()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(42));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal, null));

        Assert.Equal(GameStatus.PlacingFleet, game.Status);
        Assert.Null(game.ActiveParticipant);
        Assert.NotNull(game.Player1Board);
        Assert.Null(game.Player2Board);
        Assert.Empty(game.Player1Board!.Ships);
    }

    [Fact]
    public async Task PlaceFleetAsync_PvE_StartsGameWithComputerFleet()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(7));

        var game = await engine.CreateGameAsync(null);
        var updated = await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        Assert.Equal(GameStatus.PlayerTurn, updated.Status);
        Assert.Equal(Participant.Player1, updated.ActiveParticipant);
        Assert.Equal(GameOptions.DefaultFleet.Length, updated.Player1Board!.Ships.Count);
        Assert.Equal(GameOptions.DefaultFleet.Length, updated.Player2Board!.Ships.Count);
    }

    [Fact]
    public async Task PlaceFleetAsync_PvE_SecondPlacement_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet));
    }

    [Fact]
    public async Task PlaceFleetAsync_PvP_Player1BeforeJoin_ThenJoin_ThenPlayer2_StartsGame()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(3));

        var created = await engine.CreateGameAsync(new CreateGameRequest(10, null, GameMode.VsPlayer));
        await engine.PlaceFleetAsync(created.Id, Participant.Player1, FleetTestData.ValidFleet);

        var joined = await engine.JoinGameAsync(created.Id);
        Assert.Equal(GameStatus.PlacingFleet, joined.Status);

        var started = await engine.PlaceFleetAsync(
            joined.Id,
            Participant.Player2,
            FleetTestData.ValidFleet);

        Assert.Equal(GameStatus.Player1Turn, started.Status);
        Assert.Equal(Participant.Player1, started.ActiveParticipant);
    }

    [Fact]
    public async Task FireShotAsync_BeforeFleetPlaced_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(5));

        var game = await engine.CreateGameAsync(null);

        await Assert.ThrowsAsync<GameConflictException>(() =>
            engine.FireShotAsync(game.Id, Participant.Player1, 0, 0));
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService(), new RandomComputerOpponent(random));
}
