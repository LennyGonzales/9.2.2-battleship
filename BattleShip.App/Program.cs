using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using BattleShip.App;
using BattleShip.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

var useMockApi = builder.Configuration.GetValue("UseMockApi", true);
if (useMockApi)
{
    builder.Services.AddSingleton<IGameApiClient, MockGameApiClient>();
}
else
{
    builder.Services.AddScoped<IGameApiClient, HttpGameApiClient>();
}

await builder.Build().RunAsync();
