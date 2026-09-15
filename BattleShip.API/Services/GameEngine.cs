using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class GameEngine(
    IGameRepository repository,
    FleetPlacer fleetPlacer,
    PlayerTokenService tokenService) : IGameEngine
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
                Status = GameStatus.Waiting,
                ActiveParticipant = null,
                BoardSize = boardSize,
                Player1Token = tokenService.GenerateToken(),
                ShotCount = 0,
                Difficulty = difficulty
            };

            await repository.SaveAsync(waitingGame, cancellationToken);
            return waitingGame;
        }

        var player1Board = new Board(boardSize);
        var player2Board = new Board(boardSize);

        fleetPlacer.PlaceFleetRandomly(player1Board);
        fleetPlacer.PlaceFleetRandomly(player2Board);

        var game = new Game
        {
            Mode = GameMode.VsComputer,
            Player1Board = player1Board,
            Player2Board = player2Board,
            BoardSize = boardSize,
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

        if (game.Status != GameStatus.Waiting)
            throw new GameConflictException("La partie n'est pas en attente d'un second joueur.");

        if (game.Player2Token is not null)
            throw new GameConflictException("La partie est deja complete.");

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
}
