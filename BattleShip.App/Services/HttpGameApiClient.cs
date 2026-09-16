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

    public async Task<GameCreatedDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/games", request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions, ct))!;
    }

    public async Task<JoinGameDto> JoinGameAsync(Guid id, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/games/{id}/join");
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions, ct))!;
    }

    public async Task<GameDto> GetGameAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"api/games/{id}", ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<GameDto> PlaceFleetAsync(
        Guid id, PlaceFleetRequest request, string? playerToken, CancellationToken ct = default)
    {
        using var httpRequest = CreateRequest(HttpMethod.Post, $"api/games/{id}/fleet", playerToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        using var response = await http.SendAsync(httpRequest, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetPlayerBoardAsync(Guid id, string? playerToken, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/games/{id}/board/player", playerToken);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<BoardDto> GetOpponentBoardAsync(Guid id, string? playerToken, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/games/{id}/board/opponent", playerToken);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions, ct))!;
    }

    public async Task<ShotResultDto> FireShotAsync(
        Guid id, ShotRequest shot, string? playerToken, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/games/{id}/shots", playerToken);
        request.Content = JsonContent.Create(shot, options: JsonOptions);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions, ct))!;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string? playerToken)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(playerToken))
        {
            request.Headers.Add(PlayerTokenHeaders.HeaderName, playerToken);
        }
        return request;
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
