using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class MissionStatsLookup(IServiceProvider services)
{
    public GameStatsDto? Stats { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsBusy { get; private set; }

    public bool IsAvailable => services.GetService<IGameStatsClient>() is not null;

    public event Action? Changed;

    public async Task LookupAsync(string rawId, CancellationToken ct = default)
    {
        var statsClient = services.GetService<IGameStatsClient>();
        if (statsClient is null)
        {
            return;
        }

        IsBusy = true;
        Stats = null;
        ErrorMessage = null;
        Changed?.Invoke();

        try
        {
            var normalizedId = NormalizeMissionId(rawId);
            var result = await statsClient.GetGameStatsAsync(normalizedId, ct);

            if (result.IsSuccess)
            {
                Stats = result.Stats;
                ErrorMessage = null;
            }
            else
            {
                Stats = null;
                ErrorMessage = FormatError(result.StatusCode, result.Detail);
            }
        }
        catch
        {
            Stats = null;
            ErrorMessage = "Liaison stats interrompue.";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        Stats = null;
        ErrorMessage = null;
        Changed?.Invoke();
    }

    internal static string NormalizeMissionId(string rawId)
    {
        var value = rawId.Trim();
        var battleMarker = "/battle/";
        var battleIndex = value.LastIndexOf(battleMarker, StringComparison.OrdinalIgnoreCase);
        if (battleIndex >= 0)
        {
            value = value[(battleIndex + battleMarker.Length)..];
        }

        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
        {
            value = value[..queryIndex];
        }

        return value.Trim().TrimEnd('/');
    }

    private static string FormatError(string? statusCode, string? detail) =>
        statusCode switch
        {
            "InvalidArgument" => string.IsNullOrWhiteSpace(detail)
                ? "Identifiant illisible (InvalidArgument)."
                : $"Identifiant illisible (InvalidArgument) : {detail}",
            "NotFound" => string.IsNullOrWhiteSpace(detail)
                ? "Partie inconnue (NotFound)."
                : $"Partie inconnue (NotFound) : {detail}",
            _ => string.IsNullOrWhiteSpace(detail)
                ? "Liaison stats interrompue."
                : $"Liaison stats interrompue : {detail}",
        };
}
