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
                Status = GameStatus.Waiting,
                ActiveParticipant = null,
                Player1Token = tokenService.GenerateToken(),
                ShotCount = 0
            };

            await repository.SaveAsync(waitingGame, cancellationToken);
            return waitingGame;
        }

        var playerBoard = new Board(boardSize);
        var computerBoard = new Board(boardSize);

        fleetPlacer.PlaceFleetRandomly(playerBoard);
        fleetPlacer.PlaceFleetRandomly(computerBoard);

        var game = new Game
        {
            Mode = GameMode.VsComputer,
            BoardSize = boardSize,
            Player1Board = playerBoard,
            Player2Board = computerBoard,
            Difficulty = difficulty,
            Status = GameStatus.PlayerTurn,
            ActiveParticipant = Participant.Player1,
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

        var player1Board = new Board(game.BoardSize);
        var player2Board = new Board(game.BoardSize);

        fleetPlacer.PlaceFleetRandomly(player1Board);
        fleetPlacer.PlaceFleetRandomly(player2Board);

        game.Player1Board = player1Board;
        game.Player2Board = player2Board;
        game.Player2Token = tokenService.GenerateToken();
        game.Status = GameStatus.Player1Turn;
        game.ActiveParticipant = Participant.Player1;

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

        if (game.IsFinished() || game.Status == GameStatus.Waiting)
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
