using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Services;
using BattleShip.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

public class PowerUpEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public PowerUpEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton(ObstacleGenerationOptions.None)));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostRecon_Returns200AndHidesUndiscoveredPositions()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", new
        {
            shipName = "Porte-avions",
            orientation = "Row",
            index = 0,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PowerUpResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.Recon);
        Assert.Equal(PowerUpType.Recon, result.Type);
        Assert.Null(result.Torpedo);
        Assert.Null(result.Cells);
    }

    [Fact]
    public async Task PostPowerUp_UnknownShip_Returns400()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", new { shipName = "Fregate" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPowerUp_OnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/games/{Guid.Empty}/powerups", new
        {
            shipName = "Porte-avions",
            orientation = "Row",
            index = 0,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostPowerUp_UsedTwice_Returns409()
    {
        var gameId = await CreatePveGameAsync();
        var body = new { shipName = "Porte-avions", orientation = "Row", index = 0 };

        var first = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await _client.PostAsync($"/api/games/{gameId}/computer-turn", null);

        var second = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PostComputerTurn_OnComputerTurn_Returns200()
    {
        var gameId = await CreatePveGameAsync();
        await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 0, y = 0 });

        var response = await _client.PostAsync($"/api/games/{gameId}/computer-turn", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputerTurnResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Shot is not null || result.PowerUp is not null);
    }

    [Fact]
    public async Task PostPowerUpAfterGameFinished_Returns409()
    {
        var gameId = await CreatePveGameAsync(boardSize: 5);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var game = await repository.GetByIdAsync(gameId);
        Assert.NotNull(game);
        game.Player2Board = CreateSingleCellBoard(game.BoardSize);
        game.Player1Board = new Board(game.BoardSize);
        await repository.SaveAsync(game);

        var winningShot = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 0, y = 0 });
        winningShot.EnsureSuccessStatusCode();

        var winResult = await winningShot.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(winResult);
        Assert.Equal(GameStatus.PlayerWon, winResult.Status);

        var powerUp = await _client.PostAsJsonAsync($"/api/games/{gameId}/powerups", new
        {
            shipName = "Porte-avions",
            orientation = "Row",
            index = 0,
        });

        Assert.Equal(HttpStatusCode.Conflict, powerUp.StatusCode);
    }

    [Fact]
    public async Task GetPlayerBoard_ExposesOwnFleetPowerUpStatus()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.GetAsync($"/api/games/{gameId}/board/player");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>(JsonOptions);
        Assert.NotNull(board);
        Assert.NotNull(board.Ships);
        Assert.Equal(5, board.Ships!.Count);
        Assert.All(board.Ships, s => Assert.False(s.PowerUpUsed));
    }

    private async Task<Guid> CreatePveGameAsync(int? boardSize = null)
    {
        var response = boardSize is null
            ? await _client.PostAsJsonAsync("/api/games", new { })
            : await _client.PostAsJsonAsync("/api/games", new { boardSize });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);

        var fleetResponse = await _client.PostAsJsonAsync($"/api/games/{dto.Id}/fleet", FleetTestData.ValidFleetJson);
        fleetResponse.EnsureSuccessStatusCode();

        return dto.Id;
    }

    private static Board CreateSingleCellBoard(int size)
    {
        var board = new Board(size);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        return board;
    }
}
