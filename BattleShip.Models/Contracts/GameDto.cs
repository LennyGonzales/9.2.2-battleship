using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record GameDto(
    Guid Id,
    GameMode Mode,
    GameStatus Status,
    PlayerSide? CurrentTurn,
    int BoardSize,
    int ShotCount,
    DateTimeOffset CreatedAt);
