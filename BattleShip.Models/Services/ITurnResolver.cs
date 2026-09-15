using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface ITurnResolver
{
    Task<TurnResolution> ResolveShotAsync(
        Game game,
        Participant shooter,
        int x,
        int y,
        CancellationToken cancellationToken = default);
}
