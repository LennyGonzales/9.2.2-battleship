using System.Collections.Concurrent;
using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    public Task SaveAsync(Game game, CancellationToken cancellationToken = default)
    {
        _games[game.Id] = game;
        return Task.CompletedTask;
    }

    public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_games.GetValueOrDefault(id));
}
