using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GetGameEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public GetGameEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetExistingGame_Returns200WithGameDto()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/games/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var dto = await getResponse.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(GameStatus.PlayerTurn, dto.Status);
        Assert.Equal(PlayerSide.Player, dto.CurrentTurn);
        Assert.Equal(GameOptions.DefaultBoardSize, dto.BoardSize);
        Assert.Equal(0, dto.ShotCount);
    }

    [Fact]
    public async Task GetUnknownGame_Returns404()
    {
        var unknownId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/games/{unknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Partie inconnue", body);
    }
}
