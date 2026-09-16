using BattleShip.API.Services;
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
}
