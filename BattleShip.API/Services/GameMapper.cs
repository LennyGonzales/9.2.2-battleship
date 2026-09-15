using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public static class GameMapper
{
    public static GameDto ToDto(Game game) =>
        new(
            game.Id,
            game.Status,
            game.Status is GameStatus.PlayerWon or GameStatus.ComputerWon ? null : game.CurrentTurn,
            game.PlayerBoard.Size,
            game.ShotCount,
            game.CreatedAt);
}
