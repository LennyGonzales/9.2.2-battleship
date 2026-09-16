using BattleShip.API.Services;
using BattleShip.Grpc;
using BattleShip.Models.Services;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public sealed class GameStatsGrpcService(
    IValidator<GameStatsQuery> validator,
    IGameRepository repository,
    GameStatsCalculator calculator) : GameStats.GameStatsBase
{
    public override async Task<GameStatsReply> GetGameStats(
        GameStatsQuery request,
        ServerCallContext context)
    {
        var validation = await validator.ValidateAsync(request, context.CancellationToken);
        if (!validation.IsValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, validation.ToString()));

        if (!Guid.TryParse(request.GameId, out var gameId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "L'identifiant de partie doit etre un GUID valide."));

        var game = await repository.GetByIdAsync(gameId, context.CancellationToken);
        if (game is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Partie inconnue"));

        return calculator.ToReply(game);
    }
}
