using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class PlaceFleetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PlaceFleetEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClientWithoutObstacles();
    }

    [Fact]
    public async Task PostFleet_PvE_Returns200AndPlayerTurn()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/fleet", FleetTestData.ValidFleetJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(GameStatus.PlayerTurn, dto.Status);
        Assert.Equal(PlayerSide.Player, dto.CurrentTurn);
    }

    [Fact]
    public async Task PostFleet_PvP_BothPlayers_Returns200AndPlayer1Turn()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer", boardSize = 10 });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{created.Id}/fleet")
        {
            Content = JsonContent.Create(FleetTestData.ValidFleetJson)
        };
        request1.Headers.Add(PlayerTokenHeaders.HeaderName, created.PlayerToken);
        var player1Response = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, player1Response.StatusCode);

        var joinResponse = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions);
        Assert.NotNull(joined);

        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{created.Id}/fleet")
        {
            Content = JsonContent.Create(FleetTestData.ValidFleetJson)
        };
        request2.Headers.Add(PlayerTokenHeaders.HeaderName, joined.PlayerToken);
        var player2Response = await _client.SendAsync(request2);

        Assert.Equal(HttpStatusCode.OK, player2Response.StatusCode);
        var started = await player2Response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal(GameStatus.Player1Turn, started.Status);
        Assert.Equal(PlayerSide.Player1, started.CurrentTurn);
    }

    [Fact]
    public async Task PostFleet_InvalidFleet_Returns400()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/fleet", new
        {
            ships = new[]
            {
                new { name = "Porte-avions", x = 0, y = 0, horizontal = true },
                new { name = "Croiseur", x = 1, y = 0, horizontal = true },
                new { name = "Contre-torpilleur", x = 0, y = 2, horizontal = true },
                new { name = "Sous-marin", x = 0, y = 3, horizontal = true },
                new { name = "Torpilleur", x = 0, y = 4, horizontal = true },
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostFleet_PvP_WithoutToken_Returns401()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var response = await _client.PostAsJsonAsync($"/api/games/{created.Id}/fleet", FleetTestData.ValidFleetJson);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostFleet_Twice_Returns409()
    {
        var gameId = await CreatePveGameAsync();

        var first = await _client.PostAsJsonAsync($"/api/games/{gameId}/fleet", FleetTestData.ValidFleetJson);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync($"/api/games/{gameId}/fleet", FleetTestData.ValidFleetJson);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private async Task<Guid> CreatePveGameAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(GameStatus.PlacingFleet, dto.Status);
        return dto.Id;
    }
}
