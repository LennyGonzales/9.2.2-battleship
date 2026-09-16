using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Core;
using Grpc.Net.Client;

namespace BattleShip.App.Services;

public sealed class GrpcGameStatsClient(GrpcChannel channel) : IGameStatsClient
{
    private readonly GameStats.GameStatsClient _client = new(channel);

    public async Task<GameStatsDto?> GetGameStatsAsync(Guid gameId, CancellationToken ct = default)
    {
        try
        {
            var reply = await _client.GetGameStatsAsync(
                new GameStatsQuery { GameId = gameId.ToString() },
                cancellationToken: ct);

            return new GameStatsDto(
                Guid.Parse(reply.GameId),
                reply.PlayerShots,
                reply.ComputerShots,
                reply.PlayerHits,
                reply.ComputerHits,
                reply.Status);
        }
        catch (RpcException)
        {
            return null;
        }
    }
}
