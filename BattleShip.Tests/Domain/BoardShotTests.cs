using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class BoardShotTests
{
    [Fact]
    public void ResolveShot_OnEmptyCell_ReturnsMiss()
    {
        var board = new Board(10);
        var result = board.ResolveShot(0, 0);

        Assert.Equal(ShotOutcome.Miss, result.Outcome);
        Assert.Equal(CellState.Miss, board.GetCell(0, 0));
    }

    [Fact]
    public void ResolveShot_OnShip_ReturnsHit()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var result = board.ResolveShot(0, 0);

        Assert.Equal(ShotOutcome.Hit, result.Outcome);
        Assert.Null(result.SunkShipName);
    }

    [Fact]
    public void ResolveShot_OnLastSegment_ReturnsSunk()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        board.ResolveShot(0, 0);
        var result = board.ResolveShot(1, 0);

        Assert.Equal(ShotOutcome.Sunk, result.Outcome);
        Assert.Equal("Torpilleur", result.SunkShipName);
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void ResolveShot_OnAlreadyTargetedCell_Throws()
    {
        var board = new Board(10);
        board.ResolveShot(0, 0);

        Assert.Throws<InvalidOperationException>(() => board.ResolveShot(0, 0));
    }
}
