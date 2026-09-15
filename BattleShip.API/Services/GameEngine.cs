using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class GameEngine(
    IGameRepository repository,
    FleetPlacer fleetPlacer,
    PlayerTokenService tokenService,
    IComputerOpponent computerOpponent) : IGameEngine
{
    public async Task<Game> CreateGameAsync(CreateGameRequest? request, CancellationToken cancellationToken = default)
    {
        var boardSize = request?.BoardSize ?? GameOptions.DefaultBoardSize;
        var mode = request?.Mode ?? GameMode.VsComputer;
        var difficulty = request?.Difficulty ?? Difficulty.Normal;

        if (mode == GameMode.VsPlayer)
        {
            var waitingGame = new Game
            {
                Mode = GameMode.VsPlayer,
                BoardSize = boardSize,
                Player1Board = new Board(boardSize),
                Status = GameStatus.Waiting,
                ActiveParticipant = null,
                Player1Token = tokenService.GenerateToken(),
                ShotCount = 0
            };

            await repository.SaveAsync(waitingGame, cancellationToken);
            return waitingGame;
        }

        var game = new Game
        {
            Mode = GameMode.VsComputer,
            BoardSize = boardSize,
            Player1Board = new Board(boardSize),
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

        game.Player2Board = new Board(game.BoardSize);
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
            game.Player2Board = new Board(game.BoardSize);
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
