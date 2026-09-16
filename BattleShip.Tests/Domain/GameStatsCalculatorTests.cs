using BattleShip.API.Services;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class GameStatsCalculatorTests
{
    private readonly GameStatsCalculator _calculator = new();

    [Fact]
    public void ToReply_PveGameWithoutOpponentBoard_ReturnsZeroShots()
    {
        var game = new Game
        {
            Player1Board = new Board(10),
            Player2Board = null,
            Status = GameStatus.PlacingFleet,
        };

        var reply = _calculator.ToReply(game);

        Assert.Equal(0, reply.PlayerShots);
        Assert.Equal(0, reply.ComputerShots);
        Assert.Equal(0, reply.PlayerHits);
        Assert.Equal(0, reply.ComputerHits);
        Assert.Equal(nameof(GameStatus.PlacingFleet), reply.Status);
    }

    [Fact]
    public void ToReply_AfterPlayerAndComputerShots_CountsShotsAndHits()
    {
        var playerBoard = new Board(10);
        var computerBoard = new Board(10);

        playerBoard.PlaceShip(new Ship { Name = "Torpilleur", Length = 2 }, 0, 0, true);
        computerBoard.PlaceShip(new Ship { Name = "Torpilleur", Length = 2 }, 5, 5, true);

        computerBoard.ResolveShot(5, 5);
        playerBoard.ResolveShot(0, 0);

        var game = new Game
        {
            Player1Board = playerBoard,
            Player2Board = computerBoard,
            Status = GameStatus.ComputerTurn,
            ShotCount = 1,
        };

        var reply = _calculator.ToReply(game);

        Assert.Equal(1, reply.PlayerShots);
        Assert.Equal(1, reply.ComputerShots);
        Assert.Equal(1, reply.PlayerHits);
        Assert.Equal(1, reply.ComputerHits);
        Assert.Equal(game.ShotCount, reply.PlayerShots);
    }
}
