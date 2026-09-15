using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface IGameEngine
{
    Task<Game> CreateGameAsync(CreateGameRequest? request, CancellationToken cancellationToken = default);
    Task<Game> JoinGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<Game> PlaceFleetAsync(
        Guid gameId,
        Participant? caller,
        PlaceFleetRequest request,
        CancellationToken cancellationToken = default);
    Task<ShotTurnResult> FireShotAsync(
        Guid gameId,
        Participant? caller,
        int? x,
        int? y,
        CancellationToken cancellationToken = default);
}
