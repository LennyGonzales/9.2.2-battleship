using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record GameDto(
    Guid Id,
    GameStatus Status,
    PlayerSide? CurrentTurn,
    int BoardSize,
    int ShotCount,
    DateTimeOffset CreatedAt);
