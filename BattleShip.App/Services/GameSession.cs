using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class GameSession(IGameApiClient client)
{
    private readonly List<ShotResultDto> _history = [];

    public GameDto? Game { get; private set; }
    public BoardDto? PlayerBoard { get; private set; }
    public BoardDto? OpponentBoard { get; private set; }
    public string? AlertMessage { get; private set; }
    public bool IsBusy { get; private set; }
    public IReadOnlyList<ShotResultDto> History => _history;

    public event Action? Changed;

    public Task StartGameAsync(int boardSize, Difficulty difficulty) => RunAsync(async () =>
    {
        _history.Clear();
        Game = await client.CreateGameAsync(new CreateGameRequest(boardSize, difficulty));
        await RefreshBoardsAsync();
    });

    public Task LoadGameAsync(Guid id) => RunAsync(async () =>
    {
        Game = await client.GetGameAsync(id);
        await RefreshBoardsAsync();
    });

    public Task FireShotAsync(int x, int y) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.FireShotAsync(Game.Id, new ShotRequest(x, y));
        _history.Add(result);
        Game = Game with { Status = result.Status, ShotCount = Game.ShotCount + 1 };
        await RefreshBoardsAsync();
    });

    private async Task RefreshBoardsAsync()
    {
        if (Game is null)
        {
            return;
        }

        PlayerBoard = await client.GetPlayerBoardAsync(Game.Id);
        OpponentBoard = await client.GetOpponentBoardAsync(Game.Id);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        AlertMessage = null;
        Changed?.Invoke();
        try
        {
            await action();
        }
        catch (GameApiException ex)
        {
            AlertMessage = ex.Problem.Detail ?? ex.Problem.Title ?? "Une erreur inattendue est survenue.";
        }
        catch (Exception)
        {
            AlertMessage = "Liaison avec le QG interrompue.";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }
}
