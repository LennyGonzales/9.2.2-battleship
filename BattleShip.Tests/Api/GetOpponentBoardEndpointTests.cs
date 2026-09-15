using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GetOpponentBoardEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public GetOpponentBoardEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOpponentBoard_ForFreshVsComputerGame_Returns200WithNoShipsRevealed()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/games/{created.Id}/board/opponent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var board = await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions);
        Assert.NotNull(board);
        Assert.Equal(BoardOwner.Opponent, board.Owner);
        Assert.Equal(GameOptions.DefaultBoardSize, board.Size);
        Assert.Equal(board.Size * board.Size, board.Cells.Count);

        // Visibility rule: before any shot is fired, the enemy fleet must be
        // entirely hidden - no cell may reveal Ship, Hit, Sunk or Miss.
        Assert.All(board.Cells, cell => Assert.Equal(VisibleCellState.Unknown, cell.State));
    }

    [Fact]
    public async Task GetOpponentBoard_ForUnknownGame_Returns404()
    {
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}/board/opponent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetOpponentBoard_ForWaitingPvpGame_Returns409()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var response = await _client.GetAsync($"/api/games/{created.Id}/board/opponent");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
