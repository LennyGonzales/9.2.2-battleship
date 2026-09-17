namespace BattleShip.App.Services;

public interface IGameStatsClient
{
    Task<GameStatsLoadResult> GetGameStatsAsync(string gameId, CancellationToken ct = default);
}
