using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed class GameSession(IGameApiClient client, IJSRuntime js, IServiceProvider services, SoundFx sfx)
{
    private readonly List<TurnLogEntry> _history = [];
    private bool _finishCuePlayed;

    public GameDto? Game { get; private set; }
    public GameStatsDto? Stats { get; private set; }
    public BoardDto? PlayerBoard { get; private set; }
    public BoardDto? OpponentBoard { get; private set; }
    public string? AlertMessage { get; private set; }
    public bool IsBusy { get; private set; }
    public string? PlayerToken { get; private set; }
    public PlayerSide? MySide { get; private set; }
    public IReadOnlyList<TurnLogEntry> History => _history;

    public bool CanFire => !IsBusy && Game is not null && MySide is not null && Game.CurrentTurn == MySide;
    public bool AwaitingOpponent => Game?.Status == GameStatus.Waiting;
    public bool CanJoin => Game is { Mode: GameMode.VsPlayer, Status: GameStatus.Waiting } && MySide is null;
    public bool IsPlacingFleet => Game?.Status == GameStatus.PlacingFleet;
    public bool HasPlacedFleet { get; private set; }
    public bool NeedsFleetPlacement => Game?.Status == GameStatus.PlacingFleet && !HasPlacedFleet;
    public bool AwaitingOpponentFleet =>
        Game is { Mode: GameMode.VsPlayer, Status: GameStatus.PlacingFleet } && HasPlacedFleet;

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
                if (latest.Status != Game.Status || latest.ShotCount != Game.ShotCount)
                {
                    var previousBoard = PlayerBoard;
                    Game = latest;
                    await RefreshBoardsAsync();
                    await RefreshStatsAsync(ct);
                    await PlayIncomingFromBoardDiffAsync(previousBoard, PlayerBoard);
                    await PlayFinishIfNeededAsync();
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
        HasPlacedFleet = false;
        _finishCuePlayed = false;
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
        HasPlacedFleet = false;
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
        _finishCuePlayed = IsFinished(Game.Status);
    });

    public Task JoinGameAsync(Guid id) => RunAsync(async () =>
    {
        HasPlacedFleet = false;
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
        _history.Add(TurnLogEntry.FromShot(result));

        await PlayShotCueAsync(result);

        Game = await client.GetGameAsync(Game.Id);
        await RefreshBoardsAsync();
        await RefreshStatsAsync();
        Changed?.Invoke();

        if (Game.Mode == GameMode.VsComputer && result.Status is GameStatus.ComputerTurn)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await PlayComputerTurnAsync();

            Game = await client.GetGameAsync(Game.Id);
            await RefreshBoardsAsync();
            await RefreshStatsAsync();
        }

        await PlayFinishIfNeededAsync();
    });

    public Task UsePowerUpAsync(UsePowerUpRequest request) => RunAsync(async () =>
    {
        if (Game is null)
        {
            return;
        }

        var result = await client.UsePowerUpAsync(Game.Id, request, PlayerToken);
        _history.Add(TurnLogEntry.FromPowerUp(result));

        if (Game.Mode == GameMode.VsComputer && result.Status is GameStatus.ComputerTurn)
        {
            await PlayComputerTurnAsync();
        }

        Game = await client.GetGameAsync(Game.Id);
        await RefreshBoardsAsync();
    });

    private async Task PlayComputerTurnAsync()
    {
        if (Game is null)
        {
            return;
        }

        var computerResult = await client.PlayComputerTurnAsync(Game.Id, PlayerToken);
        if (computerResult.UsedPowerUp)
        {
            _history.Add(TurnLogEntry.FromPowerUp(computerResult.PowerUp!));
            return;
        }

        _history.Add(TurnLogEntry.FromShot(computerResult.Shot!));
        await PlayShotCueAsync(computerResult.Shot!);
    }

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

        var result = await statsClient.GetGameStatsAsync(Game.Id.ToString(), ct);
        Stats = result.Stats;
    }

    private async Task RefreshBoardsAsync()
    {
        if (Game is null)
        {
            return;
        }

        if (Game.Mode == GameMode.VsPlayer && string.IsNullOrWhiteSpace(PlayerToken))
        {
            return;
        }

        PlayerBoard = await client.GetPlayerBoardAsync(Game.Id, PlayerToken);

        SyncHasPlacedFleetFromBoard();

        if (Game.Status is GameStatus.Waiting or GameStatus.PlacingFleet)
        {
            return;
        }

        OpponentBoard = await client.GetOpponentBoardAsync(Game.Id, PlayerToken);
    }

    private void SyncHasPlacedFleetFromBoard()
    {
        if (Game is not { Mode: GameMode.VsPlayer, Status: GameStatus.PlacingFleet })
        {
            return;
        }

        HasPlacedFleet = BoardHasCompleteFleet(PlayerBoard);
    }

    private static bool BoardHasCompleteFleet(BoardDto? board)
    {
        if (board is null)
        {
            return false;
        }

        var expectedShipCells = GameOptions.DefaultFleet.Sum(ship => ship.Length);
        return board.Cells.Count(cell => cell.State == VisibleCellState.Ship) == expectedShipCells;
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

    private Task PlayShotCueAsync(ShotResultDto result)
    {
        var incoming = MySide is not null && result.Shooter != MySide;
        return sfx.PlayShotAsync(result.Shot.Outcome, incoming);
    }

    private Task PlayIncomingFromBoardDiffAsync(BoardDto? before, BoardDto? after)
    {
        var cue = DetectIncomingCue(before, after);
        return cue is null ? Task.CompletedTask : sfx.PlayAsync(cue);
    }

    private static string? DetectIncomingCue(BoardDto? before, BoardDto? after)
    {
        if (before is null || after is null)
        {
            return null;
        }

        var previous = before.Cells.ToDictionary(cell => (cell.X, cell.Y), cell => cell.State);
        var sunk = 0;
        var hits = 0;
        var misses = 0;
        var obstacles = 0;

        foreach (var cell in after.Cells)
        {
            if (!previous.TryGetValue((cell.X, cell.Y), out var prior) || prior == cell.State)
            {
                continue;
            }

            switch (cell.State)
            {
                case VisibleCellState.Sunk:
                    sunk++;
                    break;
                case VisibleCellState.Hit:
                    hits++;
                    break;
                case VisibleCellState.Miss:
                    misses++;
                    break;
                case VisibleCellState.ObstacleHit:
                    obstacles++;
                    break;
            }
        }

        if (sunk > 0)
        {
            return "incoming-sunk";
        }

        if (hits > 0)
        {
            return "incoming-hit";
        }

        if (obstacles > 0)
        {
            return "obstacle";
        }

        return misses > 0 ? "incoming-miss" : null;
    }

    private async Task PlayFinishIfNeededAsync()
    {
        if (Game is null || _finishCuePlayed || !IsFinished(Game.Status))
        {
            return;
        }

        _finishCuePlayed = true;
        await Task.Delay(450);
        await sfx.PlayAsync(IWon ? "victory" : "defeat");
    }

    private bool IWon => Game?.Status switch
    {
        GameStatus.PlayerWon => MySide == PlayerSide.Player,
        GameStatus.ComputerWon => MySide == PlayerSide.Computer,
        GameStatus.Player1Won => MySide == PlayerSide.Player1,
        GameStatus.Player2Won => MySide == PlayerSide.Player2,
        _ => false,
    };

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
