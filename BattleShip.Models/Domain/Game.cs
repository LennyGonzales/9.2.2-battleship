namespace BattleShip.Models.Domain;

public sealed class Game
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public GameMode Mode { get; init; } = GameMode.VsComputer;
    public GameStatus Status { get; set; } = GameStatus.PlayerTurn;
    public Participant? ActiveParticipant { get; set; } = Participant.Player1;
    public Board? Player1Board { get; set; }
    public Board? Player2Board { get; set; }
    public string? Player1Token { get; init; }
    public string? Player2Token { get; set; }
    public int BoardSize { get; init; } = GameOptions.DefaultBoardSize;
    public int ShotCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Difficulty Difficulty { get; init; } = Difficulty.Normal;

    public Board PlayerBoard => Player1Board!;
    public Board ComputerBoard => Player2Board!;
}
