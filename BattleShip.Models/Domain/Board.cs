namespace BattleShip.Models.Domain;

public sealed class Board
{
    private readonly CellState[,] _cells;
    private readonly List<Ship> _ships = [];

    public Board(int size)
    {
        if (size < GameOptions.MinBoardSize || size > GameOptions.MaxBoardSize)
            throw new ArgumentOutOfRangeException(nameof(size));

        Size = size;
        _cells = new CellState[size, size];
    }

    public int Size { get; }

    public IReadOnlyList<Ship> Ships => _ships;

    public CellState GetCell(int x, int y) => _cells[x, y];

    public bool CanPlaceShip(Ship ship, int x, int y, bool horizontal)
    {
        var cells = GetOccupiedCells(ship.Length, x, y, horizontal);
        if (cells is null)
            return false;

        return cells.All(c => _cells[c.X, c.Y] == CellState.Empty);
    }

    public void PlaceShip(Ship ship, int x, int y, bool horizontal)
    {
        if (!CanPlaceShip(ship, x, y, horizontal))
            throw new InvalidOperationException($"Impossible de placer {ship.Name} en ({x},{y}).");

        var cells = GetOccupiedCells(ship.Length, x, y, horizontal)!;
        ship.SetCells(cells);

        foreach (var (cellX, cellY) in cells)
            _cells[cellX, cellY] = CellState.Ship;

        _ships.Add(ship);
    }

    private List<(int X, int Y)>? GetOccupiedCells(int length, int x, int y, bool horizontal)
    {
        var cells = new List<(int X, int Y)>(length);

        for (var offset = 0; offset < length; offset++)
        {
            var cellX = horizontal ? x + offset : x;
            var cellY = horizontal ? y : y + offset;

            if (cellX < 0 || cellY < 0 || cellX >= Size || cellY >= Size)
                return null;

            cells.Add((cellX, cellY));
        }

        return cells;
    }
}
