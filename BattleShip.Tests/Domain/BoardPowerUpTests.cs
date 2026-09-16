using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class BoardPowerUpTests
{
    [Fact]
    public void TryPlaceDecoy_AdjacentToShip_Succeeds()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(0, 1);

        Assert.True(placed);
        Assert.Equal(CellState.Decoy, board.GetCell(0, 1));
    }

    [Fact]
    public void TryPlaceDecoy_NotAdjacentToAnyShip_Fails()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(5, 5);

        Assert.False(placed);
        Assert.Equal(CellState.Empty, board.GetCell(5, 5));
    }

    [Fact]
    public void TryPlaceDecoy_OnOccupiedCell_Fails()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        var placed = board.TryPlaceDecoy(1, 0);

        Assert.False(placed);
    }

    [Fact]
    public void ResolveShot_OnDecoy_ReturnsHitAndNeverSinks()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        board.TryPlaceDecoy(0, 1);

        var result = board.ResolveShot(0, 1);

        Assert.Equal(ShotOutcome.Hit, result.Outcome);
        Assert.Null(result.SunkShipName);
        Assert.Equal(CellState.DecoyHit, board.GetCell(0, 1));
        Assert.False(board.AreAllShipsSunk());
    }

    [Fact]
    public void IsAlreadyTargeted_OnDecoyHit_ReturnsTrue()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        board.TryPlaceDecoy(0, 1);
        board.ResolveShot(0, 1);

        Assert.True(board.IsAlreadyTargeted(0, 1));
    }

    [Fact]
    public void ScanLine_RowWithShip_ReturnsTrue()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 3, 4, horizontal: true);

        Assert.True(board.ScanLine(Orientation.Row, 4));
        Assert.False(board.ScanLine(Orientation.Row, 5));
    }

    [Fact]
    public void ScanLine_ColumnWithObstacle_ReturnsTrue()
    {
        var board = new Board(10);
        board.ApplyObstacles([(2, 7)]);

        Assert.True(board.ScanLine(Orientation.Column, 2));
    }

    [Fact]
    public void FireTorpedo_FromLowEdge_HitsFirstShipCell()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 5, 3, horizontal: true);

        var result = board.FireTorpedo(Orientation.Row, 3, Edge.Low);

        Assert.NotNull(result);
        Assert.Equal(5, result!.X);
        Assert.Equal(3, result.Y);
        Assert.Equal(ShotOutcome.Hit, result.Outcome);
    }

    [Fact]
    public void FireTorpedo_FromHighEdge_HitsFirstShipCellFromTheOtherSide()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 5, 3, horizontal: true);

        var result = board.FireTorpedo(Orientation.Row, 3, Edge.High);

        Assert.NotNull(result);
        Assert.Equal(6, result!.X);
        Assert.Equal(3, result.Y);
        Assert.Equal(ShotOutcome.Hit, result.Outcome);
    }

    [Fact]
    public void FireTorpedo_OnEmptyLine_ReturnsNull()
    {
        var board = new Board(10);

        var result = board.FireTorpedo(Orientation.Row, 0, Edge.Low);

        Assert.Null(result);
    }

    [Fact]
    public void FireTorpedo_StopsAtObstacleBeforeShip()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 8, 0, horizontal: true);
        board.ApplyObstacles([(3, 0)]);

        var result = board.FireTorpedo(Orientation.Row, 0, Edge.Low);

        Assert.NotNull(result);
        Assert.Equal(3, result!.X);
        Assert.Equal(ShotOutcome.Obstacle, result.Outcome);
    }
}
