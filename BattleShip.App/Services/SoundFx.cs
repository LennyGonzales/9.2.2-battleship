using BattleShip.Models.Domain;
using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public sealed class SoundFx(IJSRuntime js)
{
    public bool Muted { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        try
        {
            Muted = await js.InvokeAsync<bool>("battleSfx.isMuted");
            Changed?.Invoke();
        }
        catch (JSException)
        {
            // Script not yet available; sounds will no-op until the next call.
        }
    }

    public async Task SetMutedAsync(bool muted)
    {
        Muted = muted;
        try
        {
            await js.InvokeVoidAsync("battleSfx.setMuted", muted);
        }
        catch (JSException)
        {
        }

        Changed?.Invoke();
    }

    public Task ToggleMutedAsync() => SetMutedAsync(!Muted);

    public async Task PlayAsync(string cue)
    {
        try
        {
            await js.InvokeVoidAsync("battleSfx.play", cue);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public Task PlayShotAsync(ShotOutcome outcome, bool incoming = false)
    {
        var cue = (outcome, incoming) switch
        {
            (ShotOutcome.Miss, false) => "miss",
            (ShotOutcome.Miss, true) => "incoming-miss",
            (ShotOutcome.Hit, false) => "hit",
            (ShotOutcome.Hit, true) => "incoming-hit",
            (ShotOutcome.Sunk, false) => "sunk",
            (ShotOutcome.Sunk, true) => "incoming-sunk",
            (ShotOutcome.Obstacle, _) => "obstacle",
            _ => "sonar",
        };

        return PlayAsync(cue);
    }
}
