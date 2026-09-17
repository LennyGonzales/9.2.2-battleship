using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class GameEngine(
    IGameRepository repository,
    FleetPlacer fleetPlacer,
    PlayerTokenService tokenService,
    IComputerOpponent computerOpponent,
    Random random,
    ObstacleGenerationOptions obstacleOptions) : IGameEngine
{
    public async Task<Game> CreateGameAsync(CreateGameRequest? request, CancellationToken cancellationToken = default)
    {
        var boardSize = request?.BoardSize ?? GameOptions.DefaultBoardSize;
        var mode = request?.Mode ?? GameMode.VsComputer;
        var difficulty = request?.Difficulty ?? Difficulty.Normal;

        if (mode == GameMode.VsPlayer)
        {
            var player1Board = new Board(boardSize);
            player1Board.PlaceObstacles(
                obstacleOptions.Count, obstacleOptions.MinSize, obstacleOptions.MaxSize, random);

            var waitingGame = new Game
            {
                Mode = GameMode.VsPlayer,
                BoardSize = boardSize,
                Player1Board = player1Board,
                Status = GameStatus.Waiting,
                ActiveParticipant = null,
                Player1Token = tokenService.GenerateToken(),
                ShotCount = 0
            };

            await repository.SaveAsync(waitingGame, cancellationToken);
            return waitingGame;
        }

        var soloBoard = new Board(boardSize);
        soloBoard.PlaceObstacles(
            obstacleOptions.Count, obstacleOptions.MinSize, obstacleOptions.MaxSize, random);

        var game = new Game
        {
            Mode = GameMode.VsComputer,
            BoardSize = boardSize,
            Player1Board = soloBoard,
            Difficulty = difficulty,
            Status = GameStatus.PlacingFleet,
            ActiveParticipant = null,
            ShotCount = 0
        };

        await repository.SaveAsync(game, cancellationToken);
        return game;
    }

    public async Task<Game> JoinGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.Mode != GameMode.VsPlayer)
            throw new GameConflictException("Cette partie n'accepte pas de second joueur.");

        if (game.Status != GameStatus.Waiting || game.Player2Token is not null)
            throw new GameConflictException("Cette partie n'est plus disponible.");

        var player2Board = new Board(game.BoardSize);
        player2Board.PlaceObstacles(
            obstacleOptions.Count, obstacleOptions.MinSize, obstacleOptions.MaxSize, random);

        game.Player2Board = player2Board;
        game.Player2Token = tokenService.GenerateToken();
        game.Status = GameStatus.PlacingFleet;
        game.ActiveParticipant = null;

        await repository.SaveAsync(game, cancellationToken);
        return game;
    }

    public async Task<Game> PlaceFleetAsync(
        Guid gameId,
        Participant? caller,
        PlaceFleetRequest request,
        CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        ValidateFleetPlacementAllowed(game, caller);

        var participant = ResolveFleetParticipant(game, caller);
        var board = game.GetBoard(participant);

        if (IsFleetComplete(board))
            throw new GameConflictException("La flotte est deja placee.");

        fleetPlacer.PlaceFleetFromRequest(board, request.Ships);

        if (game.Mode == GameMode.VsComputer)
        {
            var computerBoard = new Board(game.BoardSize);
            computerBoard.PlaceObstacles(
                obstacleOptions.Count, obstacleOptions.MinSize, obstacleOptions.MaxSize, random);

            game.Player2Board = computerBoard;
            fleetPlacer.PlaceFleetRandomly(game.Player2Board);
            game.Status = GameStatus.PlayerTurn;
            game.ActiveParticipant = Participant.Player1;
        }
        else if (IsFleetComplete(game.Player1Board) && IsFleetComplete(game.Player2Board))
        {
            game.Status = GameStatus.Player1Turn;
            game.ActiveParticipant = Participant.Player1;
        }

        await repository.SaveAsync(game, cancellationToken);
        return game;
    }

    public async Task<ShotTurnResult> FireShotAsync(
        Guid gameId,
        Participant? caller,
        int? x,
        int? y,
        CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.IsFinished() || game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
            throw new GameConflictException();

        var expectedShooter = GetExpectedShooter(game);
        ValidateCaller(game, caller, expectedShooter);
        ValidateCoordinates(game, expectedShooter, x, y);

        var (shotX, shotY) = ResolveTargetCoordinates(game, expectedShooter, x, y);
        var targetBoard = game.GetOpponentBoard(expectedShooter);

        if (!targetBoard.IsWithinBounds(shotX, shotY))
            throw new ShotOutOfBoundsException(shotX, shotY);

        if (targetBoard.IsAlreadyTargeted(shotX, shotY))
            throw new GameConflictException();

        var resolution = targetBoard.ResolveShot(shotX, shotY);

        if (IsHumanShooter(game, expectedShooter))
            game.ShotCount++;

        game.Status = ResolveStatusAfterShot(game, expectedShooter, targetBoard);
        game.ActiveParticipant = GetActiveParticipantForStatus(game.Status);

        await repository.SaveAsync(game, cancellationToken);
        return new ShotTurnResult(resolution, expectedShooter, game.Status);
    }

    public async Task<PowerUpTurnResult> UsePowerUpAsync(
        Guid gameId,
        Participant? caller,
        UsePowerUpRequest request,
        CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.IsFinished() || game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
            throw new GameConflictException();

        var expectedShooter = GetExpectedShooter(game);
        ValidateCaller(game, caller, expectedShooter);

        // A client hitting this endpoint directly can never legitimately be resolved as Player2 in a
        // VsComputer game (there is no Player2 token to present), so this is only ever a client trying
        // to puppet the computer's power-up choice. The computer's own move is played internally via
        // ResolvePowerUpForShooterAsync (see PlayComputerTurnAsync), which never goes through this guard.
        if (game.Mode == GameMode.VsComputer && expectedShooter == Participant.Player2)
            throw new GameConflictException("Le power-up de l'ordinateur ne peut pas etre declenche par un client.");

        return await ResolvePowerUpForShooterAsync(game, expectedShooter, request, cancellationToken);
    }

    public async Task<ComputerTurnResult> PlayComputerTurnAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var game = await repository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new GameNotFoundException();

        if (game.Mode != GameMode.VsComputer || game.Status != GameStatus.ComputerTurn)
            throw new GameConflictException();

        var request = computerOpponent.ChoosePowerUp(game);
        if (request is not null)
        {
            var powerUpResult = await ResolvePowerUpForShooterAsync(game, Participant.Player2, request, cancellationToken);
            return new ComputerTurnResult(true, null, powerUpResult);
        }

        var shotResult = await FireShotAsync(gameId, null, null, null, cancellationToken);
        return new ComputerTurnResult(false, shotResult, null);
    }

    private async Task<PowerUpTurnResult> ResolvePowerUpForShooterAsync(
        Game game,
        Participant expectedShooter,
        UsePowerUpRequest request,
        CancellationToken cancellationToken)
    {
        var ownBoard = game.GetBoard(expectedShooter);
        var ship = ownBoard.Ships.FirstOrDefault(s => s.Name == request.ShipName)
            ?? throw new GameConflictException("Navire inconnu.");

        if (!ship.CanUsePowerUp)
            throw new GameConflictException("Power-up indisponible pour ce navire.");

        var opponentBoard = game.GetOpponentBoard(expectedShooter);
        var content = ship.PowerUpType switch
        {
            PowerUpType.Recon => ResolveRecon(opponentBoard, request),
            PowerUpType.Torpedo => ResolveTorpedo(opponentBoard, request),
            PowerUpType.TwinStrike => ResolveMultiStrike(opponentBoard, request, requireAdjacent: true),
            PowerUpType.DoubleStrike => ResolveMultiStrike(opponentBoard, request, requireAdjacent: false),
            PowerUpType.Decoy => ResolveDecoy(ownBoard, ship, request),
            _ => throw new InvalidOperationException("Power-up inconnu.")
        };

        ship.MarkPowerUpUsed();

        var shotsResolved = (content.Torpedo is not null ? 1 : 0) + (content.Cells?.Count ?? 0);
        if (IsHumanShooter(game, expectedShooter))
            game.ShotCount += shotsResolved;

        game.Status = ResolveStatusAfterShot(game, expectedShooter, opponentBoard);
        game.ActiveParticipant = GetActiveParticipantForStatus(game.Status);

        await repository.SaveAsync(game, cancellationToken);

        return new PowerUpTurnResult(
            ship.Name, ship.PowerUpType, expectedShooter, game.Status,
            content.Recon, content.Torpedo, content.Cells);
    }

    private readonly record struct PowerUpContent(
        ReconOutcome? Recon,
        ShotResolution? Torpedo,
        IReadOnlyList<ShotResolution>? Cells);

    private static (Orientation Orientation, int Index) RequireLine(UsePowerUpRequest request)
    {
        if (request.Orientation is not { } orientation || request.Index is not { } index)
            throw new GameConflictException("Ligne ou colonne requise pour ce power-up.");
        return (orientation, index);
    }

    private static PowerUpContent ResolveRecon(Board opponentBoard, UsePowerUpRequest request)
    {
        var (orientation, index) = RequireLine(request);
        if (index < 0 || index >= opponentBoard.Size)
            throw new ShotOutOfBoundsException(index, index);

        var hasContact = opponentBoard.ScanLine(orientation, index);
        return new PowerUpContent(new ReconOutcome(orientation, index, hasContact), null, null);
    }

    private static PowerUpContent ResolveTorpedo(Board opponentBoard, UsePowerUpRequest request)
    {
        var (orientation, index) = RequireLine(request);
        if (index < 0 || index >= opponentBoard.Size)
            throw new ShotOutOfBoundsException(index, index);

        var entryEdge = request.EntryEdge ?? throw new GameConflictException("Bord d'entree requis pour la torpille.");
        var resolution = opponentBoard.FireTorpedo(orientation, index, entryEdge);
        return new PowerUpContent(null, resolution, null);
    }

    private static PowerUpContent ResolveMultiStrike(Board opponentBoard, UsePowerUpRequest request, bool requireAdjacent)
    {
        var cells = request.Cells;
        if (cells is not { Count: 2 })
            throw new GameConflictException("Deux cases sont requises pour ce power-up.");

        var (a, b) = (cells[0], cells[1]);
        if (a.X == b.X && a.Y == b.Y)
            throw new GameConflictException("Les deux cases doivent etre distinctes.");

        if (requireAdjacent && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1)
            throw new GameConflictException("Les deux cases doivent etre adjacentes.");

        foreach (var cell in cells)
        {
            if (!opponentBoard.IsWithinBounds(cell.X, cell.Y))
                throw new ShotOutOfBoundsException(cell.X, cell.Y);
            if (opponentBoard.IsAlreadyTargeted(cell.X, cell.Y))
                throw new GameConflictException("Cette case a deja ete ciblee.");
        }

        var results = cells.Select(c => opponentBoard.ResolveShot(c.X, c.Y)).ToList();
        return new PowerUpContent(null, null, results);
    }

    private static PowerUpContent ResolveDecoy(Board ownBoard, Ship ship, UsePowerUpRequest request)
    {
        if (request.Cells is not { Count: 1 })
            throw new GameConflictException("Une case est requise pour le leurre.");

        var target = request.Cells[0];
        if (!ship.Cells.Any(c => Math.Abs(c.X - target.X) + Math.Abs(c.Y - target.Y) == 1))
            throw new GameConflictException("Le leurre doit etre adjacent au contre-torpilleur.");

        if (!ownBoard.TryPlaceDecoy(target.X, target.Y))
            throw new GameConflictException("Impossible de placer le leurre ici.");

        return new PowerUpContent(null, null, null);
    }

    private static void ValidateFleetPlacementAllowed(Game game, Participant? caller)
    {
        if (game.Mode == GameMode.VsComputer)
        {
            if (game.Status != GameStatus.PlacingFleet)
                throw new GameConflictException("Le placement de flotte n'est pas autorise dans cet etat.");

            return;
        }

        if (caller is null)
            throw new InvalidPlayerTokenException();

        if (game.Status is not (GameStatus.Waiting or GameStatus.PlacingFleet))
            throw new GameConflictException("Le placement de flotte n'est pas autorise dans cet etat.");

        if (game.Status == GameStatus.Waiting && caller != Participant.Player1)
            throw new GameConflictException("Le joueur 2 ne peut placer sa flotte qu'apres avoir rejoint la partie.");
    }

    private static Participant ResolveFleetParticipant(Game game, Participant? caller) =>
        game.Mode switch
        {
            GameMode.VsComputer => Participant.Player1,
            GameMode.VsPlayer => caller ?? throw new InvalidPlayerTokenException(),
            _ => throw new InvalidOperationException("Mode de jeu inconnu.")
        };

    private static bool IsFleetComplete(Board? board) =>
        board?.Ships.Count == GameOptions.DefaultFleet.Length;

    private static Participant GetExpectedShooter(Game game) =>
        game.Status switch
        {
            GameStatus.PlayerTurn or GameStatus.Player1Turn => Participant.Player1,
            GameStatus.ComputerTurn or GameStatus.Player2Turn => Participant.Player2,
            _ => throw new GameConflictException()
        };

    private static void ValidateCaller(Game game, Participant? caller, Participant expectedShooter)
    {
        if (game.Mode != GameMode.VsPlayer)
            return;

        if (caller is null)
            throw new InvalidPlayerTokenException();

        if (caller != expectedShooter)
            throw new GameConflictException();
    }

    private static void ValidateCoordinates(Game game, Participant expectedShooter, int? x, int? y)
    {
        var hasCoords = x.HasValue || y.HasValue;
        var isComputerShot = game.Mode == GameMode.VsComputer && expectedShooter == Participant.Player2;

        if (isComputerShot)
        {
            if (hasCoords)
                throw new GameConflictException("Coordonnees non attendues pour le tour de l'ordinateur.");
            return;
        }

        if (!x.HasValue || !y.HasValue)
            throw new GameConflictException("Coordonnees requises pour ce tour.");
    }

    private (int X, int Y) ResolveTargetCoordinates(
        Game game,
        Participant expectedShooter,
        int? x,
        int? y)
    {
        if (game.Mode == GameMode.VsComputer && expectedShooter == Participant.Player2)
            return computerOpponent.ChooseShot(game);

        return (x!.Value, y!.Value);
    }

    private static bool IsHumanShooter(Game game, Participant shooter) =>
        game.Mode == GameMode.VsPlayer
        || shooter == Participant.Player1;

    private static GameStatus ResolveStatusAfterShot(Game game, Participant shooter, Board targetBoard)
    {
        if (targetBoard.AreAllShipsSunk())
            return ResolveWinStatus(game, shooter);

        return game.Mode switch
        {
            GameMode.VsComputer => shooter == Participant.Player1
                ? GameStatus.ComputerTurn
                : GameStatus.PlayerTurn,
            GameMode.VsPlayer => shooter == Participant.Player1
                ? GameStatus.Player2Turn
                : GameStatus.Player1Turn,
            _ => throw new InvalidOperationException("Mode de jeu inconnu.")
        };
    }

    private static GameStatus ResolveWinStatus(Game game, Participant shooter) =>
        game.Mode switch
        {
            GameMode.VsComputer => shooter == Participant.Player1
                ? GameStatus.PlayerWon
                : GameStatus.ComputerWon,
            GameMode.VsPlayer => shooter == Participant.Player1
                ? GameStatus.Player1Won
                : GameStatus.Player2Won,
            _ => throw new InvalidOperationException("Mode de jeu inconnu.")
        };

    private static Participant? GetActiveParticipantForStatus(GameStatus status) =>
        status switch
        {
            GameStatus.PlayerTurn or GameStatus.Player1Turn => Participant.Player1,
            GameStatus.ComputerTurn or GameStatus.Player2Turn => Participant.Player2,
            _ => null
        };
}
