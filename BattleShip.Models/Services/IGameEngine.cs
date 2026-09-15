using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface IGameEngine
{
    Task<Game> CreateGameAsync(CreateGameRequest? request, CancellationToken cancellationToken = default);
    Task<Game> JoinGameAsync(Guid gameId, CancellationToken cancellationToken = default);
}
