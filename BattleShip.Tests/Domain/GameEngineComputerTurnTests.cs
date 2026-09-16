using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using BattleShip.Tests.TestHelpers;

namespace BattleShip.Tests.Domain;

public class GameEngineComputerTurnTests
{
    [Fact]
    public async Task PlayComputerTurnAsync_NoPowerUpChosen_FiresNormalShot()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new NeverUsesPowerUpOpponent(1, 1);
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
        await engine.FireShotAsync(game.Id, Participant.Player1, 9, 9);

        var result = await engine.PlayComputerTurnAsync(game.Id);

        Assert.False(result.UsedPowerUp);
        Assert.NotNull(result.Shot);
        Assert.Null(result.PowerUp);
    }

    [Fact]
    public async Task PlayComputerTurnAsync_PowerUpChosen_UsesPowerUpInsteadOfShot()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new AlwaysUsesReconOpponent();
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);
        await engine.FireShotAsync(game.Id, Participant.Player1, 9, 9);

        var result = await engine.PlayComputerTurnAsync(game.Id);

        Assert.True(result.UsedPowerUp);
        Assert.Null(result.Shot);
        Assert.NotNull(result.PowerUp);
        Assert.Equal(PowerUpType.Recon, result.PowerUp!.Type);
    }

    [Fact]
    public async Task PlayComputerTurnAsync_WrongStatus_ThrowsConflict()
    {
        var repository = new InMemoryGameRepository();
        var opponent = new NeverUsesPowerUpOpponent(0, 0);
        var engine = new GameEngine(
            repository, new FleetPlacer(new Random(1)), new PlayerTokenService(),
            opponent, new Random(1), ObstacleGenerationOptions.None);

        var game = await engine.CreateGameAsync(null);
        await engine.PlaceFleetAsync(game.Id, Participant.Player1, FleetTestData.ValidFleet);

        await Assert.ThrowsAsync<GameConflictException>(() => engine.PlayComputerTurnAsync(game.Id));
    }

    private sealed class NeverUsesPowerUpOpponent(int x, int y) : IComputerOpponent
    {
        public (int X, int Y) ChooseShot(Game game) => (x, y);
        public UsePowerUpRequest? ChoosePowerUp(Game game) => null;
    }

    private sealed class AlwaysUsesReconOpponent : IComputerOpponent
    {
        public (int X, int Y) ChooseShot(Game game) => (0, 0);
        public UsePowerUpRequest? ChoosePowerUp(Game game) =>
            new("Porte-avions", Orientation.Row, 0, null, null);
    }
}
