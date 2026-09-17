using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Core;
using Grpc.Net.Client;

namespace BattleShip.App.Services;

public sealed class GrpcGameStatsClient(GrpcChannel channel) : IGameStatsClient
{
    private readonly GameStats.GameStatsClient _client = new(channel);

    public async Task<GameStatsLoadResult> GetGameStatsAsync(string gameId, CancellationToken ct = default)
    {
        try
        {
            var reply = await _client.GetGameStatsAsync(
                new GameStatsQuery { GameId = gameId },
                cancellationToken: ct);

            var stats = new GameStatsDto(
                Guid.Parse(reply.GameId),
                reply.PlayerShots,
                reply.ComputerShots,
                reply.PlayerHits,
                reply.ComputerHits,
                reply.Status);

            return new GameStatsLoadResult(stats, "OK", null);
        }
        catch (RpcException ex)
        {
            return new GameStatsLoadResult(null, ex.StatusCode.ToString(), ex.Status.Detail);
        }
    }
}
