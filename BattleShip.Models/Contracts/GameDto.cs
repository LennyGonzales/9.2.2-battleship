namespace BattleShip.Models.Contracts;

public record CreateGameRequest(int? BoardSize, Difficulty? Difficulty);

public record GameDto(
    Guid Id,
    GameStatus Status,
    Player? CurrentTurn,
    int BoardSize,
    int ShotCount,
    DateTimeOffset CreatedAt);
