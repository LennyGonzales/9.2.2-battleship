using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record JoinGameDto(
    Guid Id,
    GameMode Mode,
    GameStatus Status,
    PlayerSide? CurrentTurn,
    int BoardSize,
    int ShotCount,
    DateTimeOffset CreatedAt,
    string PlayerToken);
