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

    public bool IsWithinBounds(int x, int y) =>
        x >= 0 && y >= 0 && x < Size && y < Size;

    public bool IsAlreadyTargeted(int x, int y)
    {
        var state = _cells[x, y];
        return state is CellState.Miss or CellState.Hit or CellState.Sunk;
    }

    public bool AreAllShipsSunk() => _ships.Count > 0 && _ships.All(s => s.IsSunk);

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

    public ShotResolution ResolveShot(int x, int y)
    {
        if (!IsWithinBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Coordonnees hors grille : ({x},{y}).");

        if (IsAlreadyTargeted(x, y))
            throw new InvalidOperationException($"La case ({x},{y}) a deja ete ciblee.");

        if (_cells[x, y] == CellState.Empty)
        {
            _cells[x, y] = CellState.Miss;
            return new ShotResolution(x, y, ShotOutcome.Miss, null);
        }

        var ship = FindShipAt(x, y)!;
        _cells[x, y] = CellState.Hit;

        if (IsShipFullyHit(ship))
        {
            foreach (var (cellX, cellY) in ship.Cells)
                _cells[cellX, cellY] = CellState.Sunk;

            ship.MarkSunk();
            return new ShotResolution(x, y, ShotOutcome.Sunk, ship.Name);
        }

        return new ShotResolution(x, y, ShotOutcome.Hit, null);
    }

    private Ship? FindShipAt(int x, int y) =>
        _ships.FirstOrDefault(s => s.Cells.Any(c => c.X == x && c.Y == y));

    private bool IsShipFullyHit(Ship ship) =>
        ship.Cells.All(c => _cells[c.X, c.Y] is CellState.Hit or CellState.Sunk);

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
