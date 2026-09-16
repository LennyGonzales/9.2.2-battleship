namespace BattleShip.Models.Domain;

public sealed record ObstacleGenerationOptions(int Count, int MinSize, int MaxSize)
{
    public static ObstacleGenerationOptions Default { get; } = new(
        GameOptions.DefaultObstacleCount, GameOptions.MinObstacleSize, GameOptions.MaxObstacleSize);

    public static ObstacleGenerationOptions None { get; } = new(0, 0, 0);
}
