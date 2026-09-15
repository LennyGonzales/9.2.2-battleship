using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed class HttpGameApiClient(HttpClient http) : IGameApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/games", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetPlayerBoardAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}/board/player", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetOpponentBoardAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}/board/opponent", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<ShotResultDto> FireShotAsync(Guid id, ShotRequest shot, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"api/games/{id}/shots", shot, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions, ct))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        ProblemDetailsDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions, ct);
        }
        catch (JsonException)
        {
            // Body wasn't problem+json; fall back to a generic problem below.
        }

        problem ??= new ProblemDetailsDto(null, response.ReasonPhrase, statusCode, null, null);
        throw new GameApiException(problem, statusCode);
    }
}
