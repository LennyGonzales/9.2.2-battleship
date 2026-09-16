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

    [Fact]
    public void ResolveShot_OnObstacle_ReturnsObstacleAndFlipsCellState()
    {
        var board = new Board(10);
        board.ApplyObstacles([(0, 0)]);

        var result = board.ResolveShot(0, 0);

        Assert.Equal(ShotOutcome.Obstacle, result.Outcome);
        Assert.Null(result.SunkShipName);
        Assert.Equal(CellState.ObstacleHit, board.GetCell(0, 0));
    }

    [Fact]
    public void ResolveShot_OnAlreadyShotObstacle_Throws()
    {
        var board = new Board(10);
        board.ApplyObstacles([(0, 0)]);
        board.ResolveShot(0, 0);

        Assert.Throws<InvalidOperationException>(() => board.ResolveShot(0, 0));
    }

    [Fact]
    public void CanPlaceShip_OverObstacle_ReturnsFalse()
    {
        var board = new Board(10);
        board.ApplyObstacles([(0, 0), (1, 0)]);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };

        Assert.False(board.CanPlaceShip(ship, 0, 0, horizontal: true));
    }

    [Fact]
    public void PlaceObstacles_OnlyTouchesPreviouslyEmptyCells_AndProducesConnectedClusters()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 5, 5, horizontal: true);

        board.PlaceObstacles(count: 3, minSize: 2, maxSize: 2, new Random(42));

        var obstacleCells = new List<(int X, int Y)>();
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.GetCell(x, y) == CellState.Obstacle)
                {
                    obstacleCells.Add((x, y));
                }
            }
        }

        Assert.Equal(CellState.Ship, board.GetCell(5, 5));
        Assert.Equal(CellState.Ship, board.GetCell(6, 5));
        Assert.DoesNotContain((5, 5), obstacleCells);
        Assert.DoesNotContain((6, 5), obstacleCells);
        Assert.All(obstacleCells, cell => Assert.True(
            obstacleCells.Any(other => other != cell
                && Math.Abs(other.X - cell.X) + Math.Abs(other.Y - cell.Y) == 1),
            $"Obstacle cell {cell} has no orthogonal neighbor in its own cluster."));
    }

    [Fact]
    public void ApplyObstacles_IgnoresOutOfBoundsCells()
    {
        var board = new Board(5);

        board.ApplyObstacles([(2, 2), (10, 10), (-1, 0)]);

        Assert.Equal(CellState.Obstacle, board.GetCell(2, 2));
    }
}
