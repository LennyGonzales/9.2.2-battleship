using BattleShip.API.Endpoints;
using BattleShip.API.Grpc;
using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Services;
using FluentValidation;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddGrpc();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:8081"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddScoped<IGameEngine, GameEngine>();
builder.Services.AddScoped<IComputerOpponent, DifficultyComputerOpponent>();
builder.Services.AddScoped<FleetPlacer>();
builder.Services.AddSingleton<PlayerTokenService>();
builder.Services.AddSingleton<ParticipantResolver>();
builder.Services.AddScoped<IValidator<CreateGameRequest>, CreateGameRequestValidator>();
builder.Services.AddScoped<IValidator<ShotRequest>, ShotRequestValidator>();
builder.Services.AddScoped<IValidator<PlaceFleetRequest>, PlaceFleetRequestValidator>();
builder.Services.AddScoped<IValidator<GameStatsQuery>, GameStatsQueryValidator>();
builder.Services.AddScoped<GameStatsCalculator>();
builder.Services.AddSingleton<Random>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseGrpcWeb();
app.MapGrpcService<GameStatsGrpcService>().EnableGrpcWeb();
app.MapGameEndpoints();
app.MapFleetEndpoints();
app.MapShotEndpoints();

app.Run();

public partial class Program;
