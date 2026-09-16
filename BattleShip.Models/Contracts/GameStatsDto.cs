namespace BattleShip.Models.Contracts;

public record GameStatsDto(
    Guid GameId,
    int PlayerShots,
    int ComputerShots,
    int PlayerHits,
    int ComputerHits,
    string Status);
