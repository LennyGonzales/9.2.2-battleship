using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class GameEngine(IGameRepository repository, Random random) : IGameEngine
{
    public async Task<Game> CreateGameAsync(CreateGameRequest? request, CancellationToken cancellationToken = default)
    {
        var boardSize = request?.BoardSize ?? GameOptions.DefaultBoardSize;
        var difficulty = request?.Difficulty ?? Difficulty.Normal;

        var playerBoard = new Board(boardSize);
        var computerBoard = new Board(boardSize);

        PlaceFleetRandomly(playerBoard);
        PlaceFleetRandomly(computerBoard);

        var game = new Game
        {
            PlayerBoard = playerBoard,
            ComputerBoard = computerBoard,
            Difficulty = difficulty,
            Status = GameStatus.PlayerTurn,
            CurrentTurn = PlayerSide.Player,
            ShotCount = 0
        };

        await repository.SaveAsync(game, cancellationToken);
        return game;
    }

    private void PlaceFleetRandomly(Board board)
    {
        foreach (var (name, length) in GameOptions.DefaultFleet)
        {
            var ship = new Ship { Name = name, Length = length };
            var placed = false;

            for (var attempt = 0; attempt < GameOptions.MaxPlacementAttemptsPerShip; attempt++)
            {
                var x = random.Next(board.Size);
                var y = random.Next(board.Size);
                var horizontal = random.Next(2) == 0;

                if (!board.CanPlaceShip(ship, x, y, horizontal))
                    continue;

                board.PlaceShip(ship, x, y, horizontal);
                placed = true;
                break;
            }

            if (!placed)
                throw new InvalidOperationException($"Impossible de placer la flotte sur une grille {board.Size}x{board.Size}.");
        }
    }
}
