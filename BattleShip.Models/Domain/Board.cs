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
        return state is CellState.Miss or CellState.Hit or CellState.Sunk or CellState.ObstacleHit or CellState.DecoyHit;
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

        if (_cells[x, y] == CellState.Decoy)
        {
            _cells[x, y] = CellState.DecoyHit;
            return new ShotResolution(x, y, ShotOutcome.Hit, null);
        }

        if (_cells[x, y] == CellState.Obstacle)
        {
            _cells[x, y] = CellState.ObstacleHit;
            return new ShotResolution(x, y, ShotOutcome.Obstacle, null);
        }

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

    public void PlaceObstacles(int count, int minSize, int maxSize, Random rng)
    {
        for (var i = 0; i < count; i++)
        {
            var targetSize = rng.Next(minSize, maxSize + 1);
            TryPlaceOneObstacle(targetSize, rng);
        }
    }

    public void ApplyObstacles(IEnumerable<(int X, int Y)> cells)
    {
        foreach (var (x, y) in cells)
        {
            if (IsWithinBounds(x, y))
                _cells[x, y] = CellState.Obstacle;
        }
    }

    public bool TryPlaceDecoy(int x, int y)
    {
        if (!IsWithinBounds(x, y) || _cells[x, y] != CellState.Empty)
            return false;

        if (!GetOrthogonalNeighbors(x, y).Any(n => _cells[n.X, n.Y] == CellState.Ship))
            return false;

        _cells[x, y] = CellState.Decoy;
        return true;
    }

    public bool ScanLine(Orientation orientation, int index)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));

        for (var i = 0; i < Size; i++)
        {
            var (x, y) = orientation == Orientation.Row ? (i, index) : (index, i);
            if (_cells[x, y] is CellState.Ship or CellState.Hit or CellState.Sunk
                or CellState.Obstacle or CellState.ObstacleHit
                or CellState.Decoy or CellState.DecoyHit)
            {
                return true;
            }
        }

        return false;
    }

    public ShotResolution? FireTorpedo(Orientation orientation, int index, Edge entryEdge)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));

        var indices = Enumerable.Range(0, Size);
        if (entryEdge == Edge.High)
            indices = indices.Reverse();

        foreach (var i in indices)
        {
            var (x, y) = orientation == Orientation.Row ? (i, index) : (index, i);
            if (_cells[x, y] is CellState.Ship or CellState.Obstacle or CellState.Decoy)
                return ResolveShot(x, y);
        }

        return null;
    }

    private IEnumerable<(int X, int Y)> GetOrthogonalNeighbors(int x, int y)
    {
        if (IsWithinBounds(x + 1, y)) yield return (x + 1, y);
        if (IsWithinBounds(x - 1, y)) yield return (x - 1, y);
        if (IsWithinBounds(x, y + 1)) yield return (x, y + 1);
        if (IsWithinBounds(x, y - 1)) yield return (x, y - 1);
    }

    private void TryPlaceOneObstacle(int targetSize, Random rng)
    {
        for (var attempt = 0; attempt < GameOptions.MaxObstaclePlacementAttempts; attempt++)
        {
            var startX = rng.Next(Size);
            var startY = rng.Next(Size);

            if (_cells[startX, startY] != CellState.Empty)
                continue;

            var cluster = GrowCluster(startX, startY, targetSize, rng);
            if (cluster.Count != targetSize)
                continue;

            foreach (var (x, y) in cluster)
                _cells[x, y] = CellState.Obstacle;
            return;
        }
    }

    private List<(int X, int Y)> GrowCluster(int startX, int startY, int targetSize, Random rng)
    {
        var cluster = new List<(int X, int Y)> { (startX, startY) };
        var frontier = new List<(int X, int Y)> { (startX, startY) };

        while (cluster.Count < targetSize && frontier.Count > 0)
        {
            var from = frontier[rng.Next(frontier.Count)];
            var neighbors = GetEmptyNeighbors(from, cluster);

            if (neighbors.Count == 0)
            {
                frontier.Remove(from);
                continue;
            }

            var next = neighbors[rng.Next(neighbors.Count)];
            cluster.Add(next);
            frontier.Add(next);
        }

        return cluster;
    }

    private List<(int X, int Y)> GetEmptyNeighbors((int X, int Y) from, List<(int X, int Y)> cluster)
    {
        var candidates = new[]
        {
            (from.X + 1, from.Y),
            (from.X - 1, from.Y),
            (from.X, from.Y + 1),
            (from.X, from.Y - 1),
        };

        return candidates
            .Where(n => IsWithinBounds(n.Item1, n.Item2)
                        && _cells[n.Item1, n.Item2] == CellState.Empty
                        && !cluster.Contains(n))
            .ToList();
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
