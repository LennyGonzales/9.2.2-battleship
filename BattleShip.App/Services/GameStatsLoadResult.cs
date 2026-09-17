using BattleShip.Models.Contracts;

namespace BattleShip.App.Services;

public sealed record GameStatsLoadResult(
    GameStatsDto? Stats,
    string? StatusCode,
    string? Detail)
{
    public bool IsSuccess => Stats is not null && StatusCode == "OK";
}
