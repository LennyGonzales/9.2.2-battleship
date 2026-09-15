using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class CreateGameEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public CreateGameEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostEmptyBody_Returns201WithGameDto()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(GameStatus.PlacingFleet, dto.Status);
        Assert.Null(dto.CurrentTurn);
        Assert.Equal(GameOptions.DefaultBoardSize, dto.BoardSize);
        Assert.Equal(0, dto.ShotCount);
    }

    [Fact]
    public async Task PostInvalidBoardSize_Returns400ValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { boardSize = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostVsPlayer_Returns201WithPlayerToken()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer", boardSize = 10 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(GameMode.VsPlayer, dto.Mode);
        Assert.Equal(GameStatus.Waiting, dto.Status);
        Assert.False(string.IsNullOrWhiteSpace(dto.PlayerToken));
    }

    [Fact]
    public async Task PostWithOptions_Returns201WithLocationHeader()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new
        {
            boardSize = 10,
            difficulty = "Normal"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal($"/api/games/{dto.Id}", response.Headers.Location?.ToString());
        Assert.Equal(10, dto.BoardSize);
    }
}
