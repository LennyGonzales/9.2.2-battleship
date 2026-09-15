using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;

namespace BattleShip.Tests.Domain;

public class GameEnginePvpTests
{
    [Fact]
    public async Task CreateGameAsync_VsPlayer_ReturnsWaitingWithPlayer1Token()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(42));

        var game = await engine.CreateGameAsync(new CreateGameRequest(10, null, GameMode.VsPlayer));

        Assert.Equal(GameMode.VsPlayer, game.Mode);
        Assert.Equal(GameStatus.Waiting, game.Status);
        Assert.Null(game.ActiveParticipant);
        Assert.Null(game.Player1Board);
        Assert.Null(game.Player2Board);
        Assert.False(string.IsNullOrWhiteSpace(game.Player1Token));
        Assert.Null(game.Player2Token);
    }

    [Fact]
    public async Task JoinGameAsync_PlacesFleetsAndStartsPlayer1Turn()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(7));

        var created = await engine.CreateGameAsync(new CreateGameRequest(10, null, GameMode.VsPlayer));
        var joined = await engine.JoinGameAsync(created.Id);

        Assert.Equal(GameStatus.Player1Turn, joined.Status);
        Assert.Equal(Participant.Player1, joined.ActiveParticipant);
        Assert.NotNull(joined.Player1Board);
        Assert.NotNull(joined.Player2Board);
        Assert.False(string.IsNullOrWhiteSpace(joined.Player2Token));
        Assert.Equal(GameOptions.DefaultFleet.Length, joined.Player1Board!.Ships.Count);
        Assert.Equal(GameOptions.DefaultFleet.Length, joined.Player2Board!.Ships.Count);
    }

    [Fact]
    public async Task JoinGameAsync_WhenAlreadyJoined_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(1));

        var created = await engine.CreateGameAsync(new CreateGameRequest(10, null, GameMode.VsPlayer));
        await engine.JoinGameAsync(created.Id);

        await Assert.ThrowsAsync<GameConflictException>(() => engine.JoinGameAsync(created.Id));
    }

    [Fact]
    public async Task JoinGameAsync_OnVsComputerGame_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var engine = CreateEngine(repository, new Random(3));

        var created = await engine.CreateGameAsync(new CreateGameRequest(10, Difficulty.Normal, null));

        await Assert.ThrowsAsync<GameConflictException>(() => engine.JoinGameAsync(created.Id));
    }

    private static GameEngine CreateEngine(InMemoryGameRepository repository, Random random) =>
        new(repository, new FleetPlacer(random), new PlayerTokenService());
}
