using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed class GameSession(IGameApiClient client, IJSRuntime js, IServiceProvider services)
{
    private readonly List<ShotResultDto> _history = [];

    public GameDto? Game { get; private set; }
    public GameStatsDto? Stats { get; private set; }
    public BoardDto? PlayerBoard { get; private set; }
    public BoardDto? OpponentBoard { get; private set; }
    public string? AlertMessage { get; private set; }
    public bool IsBusy { get; private set; }
    public string? PlayerToken { get; private set; }
    public PlayerSide? MySide { get; private set; }
    public IReadOnlyList<ShotResultDto> History => _history;

    public bool CanFire => !IsBusy && Game is not null && MySide is not null && Game.CurrentTurn == MySide;
    public bool AwaitingOpponent => Game?.Status == GameStatus.Waiting;
    public bool CanJoin => Game is { Mode: GameMode.VsPlayer, Status: GameStatus.Waiting } && MySide is null;
    public bool IsPlacingFleet => Game?.Status == GameStatus.PlacingFleet;

    public event Action? Changed;

    private CancellationTokenSource? _pollCts;

    public void StartPolling()
    {
        if (Game is not { Mode: GameMode.VsPlayer } || MySide is null)
        {
            return;
        }

        StopPolling();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (ct.IsCancellationRequested || Game is null)
            {
                return;
            }

            if (IsFinished(Game.Status))
            {
                return;
            }

            if (CanFire)
            {
                continue;
            }

            try
            {
                var latest = await client.GetGameAsync(Game.Id, ct);
                if (latest.Status != Game.Status)
                {
                    Game = latest;
                    await RefreshBoardsAsync();
                    await RefreshStatsAsync(ct);
                    Changed?.Invoke();
                }
            }
            catch
            {
                // Transient polling failure: retry on the next tick.
            }
        }
    }

    private static bool IsFinished(GameStatus status) =>
        status is GameStatus.PlayerWon or GameStatus.ComputerWon or GameStatus.Player1Won or GameStatus.Player2Won;

    public Task StartGameAsync(int boardSize, Difficulty difficulty, GameMode mode) => RunAsync(async () =>
    {
        _history.Clear();
        var created = await client.CreateGameAsync(new CreateGameRequest(boardSize, difficulty, mode));
        Game = ToGameDto(created.Id, created.Mode, created.Status, created.CurrentTurn, created.BoardSize,
            created.ShotCount, created.CreatedAt);
        MySide = mode == GameMode.VsPlayer ? PlayerSide.Player1 : PlayerSide.Player;
        PlayerToken = created.PlayerToken;
        await PersistTokenAsync();
        await RefreshBoardsAsync();
    });

    public Task LoadGameAsync(Guid id) => RunAsync(async () =>
    {
        _history.Clear();
        Game = await client.GetGameAsync(id);
        if (Game.Mode == GameMode.VsComputer)
        {
            MySide = PlayerSide.Player;
            PlayerToken = null;
        }
        else
        {
            (PlayerToken, MySide) = await RestoreTokenAsync(id);
        }
        await RefreshBoardsAsync();
        await RefreshStatsAsync();
    });

    public Task JoinGameAsync(Guid id) => RunAsync(async () =>
    {
        var joined = await client.JoinGameAsync(id);
        Game = ToGameDto(joined.Id, joined.Mode, joined.Status, joined.CurrentTurn, joined.BoardSize,
            joined.ShotCount, joined.CreatedAt);
        MySide = PlayerSide.Player2;
        PlayerToken = joined.PlayerToken;
        await PersistTokenAsync();
        await RefreshBoardsAsync();
    });

    public Task PlaceFleetAsync(IReadOnlyList<PlaceShipRequest> ships) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        Game = await client.PlaceFleetAsync(Game.Id, new PlaceFleetRequest(ships), PlayerToken);
        await RefreshBoardsAsync();
    });

    public Task FireShotAsync(int x, int y) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.FireShotAsync(Game.Id, new ShotRequest(x, y), PlayerToken);
        _history.Add(result);

        if (result.Status is GameStatus.ComputerTurn)
        {
            var computerResult = await client.FireShotAsync(Game.Id, new ShotRequest(null, null), PlayerToken);
            _history.Add(computerResult);
        }

        Game = await client.GetGameAsync(Game.Id);
        await RefreshBoardsAsync();
        await RefreshStatsAsync();
    });

    private async Task RefreshStatsAsync(CancellationToken ct = default)
    {
        if (Game is null)
        {
            Stats = null;
            return;
        }

        var statsClient = services.GetService<IGameStatsClient>();
        if (statsClient is null)
        {
            Stats = null;
            return;
        }

        try
        {
            Stats = await statsClient.GetGameStatsAsync(Game.Id, ct);
        }
        catch
        {
            Stats = null;
        }
    }

    private async Task RefreshBoardsAsync()
    {
        if (Game is null || Game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
        {
            return;
        }

        PlayerBoard = await client.GetPlayerBoardAsync(Game.Id, PlayerToken);
        OpponentBoard = await client.GetOpponentBoardAsync(Game.Id, PlayerToken);
    }

    private async Task PersistTokenAsync()
    {
        if (Game is null || PlayerToken is null || MySide is null)
        {
            return;
        }

        try
        {
            await js.InvokeVoidAsync("sessionStorage.setItem", TokenKey(Game.Id), $"{MySide}:{PlayerToken}");
        }
        catch (JSException)
        {
            // Session storage unavailable (e.g. private browsing); the token just won't survive a refresh.
        }
    }

    private async Task<(string? Token, PlayerSide? Side)> RestoreTokenAsync(Guid id)
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey(id));
            if (string.IsNullOrEmpty(raw))
            {
                return (null, null);
            }

            var parts = raw.Split(':', 2);
            if (parts.Length != 2 || !Enum.TryParse<PlayerSide>(parts[0], out var side))
            {
                return (null, null);
            }

            return (parts[1], side);
        }
        catch (JSException)
        {
            return (null, null);
        }
    }

    private static string TokenKey(Guid id) => $"battleship.token.{id}";

    private static GameDto ToGameDto(
        Guid id, GameMode mode, GameStatus status, PlayerSide? currentTurn,
        int boardSize, int shotCount, DateTimeOffset createdAt) =>
        new(id, mode, status, currentTurn, boardSize, shotCount, createdAt);

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
