using BattleShip.API.Services;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class DifficultyComputerOpponentTests
{
    private readonly DifficultyComputerOpponent _opponent = new(new Random(0));

    [Fact]
    public void Easy_PicksUntargetedCell()
    {
        var board = new Board(5);
        board.ResolveShot(0, 0);
        board.ResolveShot(1, 1);
        var game = CreateGame(Difficulty.Easy, board);

        var shot = _opponent.ChooseShot(game);

        Assert.False(board.IsAlreadyTargeted(shot.X, shot.Y));
    }

    [Fact]
    public void Normal_PrefersParity()
    {
        var board = new Board(5);
        var game = CreateGame(Difficulty.Normal, board);

        var shot = _opponent.ChooseShot(game);

        Assert.Equal(0, (shot.X + shot.Y) % 2);
    }

    [Fact]
    public void Normal_FallsBackToOddParity_WhenEvenParityCellsAreTargeted()
    {
        var board = new Board(5);

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if ((x + y) % 2 == 0)
                    board.ResolveShot(x, y);
            }
        }

        var game = CreateGame(Difficulty.Normal, board);
        var shot = _opponent.ChooseShot(game);

        Assert.Equal(1, (shot.X + shot.Y) % 2);
    }

    [Fact]
    public void Hard_PicksAdjacentToHit()
    {
        var board = new Board(5);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 2, 2, horizontal: true);
        board.ResolveShot(2, 2);

        var game = CreateGame(Difficulty.Hard, board);
        var shot = _opponent.ChooseShot(game);

        var dx = Math.Abs(shot.X - 2);
        var dy = Math.Abs(shot.Y - 2);
        Assert.True(dx + dy == 1);
    }

    [Fact]
    public void Hard_PicksAdjacentToDecoyHit()
    {
        var board = new Board(5);
        var ship = new Ship { Name = "Contre-torpilleur", Length = 3 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        Assert.True(board.TryPlaceDecoy(0, 1));
        board.ResolveShot(0, 1);
        Assert.Equal(CellState.DecoyHit, board.GetCell(0, 1));

        var game = CreateGame(Difficulty.Hard, board);
        var shot = _opponent.ChooseShot(game);

        Assert.True(IsAdjacentTo(shot, 0, 1));
    }

    [Fact]
    public void Hard_IgnoresSunkCells_AndUsesParity()
    {
        var board = new Board(5);
        var ship = new Ship { Name = "Torpilleur", Length = 2 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        board.ResolveShot(0, 0);
        board.ResolveShot(1, 0);

        var game = CreateGame(Difficulty.Hard, board);
        var shot = _opponent.ChooseShot(game);

        Assert.False(IsAdjacentTo(shot, 0, 0));
        Assert.False(IsAdjacentTo(shot, 1, 0));
        Assert.Equal(0, (shot.X + shot.Y) % 2);
    }

    [Fact]
    public void NoAvailableCells_Throws()
    {
        var board = new Board(5);

        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
                board.ResolveShot(x, y);
        }

        var game = CreateGame(Difficulty.Easy, board);

        Assert.Throws<InvalidOperationException>(() => _opponent.ChooseShot(game));
    }

    private static Game CreateGame(Difficulty difficulty, Board playerBoard) =>
        new()
        {
            Mode = GameMode.VsComputer,
            Difficulty = difficulty,
            BoardSize = playerBoard.Size,
            Player1Board = playerBoard,
            Player2Board = new Board(playerBoard.Size),
            Status = GameStatus.ComputerTurn,
        };

    private static bool IsAdjacentTo((int X, int Y) shot, int x, int y) =>
        Math.Abs(shot.X - x) + Math.Abs(shot.Y - y) == 1;

    [Fact]
    public void ChoosePowerUp_NoEligibleShips_ReturnsNull()
    {
        var playerBoard = new Board(10);
        var game = CreateGameWithComputerFleet(Difficulty.Hard, playerBoard, allPowerUpsUsed: true);

        var request = _opponent.ChoosePowerUp(game);

        Assert.Null(request);
    }

    [Fact]
    public void ChoosePowerUp_WhenReturningRequest_TargetsAnEligibleShipWithMatchingShape()
    {
        var playerBoard = new Board(10);
        var game = CreateGameWithComputerFleet(Difficulty.Hard, playerBoard, allPowerUpsUsed: false);

        UsePowerUpRequest? request = null;
        for (var seed = 0; seed < 200 && request is null; seed++)
        {
            var opponent = new DifficultyComputerOpponent(new Random(seed));
            request = opponent.ChoosePowerUp(game);
        }

        Assert.NotNull(request);
        var ship = game.Player2Board!.Ships.First(s => s.Name == request!.ShipName);
        Assert.True(ship.CanUsePowerUp);
        AssertShapeMatchesType(ship.PowerUpType, request!);
    }

    private static void AssertShapeMatchesType(PowerUpType type, UsePowerUpRequest request)
    {
        switch (type)
        {
            case PowerUpType.Recon:
                Assert.NotNull(request.Orientation);
                Assert.NotNull(request.Index);
                break;
            case PowerUpType.Torpedo:
                Assert.NotNull(request.Orientation);
                Assert.NotNull(request.Index);
                Assert.NotNull(request.EntryEdge);
                break;
            case PowerUpType.TwinStrike:
            case PowerUpType.DoubleStrike:
                Assert.NotNull(request.Cells);
                Assert.Equal(2, request.Cells!.Count);
                break;
            case PowerUpType.Decoy:
                Assert.NotNull(request.Cells);
                Assert.Single(request.Cells!);
                break;
        }
    }

    [Fact]
    public void ChoosePowerUp_DecoyShipDamagedButAlive_ReturnsPlaceableTarget()
    {
        var playerBoard = new Board(10);
        var game = CreateGameWithComputerFleet(Difficulty.Hard, playerBoard, allPowerUpsUsed: false);

        var decoyShip = game.Player2Board!.Ships.First(s => s.PowerUpType == PowerUpType.Decoy);
        var (hitX, hitY) = decoyShip.Cells[0];
        game.Player2Board.ResolveShot(hitX, hitY);
        Assert.False(decoyShip.IsSunk);
        Assert.True(decoyShip.CanUsePowerUp);

        UsePowerUpRequest? request = null;
        for (var seed = 0; seed < 1000 && request is null; seed++)
        {
            var opponent = new DifficultyComputerOpponent(new Random(seed));
            var candidate = opponent.ChoosePowerUp(game);
            if (candidate is not null && candidate.ShipName == decoyShip.Name)
                request = candidate;
        }

        Assert.NotNull(request);
        var target = request!.Cells!.Single();
        Assert.True(game.Player2Board.TryPlaceDecoy(target.X, target.Y));
    }

    private static Game CreateGameWithComputerFleet(Difficulty difficulty, Board playerBoard, bool allPowerUpsUsed)
    {
        var computerBoard = new Board(playerBoard.Size);
        new FleetPlacer(new Random(123)).PlaceFleetRandomly(computerBoard);

        if (allPowerUpsUsed)
        {
            foreach (var ship in computerBoard.Ships)
                ship.MarkPowerUpUsed();
        }

        return new Game
        {
            Mode = GameMode.VsComputer,
            Difficulty = difficulty,
            BoardSize = playerBoard.Size,
            Player1Board = playerBoard,
            Player2Board = computerBoard,
            Status = GameStatus.ComputerTurn,
        };
    }
}
