using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestHelpers;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Grpc;

public class GameStatsGrpcServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public GameStatsGrpcServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetGameStats_AfterShots_ReturnsStats()
    {
        var gameId = await CreatePveGameWithShotsAsync();

        var client = CreateGrpcClient();
        var reply = await client.GetGameStatsAsync(new GameStatsQuery { GameId = gameId.ToString() });

        Assert.Equal(gameId.ToString(), reply.GameId);
        Assert.Equal(1, reply.PlayerShots);
        Assert.True(reply.ComputerShots >= 1);
        Assert.Equal(nameof(GameStatus.PlayerTurn), reply.Status);
    }

    [Fact]
    public async Task GetGameStats_UnknownGame_ThrowsNotFound()
    {
        var client = CreateGrpcClient();
        var gameId = Guid.NewGuid().ToString();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.GetGameStatsAsync(new GameStatsQuery { GameId = gameId }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetGameStats_InvalidGameId_ThrowsInvalidArgument()
    {
        var client = CreateGrpcClient();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.GetGameStatsAsync(new GameStatsQuery { GameId = "not-a-guid" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    private GameStats.GameStatsClient CreateGrpcClient()
    {
        var channel = GrpcChannel.ForAddress(
            _factory.Server.BaseAddress!,
            new GrpcChannelOptions
            {
                HttpClient = _httpClient
            });

        return new GameStats.GameStatsClient(channel);
    }

    private async Task<Guid> CreatePveGameWithShotsAsync()
    {
        var response = await _httpClient.PostAsJsonAsync("/api/games", new { });
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions);
        Assert.NotNull(dto);

        var fleetResponse = await _httpClient.PostAsJsonAsync(
            $"/api/games/{dto.Id}/fleet",
            FleetTestData.ValidFleetJson);
        fleetResponse.EnsureSuccessStatusCode();

        var shotResponse = await _httpClient.PostAsJsonAsync(
            $"/api/games/{dto.Id}/shots",
            new { x = 4, y = 7 });
        shotResponse.EnsureSuccessStatusCode();

        var computerResponse = await _httpClient.PostAsJsonAsync(
            $"/api/games/{dto.Id}/shots",
            new { });
        computerResponse.EnsureSuccessStatusCode();

        return dto.Id;
    }
}
