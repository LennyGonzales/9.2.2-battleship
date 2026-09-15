using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class JoinGameEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public JoinGameEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JoinWaitingPvpGame_Returns200WithPlayerToken()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer", boardSize = 10 });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var joinResponse = await _client.PostAsync($"/api/games/{created.Id}/join", null);

        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);

        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions);
        Assert.NotNull(joined);
        Assert.Equal(GameStatus.Player1Turn, joined.Status);
        Assert.Equal(PlayerSide.Player1, joined.CurrentTurn);
        Assert.False(string.IsNullOrWhiteSpace(joined.PlayerToken));
    }

    [Fact]
    public async Task JoinUnknownGame_Returns404()
    {
        var response = await _client.PostAsync($"/api/games/{Guid.Empty}/join", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JoinAlreadyJoinedGame_Returns409()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var firstJoin = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        Assert.Equal(HttpStatusCode.OK, firstJoin.StatusCode);

        var secondJoin = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        Assert.Equal(HttpStatusCode.Conflict, secondJoin.StatusCode);
    }
}
