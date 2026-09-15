using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public static class GameMapper
{
    public static GameDto ToDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt);

    public static GameCreatedDto ToCreatedDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt,
            game.Mode == GameMode.VsPlayer ? game.Player1Token : null);

    public static JoinGameDto ToJoinDto(Game game) =>
        new(
            game.Id,
            game.Mode,
            game.Status,
            game.IsFinished() ? null : game.MapToPlayerSide(game.ActiveParticipant),
            game.BoardSize,
            game.ShotCount,
            game.CreatedAt,
            game.Player2Token!);
}
