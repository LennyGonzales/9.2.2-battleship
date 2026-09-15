using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface IGameRepository
{
    Task SaveAsync(Game game, CancellationToken cancellationToken = default);
    Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
