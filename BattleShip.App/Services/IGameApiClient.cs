using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public interface IGameApiClient
{
    Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default);
    Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default);
    Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default);
}
