using System.Collections.Concurrent;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.App.Services;

public sealed class MockGameApiClient : IGameApiClient
{
    private readonly ConcurrentDictionary<Guid, MockGame> _games = new();

    public Task<GameCreatedDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        if (request.Mode == GameMode.VsPlayer)
        {
            throw Conflict("Le multijoueur necessite le vrai backend (UseMockApi=false).");
        }

        var boardSize = Math.Clamp(request.BoardSize ?? GameOptions.DefaultBoardSize, GameOptions.MinBoardSize, GameOptions.MaxBoardSize);
        var difficulty = request.Difficulty ?? Difficulty.Normal;

        var playerBoard = new Board(boardSize);
        playerBoard.PlaceObstacles(
            GameOptions.DefaultObstacleCount, GameOptions.MinObstacleSize, GameOptions.MaxObstacleSize, Random.Shared);

        var game = new MockGame
        {
            Id = Guid.NewGuid(),
            BoardSize = boardSize,
            Difficulty = difficulty,
            PlayerBoard = playerBoard,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _games[game.Id] = game;
        return Task.FromResult(ToGameCreatedDto(game));
    }

    public Task<JoinGameDto> JoinGameAsync(Guid id, CancellationToken ct = default) =>
        throw Conflict("Le multijoueur necessite le vrai backend (UseMockApi=false).");

    public Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ToGameDto(GetGameOrThrow(id)));

    public Task<GameDto> PlaceFleetAsync(
        Guid id, PlaceFleetRequest request, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);

        if (game.Status != GameStatus.PlacingFleet)
        {
            throw Conflict("Le placement de flotte n'est pas autorise dans cet etat.");
        }

        if (game.PlayerBoard.Ships.Count > 0)
        {
            throw Conflict("La flotte est deja placee.");
        }

        PlaceFleetFromRequest(game.PlayerBoard, request.Ships);

        var computerBoard = new Board(game.BoardSize);
        computerBoard.PlaceObstacles(
            GameOptions.DefaultObstacleCount, GameOptions.MinObstacleSize, GameOptions.MaxObstacleSize, Random.Shared);

        game.ComputerBoard = computerBoard;
        PlaceFleetRandomly(game.ComputerBoard, Random.Shared);
        game.Status = GameStatus.PlayerTurn;

        return Task.FromResult(ToGameDto(game));
    }

    public Task<BoardDto> GetPlayerBoardAsync(Guid id, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        return Task.FromResult(ToBoardDto(game.PlayerBoard, BoardOwner.Player));
    }

    public Task<BoardDto> GetOpponentBoardAsync(Guid id, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);
        return Task.FromResult(ToBoardDto(game.ComputerBoard!, BoardOwner.Opponent));
    }

    public Task<ShotResultDto> FireShotAsync(
        Guid id, ShotRequest shot, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);
        if (game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon)
        {
            throw Conflict("La partie est deja terminee.");
        }

        var isComputerTurnCall = shot.X is null && shot.Y is null;

        return Task.FromResult(isComputerTurnCall
            ? FireComputerShot(game)
            : FirePlayerShot(game, shot));
    }

    public Task<PowerUpResultDto> UsePowerUpAsync(
        Guid id, UsePowerUpRequest request, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);

        if (game.Status != GameStatus.PlayerTurn)
        {
            throw Conflict("Ce n'est pas votre tour.");
        }

        var result = ResolvePowerUp(game, game.PlayerBoard, game.ComputerBoard!, request, PlayerSide.Player);
        game.Status = game.ComputerBoard!.AreAllShipsSunk() ? GameStatus.PlayerWon : GameStatus.ComputerTurn;

        return Task.FromResult(result with { Status = game.Status });
    }

    public Task<ComputerTurnResultDto> PlayComputerTurnAsync(
        Guid id, string? playerToken, CancellationToken ct = default)
    {
        var game = GetGameOrThrow(id);
        EnsureStarted(game);

        if (game.Status != GameStatus.ComputerTurn)
        {
            throw Conflict("Ce n'est pas le tour de l'ordinateur.");
        }

        var request = ChooseComputerPowerUp(game);
        if (request is not null)
        {
            var powerUpResult = ResolvePowerUp(game, game.ComputerBoard!, game.PlayerBoard, request, PlayerSide.Computer);
            game.Status = game.PlayerBoard.AreAllShipsSunk() ? GameStatus.ComputerWon : GameStatus.PlayerTurn;
            return Task.FromResult(new ComputerTurnResultDto(true, null, powerUpResult with { Status = game.Status }));
        }

        var shotResult = FireComputerShot(game);
        return Task.FromResult(new ComputerTurnResultDto(false, shotResult, null));
    }

    private static void EnsureStarted(MockGame game)
    {
        if (game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
        {
            throw Conflict("La partie n'a pas encore commence.");
        }
    }

    private static ShotResultDto FirePlayerShot(MockGame game, ShotRequest shot)
    {
        if (game.Status != GameStatus.PlayerTurn)
        {
            throw Conflict("Ce n'est pas votre tour.");
        }

        if (shot.X is null || shot.Y is null)
        {
            throw ValidationError("Coordonnees requises pour ce tour.");
        }

        if (!game.ComputerBoard!.IsWithinBounds(shot.X.Value, shot.Y.Value))
        {
            throw ValidationError("La cible est hors de la grille.");
        }

        if (game.ComputerBoard.IsAlreadyTargeted(shot.X.Value, shot.Y.Value))
        {
            throw Conflict("Cette case a deja ete ciblee.");
        }

        var resolution = game.ComputerBoard.ResolveShot(shot.X.Value, shot.Y.Value);
        game.ShotCount++;
        game.Status = game.ComputerBoard.AreAllShipsSunk() ? GameStatus.PlayerWon : GameStatus.ComputerTurn;

        var outcome = new ShotOutcomeDto(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);
        return new ShotResultDto(outcome, PlayerSide.Player, game.Status);
    }

    private static ShotResultDto FireComputerShot(MockGame game)
    {
        if (game.Status != GameStatus.ComputerTurn)
        {
            throw Conflict("Ce n'est pas le tour de l'ordinateur.");
        }

        var target = PickComputerTarget(game);
        var resolution = game.PlayerBoard.ResolveShot(target.X, target.Y);
        UpdateHuntState(game, resolution);
        game.Status = game.PlayerBoard.AreAllShipsSunk() ? GameStatus.ComputerWon : GameStatus.PlayerTurn;

        var outcome = new ShotOutcomeDto(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);
        return new ShotResultDto(outcome, PlayerSide.Computer, game.Status);
    }

    private MockGame GetGameOrThrow(Guid id)
    {
        if (!_games.TryGetValue(id, out var game))
        {
            throw NotFound("Partie introuvable.");
        }
        return game;
    }

    private static void PlaceFleetFromRequest(Board board, IReadOnlyList<PlaceShipRequest> ships)
    {
        ValidateFleetComposition(ships);

        foreach (var placement in ships)
        {
            var spec = GameOptions.DefaultFleet.First(s => s.Name == placement.Name);
            var ship = new Ship { Name = spec.Name, Length = spec.Length };

            if (!board.CanPlaceShip(ship, placement.X, placement.Y, placement.Horizontal))
            {
                throw ValidationError($"Impossible de placer {placement.Name} en ({placement.X},{placement.Y}).");
            }

            board.PlaceShip(ship, placement.X, placement.Y, placement.Horizontal);
        }
    }

    private static void ValidateFleetComposition(IReadOnlyList<PlaceShipRequest> ships)
    {
        if (ships.Count != GameOptions.DefaultFleet.Length)
        {
            throw ValidationError($"La flotte doit contenir exactement {GameOptions.DefaultFleet.Length} navires.");
        }

        var expectedNames = GameOptions.DefaultFleet.Select(s => s.Name).ToHashSet();
        var receivedNames = new HashSet<string>();

        foreach (var ship in ships)
        {
            if (!expectedNames.Contains(ship.Name))
            {
                throw ValidationError($"Navire inconnu : {ship.Name}.");
            }

            if (!receivedNames.Add(ship.Name))
            {
                throw ValidationError($"Navire en double : {ship.Name}.");
            }
        }

        foreach (var expected in expectedNames)
        {
            if (!receivedNames.Contains(expected))
            {
                throw ValidationError($"Navire manquant : {expected}.");
            }
        }
    }

    private static void PlaceFleetRandomly(Board board, Random rng)
    {
        foreach (var (name, length) in GameOptions.DefaultFleet)
        {
            var ship = new Ship { Name = name, Length = length };
            var placed = false;

            for (var attempt = 0; attempt < GameOptions.MaxPlacementAttemptsPerShip; attempt++)
            {
                var horizontal = rng.Next(2) == 0;
                var x = rng.Next(board.Size);
                var y = rng.Next(board.Size);

                if (!board.CanPlaceShip(ship, x, y, horizontal))
                {
                    continue;
                }

                board.PlaceShip(ship, x, y, horizontal);
                placed = true;
                break;
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"Impossible de placer le navire '{name}' sur une grille de taille {board.Size}.");
            }
        }
    }

    private static (int X, int Y) PickComputerTarget(MockGame game)
    {
        if (game.Difficulty == Difficulty.Hard)
        {
            while (game.HuntQueue.Count > 0)
            {
                var candidate = game.HuntQueue.Dequeue();
                if (game.PlayerBoard.IsWithinBounds(candidate.X, candidate.Y)
                    && !game.PlayerBoard.IsAlreadyTargeted(candidate.X, candidate.Y))
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
        } while (game.PlayerBoard.IsAlreadyTargeted(candidate.X, candidate.Y));
        return candidate;
    }

    private static void UpdateHuntState(MockGame game, ShotResolution resolution)
    {
        if (game.Difficulty != Difficulty.Hard)
        {
            return;
        }

        if (resolution.Outcome == ShotOutcome.Sunk)
        {
            game.HuntQueue.Clear();
        }
        else if (resolution.Outcome == ShotOutcome.Hit)
        {
            var (x, y) = (resolution.X, resolution.Y);
            foreach (var neighbor in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                game.HuntQueue.Enqueue(neighbor);
            }
        }
    }

    private static PowerUpResultDto ResolvePowerUp(
        MockGame game, Board ownBoard, Board opponentBoard, UsePowerUpRequest request, PlayerSide actor)
    {
        var ship = ownBoard.Ships.FirstOrDefault(s => s.Name == request.ShipName)
            ?? throw ValidationError("Navire inconnu.");

        if (!ship.CanUsePowerUp)
        {
            throw Conflict("Power-up indisponible pour ce navire.");
        }

        ReconResultDto? recon = null;
        ShotOutcomeDto? torpedo = null;
        IReadOnlyList<ShotOutcomeDto>? cells = null;

        switch (ship.PowerUpType)
        {
            case PowerUpType.Recon:
            {
                var (orientation, index) = RequireLine(request);
                recon = new ReconResultDto(orientation, index, opponentBoard.ScanLine(orientation, index));
                break;
            }
            case PowerUpType.Torpedo:
            {
                var (orientation, index) = RequireLine(request);
                var entryEdge = request.EntryEdge ?? throw ValidationError("Bord d'entree requis pour la torpille.");
                var resolution = opponentBoard.FireTorpedo(orientation, index, entryEdge);
                torpedo = resolution is null ? null : ToOutcomeDto(resolution);
                break;
            }
            case PowerUpType.TwinStrike:
                cells = ResolveMultiStrike(opponentBoard, request, requireAdjacent: true);
                break;
            case PowerUpType.DoubleStrike:
                cells = ResolveMultiStrike(opponentBoard, request, requireAdjacent: false);
                break;
            case PowerUpType.Decoy:
                ResolveDecoy(ownBoard, ship, request);
                break;
        }

        ship.MarkPowerUpUsed();

        return new PowerUpResultDto(ship.Name, ship.PowerUpType, actor, game.Status, recon, torpedo, cells);
    }

    private static (Orientation Orientation, int Index) RequireLine(UsePowerUpRequest request)
    {
        if (request.Orientation is not { } orientation || request.Index is not { } index)
        {
            throw ValidationError("Ligne ou colonne requise pour ce power-up.");
        }
        return (orientation, index);
    }

    private static IReadOnlyList<ShotOutcomeDto> ResolveMultiStrike(Board opponentBoard, UsePowerUpRequest request, bool requireAdjacent)
    {
        var targetCells = request.Cells;
        if (targetCells is not { Count: 2 })
        {
            throw ValidationError("Deux cases sont requises pour ce power-up.");
        }

        var (a, b) = (targetCells[0], targetCells[1]);
        if (a.X == b.X && a.Y == b.Y)
        {
            throw Conflict("Les deux cases doivent etre distinctes.");
        }

        if (requireAdjacent && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1)
        {
            throw ValidationError("Les deux cases doivent etre adjacentes.");
        }

        foreach (var cell in targetCells)
        {
            if (!opponentBoard.IsWithinBounds(cell.X, cell.Y))
            {
                throw ValidationError("La cible est hors de la grille.");
            }
            if (opponentBoard.IsAlreadyTargeted(cell.X, cell.Y))
            {
                throw Conflict("Cette case a deja ete ciblee.");
            }
        }

        return targetCells.Select(c => ToOutcomeDto(opponentBoard.ResolveShot(c.X, c.Y))).ToList();
    }

    private static void ResolveDecoy(Board ownBoard, Ship ship, UsePowerUpRequest request)
    {
        if (request.Cells is not { Count: 1 })
        {
            throw ValidationError("Une case est requise pour le leurre.");
        }

        var target = request.Cells[0];
        if (!ship.Cells.Any(c => Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y) == 1))
        {
            throw ValidationError("Le leurre doit etre adjacent au contre-torpilleur.");
        }

        if (!ownBoard.TryPlaceDecoy(target.X, target.Y))
        {
            throw Conflict("Impossible de placer le leurre ici.");
        }
    }

    private static ShotOutcomeDto ToOutcomeDto(ShotResolution resolution) =>
        new(resolution.X, resolution.Y, resolution.Outcome, resolution.SunkShipName);

    private static UsePowerUpRequest? ChooseComputerPowerUp(MockGame game)
    {
        var eligible = game.ComputerBoard!.Ships.Where(s => s.CanUsePowerUp).ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var chance = game.Difficulty == Difficulty.Hard ? 0.35 : 0.15;
        if (Random.Shared.NextDouble() >= chance)
        {
            return null;
        }

        var decoy = eligible.FirstOrDefault(s => s.PowerUpType == PowerUpType.Decoy);
        if (decoy is not null && Random.Shared.Next(3) == 0)
        {
            var target = FindDecoySpot(decoy, game.ComputerBoard);
            if (target is not null)
            {
                return new UsePowerUpRequest(decoy.Name, null, null, null, [new CellTarget(target.Value.X, target.Value.Y)]);
            }
        }

        var attackers = eligible.Where(s => s.PowerUpType != PowerUpType.Decoy).ToList();
        if (attackers.Count == 0)
        {
            return null;
        }

        var ship = attackers[Random.Shared.Next(attackers.Count)];
        var board = game.PlayerBoard;

        return ship.PowerUpType switch
        {
            PowerUpType.Recon => new UsePowerUpRequest(
                ship.Name, RandomOrientation(), Random.Shared.Next(board.Size), null, null),
            PowerUpType.Torpedo => new UsePowerUpRequest(
                ship.Name, RandomOrientation(), Random.Shared.Next(board.Size), RandomEdge(), null),
            PowerUpType.TwinStrike => BuildAdjacentPairRequest(ship, board),
            PowerUpType.DoubleStrike => BuildAnyPairRequest(ship, board),
            _ => null,
        };
    }

    private static Orientation RandomOrientation() => Random.Shared.Next(2) == 0 ? Orientation.Row : Orientation.Column;

    private static Edge RandomEdge() => Random.Shared.Next(2) == 0 ? Edge.Low : Edge.High;

    private static (int X, int Y)? FindDecoySpot(Ship ship, Board ownBoard)
    {
        foreach (var (x, y) in ship.Cells)
        {
            if (ownBoard.GetCell(x, y) != CellState.Ship)
            {
                continue; // damaged cell — TryPlaceDecoy requires adjacency to a still-intact Ship cell
            }

            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (ownBoard.IsWithinBounds(nx, ny) && ownBoard.GetCell(nx, ny) == CellState.Empty)
                {
                    return (nx, ny);
                }
            }
        }
        return null;
    }

    private static UsePowerUpRequest? BuildAdjacentPairRequest(Ship ship, Board board)
    {
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (board.IsAlreadyTargeted(x, y))
                {
                    continue;
                }

                if (board.IsWithinBounds(x + 1, y) && !board.IsAlreadyTargeted(x + 1, y))
                {
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(x, y), new CellTarget(x + 1, y)]);
                }

                if (board.IsWithinBounds(x, y + 1) && !board.IsAlreadyTargeted(x, y + 1))
                {
                    return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(x, y), new CellTarget(x, y + 1)]);
                }
            }
        }
        return null;
    }

    private static UsePowerUpRequest? BuildAnyPairRequest(Ship ship, Board board)
    {
        var candidates = new List<(int X, int Y)>();
        for (var x = 0; x < board.Size; x++)
        {
            for (var y = 0; y < board.Size; y++)
            {
                if (!board.IsAlreadyTargeted(x, y))
                {
                    candidates.Add((x, y));
                }
            }
        }

        if (candidates.Count < 2)
        {
            return null;
        }

        var first = candidates[Random.Shared.Next(candidates.Count)];
        candidates.Remove(first);
        var second = candidates[Random.Shared.Next(candidates.Count)];

        return new UsePowerUpRequest(ship.Name, null, null, null, [new CellTarget(first.X, first.Y), new CellTarget(second.X, second.Y)]);
    }

    private static PlayerSide? CurrentTurn(GameStatus status) => status switch
    {
        GameStatus.PlayerTurn => PlayerSide.Player,
        GameStatus.ComputerTurn => PlayerSide.Computer,
        _ => null,
    };

    private static GameDto ToGameDto(MockGame game) => new(
        game.Id,
        GameMode.VsComputer,
        game.Status,
        CurrentTurn(game.Status),
        game.BoardSize,
        game.ShotCount,
        game.CreatedAt);

    private static GameCreatedDto ToGameCreatedDto(MockGame game) => new(
        game.Id,
        GameMode.VsComputer,
        game.Status,
        CurrentTurn(game.Status),
        game.BoardSize,
        game.ShotCount,
        game.CreatedAt,
        null);

    private static BoardDto ToBoardDto(Board board, BoardOwner owner)
    {
        var cells = new List<CellDto>(board.Size * board.Size);
        for (var y = 0; y < board.Size; y++)
        {
            for (var x = 0; x < board.Size; x++)
            {
                cells.Add(new CellDto(x, y, ToVisibleCellState(board.GetCell(x, y), owner)));
            }
        }
        var ships = owner == BoardOwner.Player ? ToShipStatusDtos(board) : null;
        return new BoardDto(owner, board.Size, cells, ships);
    }

    private static IReadOnlyList<ShipStatusDto> ToShipStatusDtos(Board board) =>
        board.Ships.Select(s => new ShipStatusDto(s.Name, s.Length, s.PowerUpType, s.IsSunk, s.PowerUpUsed)).ToList();

    private static VisibleCellState ToVisibleCellState(CellState state, BoardOwner owner) => (owner, state) switch
    {
        (BoardOwner.Opponent, CellState.Ship) => VisibleCellState.Unknown,
        (BoardOwner.Opponent, CellState.Empty) => VisibleCellState.Unknown,
        (BoardOwner.Opponent, CellState.Miss) => VisibleCellState.Miss,
        (BoardOwner.Player, CellState.Miss) => VisibleCellState.Empty,
        (BoardOwner.Opponent, CellState.Obstacle) => VisibleCellState.Unknown,
        (_, CellState.Obstacle) => VisibleCellState.Obstacle,
        (_, CellState.ObstacleHit) => VisibleCellState.Obstacle,
        (BoardOwner.Player, CellState.Decoy) => VisibleCellState.Ship,
        (BoardOwner.Opponent, CellState.Decoy) => VisibleCellState.Unknown,
        (_, CellState.DecoyHit) => VisibleCellState.Hit,
        (_, CellState.Empty) => VisibleCellState.Empty,
        (_, CellState.Ship) => VisibleCellState.Ship,
        (_, CellState.Hit) => VisibleCellState.Hit,
        (_, CellState.Sunk) => VisibleCellState.Sunk,
        _ => VisibleCellState.Unknown,
    };

    private static GameApiException NotFound(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Not Found", 404, detail, null), 404);

    private static GameApiException Conflict(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Conflict", 409, detail, null), 409);

    private static GameApiException ValidationError(string detail) =>
        new(new ProblemDetailsDto("about:blank", "Bad Request", 400, detail, null), 400);

    private sealed class MockGame
    {
        public required Guid Id { get; init; }
        public required int BoardSize { get; init; }
        public required Difficulty Difficulty { get; init; }
        public required Board PlayerBoard { get; init; }
        public Board? ComputerBoard { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public Queue<(int X, int Y)> HuntQueue { get; } = [];
        public GameStatus Status { get; set; } = GameStatus.PlacingFleet;
        public int ShotCount { get; set; }
    }
}
