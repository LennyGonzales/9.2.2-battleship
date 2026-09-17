using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public interface IGameStatsClient
{
    Task<GameStatsDto?> GetGameStatsAsync(Guid gameId, CancellationToken ct = default);
}
