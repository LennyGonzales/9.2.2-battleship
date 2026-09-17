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

public class FireShotEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public FireShotEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton(ObstacleGenerationOptions.None)));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostPlayerShotThenComputerShot_Returns200()
    {
        var gameId = await CreatePveGameAsync();

        var playerResponse = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 4, y = 7 });
        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);

        var playerResult = await playerResponse.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(playerResult);
        Assert.Equal(PlayerSide.Player, playerResult.Shooter);
        Assert.Equal(GameStatus.ComputerTurn, playerResult.Status);

        var computerResponse = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { });
        Assert.Equal(HttpStatusCode.OK, computerResponse.StatusCode);

        var computerResult = await computerResponse.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(computerResult);
        Assert.Equal(PlayerSide.Computer, computerResult.Shooter);
        Assert.True(computerResult.Status is GameStatus.PlayerTurn or GameStatus.ComputerWon);
    }

    [Fact]
    public async Task PostShotOnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/games/{Guid.Empty}/shots",
            new { x = 0, y = 0 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostShotOnAlreadyTargetedCell_Returns409()
    {
        var gameId = await CreatePveGameAsync();

        var first = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 2, y = 3 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var retry = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 2, y = 3 });
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
    }

    [Fact]
    public async Task PostShotOutOfBounds_Returns400()
    {
        var gameId = await CreatePveGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 10, y = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostPvpShotWithoutToken_Returns401()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var joinResponse = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions);
        Assert.NotNull(joined);

        await PlaceFleetAsync(created.Id, created.PlayerToken!);
        await PlaceFleetAsync(created.Id, joined.PlayerToken);

        var response = await _client.PostAsJsonAsync($"/api/games/{created.Id}/shots", new { x = 0, y = 0 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostShotAfterPlayerWins_Returns409()
    {
        var gameId = await SeedPvePlayerWinScenarioAsync();

        var winningShot = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 0, y = 0 });
        winningShot.EnsureSuccessStatusCode();

        var winResult = await winningShot.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(winResult);
        Assert.Equal(GameStatus.PlayerWon, winResult.Status);

        var afterWin = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 1, y = 1 });
        Assert.Equal(HttpStatusCode.Conflict, afterWin.StatusCode);
    }

    [Fact]
    public async Task PostShotAfterComputerWins_Returns409()
    {
        var gameId = await CreatePveGameAsync(boardSize: 5);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var game = await repository.GetByIdAsync(gameId);
        Assert.NotNull(game);
        game.Status = GameStatus.ComputerWon;
        game.ActiveParticipant = null;
        await repository.SaveAsync(game);

        var afterLoss = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 1, y = 1 });
        Assert.Equal(HttpStatusCode.Conflict, afterLoss.StatusCode);
    }

    [Fact]
    public async Task PostPvpShotAfterGameFinished_Returns409()
    {
        var (gameId, player1Token, _) = await CreateReadyPvpGameAsync();

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var game = await repository.GetByIdAsync(gameId);
        Assert.NotNull(game);
        game.Player2Board = CreateSingleCellBoard(game.BoardSize);
        game.Player1Board = new Board(game.BoardSize);
        await repository.SaveAsync(game);

        var winningRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/shots")
        {
            Content = JsonContent.Create(new { x = 0, y = 0 })
        };
        winningRequest.Headers.Add(PlayerTokenHeaders.HeaderName, player1Token);

        var winningShot = await _client.SendAsync(winningRequest);
        winningShot.EnsureSuccessStatusCode();

        var winResult = await winningShot.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(winResult);
        Assert.Equal(GameStatus.Player1Won, winResult.Status);

        var afterWin = await _client.PostAsJsonAsync($"/api/games/{gameId}/shots", new { x = 1, y = 1 });
        Assert.Equal(HttpStatusCode.Conflict, afterWin.StatusCode);
    }

    [Fact]
    public async Task PostPvpShotWithToken_Returns200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer" });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var joinResponse = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions);
        Assert.NotNull(joined);

        await PlaceFleetAsync(created.Id, created.PlayerToken!);
        await PlaceFleetAsync(created.Id, joined.PlayerToken);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{created.Id}/shots")
        {
            Content = JsonContent.Create(new { x = 1, y = 1 })
        };
        request.Headers.Add(PlayerTokenHeaders.HeaderName, created.PlayerToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(PlayerSide.Player1, result.Shooter);
        Assert.Equal(GameStatus.Player2Turn, result.Status);
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

    private async Task<Guid> SeedPvePlayerWinScenarioAsync()
    {
        var gameId = await CreatePveGameAsync(boardSize: 5);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var game = await repository.GetByIdAsync(gameId);
        Assert.NotNull(game);
        game.Player2Board = CreateSingleCellBoard(game.BoardSize);
        game.Player1Board = new Board(game.BoardSize);
        await repository.SaveAsync(game);

        return gameId;
    }

    private async Task<(Guid GameId, string Player1Token, string Player2Token)> CreateReadyPvpGameAsync()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/games", new { mode = "VsPlayer", boardSize = 5 });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<GameCreatedDto>(JsonOptions);
        Assert.NotNull(created);

        var joinResponse = await _client.PostAsync($"/api/games/{created.Id}/join", null);
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameDto>(JsonOptions);
        Assert.NotNull(joined);

        await PlaceFleetAsync(created.Id, created.PlayerToken!);
        await PlaceFleetAsync(created.Id, joined.PlayerToken);

        return (created.Id, created.PlayerToken!, joined.PlayerToken);
    }

    private async Task PlaceFleetAsync(Guid gameId, string playerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/fleet")
        {
            Content = JsonContent.Create(FleetTestData.ValidFleetJson)
        };
        request.Headers.Add(PlayerTokenHeaders.HeaderName, playerToken);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static Board CreateSingleCellBoard(int size)
    {
        var board = new Board(size);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 0, 0, horizontal: true);
        return board;
    }
}
