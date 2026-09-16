using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.TestHelpers;

public static class WebApplicationFactoryExtensions
{
    /// <summary>
    /// Creates an HttpClient against a copy of the app with obstacle generation disabled, so tests
    /// that place a fleet at fixed coordinates (see <see cref="FleetTestData"/>) can't collide with
    /// a randomly-placed island.
    /// </summary>
    public static HttpClient CreateClientWithoutObstacles(this WebApplicationFactory<Program> factory) =>
        factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton(ObstacleGenerationOptions.None)))
            .CreateClient();
}
