namespace BattleShip.Models.Domain;

public sealed class Game
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public GameStatus Status { get; set; } = GameStatus.PlayerTurn;
    public PlayerSide CurrentTurn { get; set; } = PlayerSide.Player;
    public required Board PlayerBoard { get; init; }
    public required Board ComputerBoard { get; init; }
    public int ShotCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Difficulty Difficulty { get; init; } = Difficulty.Normal;
}
