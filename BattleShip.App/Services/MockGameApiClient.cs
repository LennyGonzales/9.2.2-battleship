using System.Collections.Concurrent;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class MockGameApiClient : IGameApiClient
{
    private static readonly (string Name, int Length)[] FleetSpec =
    [
        ("Porte-avions", 5),
        ("Croiseur", 4),
        ("Contre-torpilleur", 3),
        ("Sous-marin", 3),
        ("Torpilleur", 2),
    ];

    private readonly ConcurrentDictionary<Guid, MockGame> _games = new();

    public Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        var boardSize = Math.Clamp(request.BoardSize ?? 10, 5, 20);
        var difficulty = request.Difficulty ?? Difficulty.Normal;
        var rng = Random.Shared;

        var game = new MockGame
        {
            Id = Guid.NewGuid(),
            BoardSize = boardSize,
            Difficulty = difficulty,
            PlayerFleet = PlaceFleet(boardSize, rng),
            ComputerFleet = PlaceFleet(boardSize, rng),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _games[game.Id] = game;
        return Task.FromResult(ToGameDto(game));
    }

    public Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ToGameDto(GetGameOrThrow(id)));

    public Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        var cells = new List<CellDto>();
        for (var y = 0; y < game.BoardSize; y++)
        {
            for (var x = 0; x < game.BoardSize; x++)
            {
                var cell = (X: x, Y: y);
                var ship = game.PlayerFleet.FirstOrDefault(s => s.Cells.Contains(cell));
                var state = ship switch
                {
                    null => VisibleCellState.Empty,
                    _ when ship.Hits.Contains(cell) && ship.IsSunk => VisibleCellState.Sunk,
                    _ when ship.Hits.Contains(cell) => VisibleCellState.Hit,
                    _ => VisibleCellState.Ship,
                };
                cells.Add(new CellDto(x, y, state));
            }
        }
        return Task.FromResult(new BoardDto(BoardOwner.Player, game.BoardSize, cells));
    }

    public Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        var cells = new List<CellDto>();
        for (var y = 0; y < game.BoardSize; y++)
        {
            for (var x = 0; x < game.BoardSize; x++)
            {
                var cell = (X: x, Y: y);
                if (!game.PlayerShotsAtComputer.Contains(cell))
                {
                    cells.Add(new CellDto(x, y, VisibleCellState.Unknown));
                    continue;
                }

                var ship = game.ComputerFleet.FirstOrDefault(s => s.Cells.Contains(cell));
                var state = ship switch
                {
                    { IsSunk: true } => VisibleCellState.Sunk,
                    not null => VisibleCellState.Hit,
                    null => VisibleCellState.Miss,
                };
                cells.Add(new CellDto(x, y, state));
            }
        }
        return Task.FromResult(new BoardDto(BoardOwner.Opponent, game.BoardSize, cells));
    }

    public Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        if (game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon)
        {
            throw Conflict("La partie est deja terminee.");
        }

        if (shot.X < 0 || shot.X >= game.BoardSize || shot.Y < 0 || shot.Y >= game.BoardSize)
        {
            throw ValidationError("La cible est hors de la grille.");
        }

        var target = (X: shot.X, Y: shot.Y);
        if (!game.PlayerShotsAtComputer.Add(target))
        {
            throw Conflict("Cette case a deja ete ciblee.");
        }

        var playerShot = ResolveShot(game.ComputerFleet, target);
        game.ShotCount++;

        ShotOutcomeDto? computerShot = null;
        if (AllSunk(game.ComputerFleet))
        {
            game.Status = GameStatus.PlayerWon;
        }
        else
        {
            var computerTarget = PickComputerTarget(game);
            game.ComputerShotsAtPlayer.Add(computerTarget);
            computerShot = ResolveShot(game.PlayerFleet, computerTarget);
            UpdateHuntState(game, computerShot);
            game.Status = AllSunk(game.PlayerFleet) ? GameStatus.ComputerWon : GameStatus.PlayerTurn;
        }

        return Task.FromResult(new ShotResultDto(playerShot, computerShot, game.Status));
    }

    private MockGame GetGameOrThrow(Guid id)
    {
        if (!_games.TryGetValue(id, out var game))
        {
            throw NotFound("Partie introuvable.");
        }
        return game;
    }

    private static List<ShipInstance> PlaceFleet(int boardSize, Random rng)
    {
        var occupied = new HashSet<(int X, int Y)>();
        var fleet = new List<ShipInstance>();

        foreach (var (name, length) in FleetSpec)
        {
            const int maxAttempts = 5000;
            var placed = false;

            for (var attempt = 0; attempt < maxAttempts && !placed; attempt++)
            {
                var horizontal = rng.Next(2) == 0;
                var maxX = horizontal ? boardSize - length : boardSize - 1;
                var maxY = horizontal ? boardSize - 1 : boardSize - length;
                var startX = rng.Next(maxX + 1);
                var startY = rng.Next(maxY + 1);

                var cells = Enumerable.Range(0, length)
                    .Select(i => horizontal
                        ? (X: startX + i, Y: startY)
                        : (X: startX, Y: startY + i))
                    .ToList();

                if (cells.Any(occupied.Contains))
                {
                    continue;
                }

                foreach (var cell in cells)
                {
                    occupied.Add(cell);
                }

                fleet.Add(new ShipInstance { Name = name, Length = length, Cells = cells });
                placed = true;
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"Impossible de placer le navire '{name}' sur une grille de taille {boardSize}.");
            }
        }

        return fleet;
    }

    private static ShotOutcomeDto ResolveShot(List<ShipInstance> fleet, (int X, int Y) target)
    {
        var ship = fleet.FirstOrDefault(s => s.Cells.Contains(target));
        if (ship is null)
        {
            return new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Miss, null);
        }

        ship.Hits.Add(target);
        return ship.IsSunk
            ? new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Sunk, ship.Name)
            : new ShotOutcomeDto(target.X, target.Y, ShotOutcome.Hit, null);
    }

    private static (int X, int Y) PickComputerTarget(MockGame game)
    {
        if (game.Difficulty == Difficulty.Hard)
        {
            while (game.HuntQueue.Count > 0)
            {
                var candidate = game.HuntQueue.Dequeue();
                if (IsInBounds(candidate, game.BoardSize) && !game.ComputerShotsAtPlayer.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        return RandomUntried(game);
    }

    private static (int X, int Y) RandomUntried(MockGame game)
    {
        var rng = Random.Shared;
        (int X, int Y) candidate;
        do
        {
            candidate = (rng.Next(game.BoardSize), rng.Next(game.BoardSize));
        } while (game.ComputerShotsAtPlayer.Contains(candidate));
        return candidate;
    }

    private static bool IsInBounds((int X, int Y) cell, int size) =>
        cell.X >= 0 && cell.X < size && cell.Y >= 0 && cell.Y < size;

    private static void UpdateHuntState(MockGame game, ShotOutcomeDto outcome)
    {
        if (game.Difficulty != Difficulty.Hard)
        {
            return;
        }

        if (outcome.Outcome == ShotOutcome.Sunk)
        {
            game.HuntQueue.Clear();
        }
        else if (outcome.Outcome == ShotOutcome.Hit)
        {
            var (x, y) = (outcome.X, outcome.Y);
            foreach (var neighbor in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                game.HuntQueue.Enqueue(neighbor);
            }
        }
    }

    private static bool AllSunk(IEnumerable<ShipInstance> fleet) => fleet.All(s => s.IsSunk);

    private static GameDto ToGameDto(MockGame game) => new(
        game.Id,
        game.Status,
        game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon ? null : Player.Player,
        game.BoardSize,
        game.ShotCount,
        game.CreatedAt);

    private static GameApiException NotFound(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Not Found", 404, detail, null), 404);

    private static GameApiException Conflict(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Conflict", 409, detail, null), 409);

    private static GameApiException ValidationError(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Bad Request", 400, detail, null), 400);

    private sealed class ShipInstance
    {
        public required string Name { get; init; }
        public required int Length { get; init; }
        public required List<(int X, int Y)> Cells { get; init; }
        public HashSet<(int X, int Y)> Hits { get; } = [];
        public bool IsSunk => Hits.Count == Cells.Count;
    }

    private sealed class MockGame
    {
        public required Guid Id { get; init; }
        public required int BoardSize { get; init; }
        public required Difficulty Difficulty { get; init; }
        public required List<ShipInstance> PlayerFleet { get; init; }
        public required List<ShipInstance> ComputerFleet { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public HashSet<(int X, int Y)> PlayerShotsAtComputer { get; } = [];
        public HashSet<(int X, int Y)> ComputerShotsAtPlayer { get; } = [];
        public Queue<(int X, int Y)> HuntQueue { get; } = [];
        public GameStatus Status { get; set; } = GameStatus.PlayerTurn;
        public int ShotCount { get; set; }
    }
}
