using BattleShip.App;
using BattleShip.App.Services;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

var useMockApi = builder.Configuration.GetValue("UseMockApi", false);
if (useMockApi)
{
    builder.Services.AddSingleton<IGameApiClient, MockGameApiClient>();
}
else
{
    builder.Services.AddScoped<IGameApiClient, HttpGameApiClient>();

    builder.Services.AddSingleton(_ =>
    {
        var baseUrl = apiBaseUrl.TrimEnd('/');
        return GrpcChannel.ForAddress(baseUrl, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
    });
    builder.Services.AddScoped<IGameStatsClient, GrpcGameStatsClient>();
}

builder.Services.AddScoped<GameSession>();
await builder.Build().RunAsync();
