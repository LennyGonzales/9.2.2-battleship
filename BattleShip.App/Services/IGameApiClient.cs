using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public interface IGameApiClient
{
    Task<GameCreatedDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<JoinGameDto> JoinGameAsync(Guid id, CancellationToken ct = default);
    Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default);
    Task<GameDto> PlaceFleetAsync(Guid id, PlaceFleetRequest request, string? playerToken, CancellationToken ct = default);
    Task<BoardDto> GetPlayerBoardAsync(Guid id, string? playerToken, CancellationToken ct = default);
    Task<BoardDto> GetOpponentBoardAsync(Guid id, string? playerToken, CancellationToken ct = default);
    Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, string? playerToken, CancellationToken ct = default);
}
