using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GetPlayerBoardEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public GetPlayerBoardEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPlayerBoard_BeforeFleetPlacement_Returns200WithEmptyBoard()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/games/{created.Id}/board/player");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var board = await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions);
        Assert.NotNull(board);
        Assert.Equal(BoardOwner.Player, board.Owner);
        Assert.Equal(GameOptions.DefaultBoardSize, board.Size);
        Assert.Equal(board.Size * board.Size, board.Cells.Count);
        Assert.Equal(0, board.Cells.Count(c => c.State == VisibleCellState.Ship));
    }

    [Fact]
    public async Task GetPlayerBoard_AfterFleetPlacement_Returns200WithOwnFleetVisible()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(created);

        var fleetResponse = await _client.PostAsJsonAsync($"/api/games/{created.Id}/fleet", FleetTestData.ValidFleetJson);
        fleetResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/api/games/{created.Id}/board/player");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var board = await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions);
        Assert.NotNull(board);
        Assert.Equal(BoardOwner.Player, board.Owner);

        var shipCellCount = board.Cells.Count(c => c.State == VisibleCellState.Ship);
        var expectedShipCellCount = GameOptions.DefaultFleet.Sum(ship => ship.Length);
        Assert.Equal(expectedShipCellCount, shipCellCount);
    }

    [Fact]
    public async Task GetPlayerBoard_ForUnknownGame_Returns404()
    {
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}/board/player");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetPlayerBoard_ForWaitingPvpGame_Returns200WithEmptyPlayerBoard()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{created.Id}/board/player");
        request.Headers.Add(PlayerTokenHeaders.HeaderName, created.PlayerToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
