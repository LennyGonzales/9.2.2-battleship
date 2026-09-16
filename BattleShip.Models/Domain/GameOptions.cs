namespace BattleShip.Models.Domain;

public static class GameOptions
{
    public const int DefaultBoardSize = 10;
    public const int MinBoardSize = 5;
    public const int MaxBoardSize = 20;
    public const int MaxPlacementAttemptsPerShip = 100;
    public const int DefaultObstacleCount = 3;
    public const int MinObstacleSize = 1;
    public const int MaxObstacleSize = 2;
    public const int MaxObstaclePlacementAttempts = 50;

    public static readonly (string Name, int Length)[] DefaultFleet =
    [
        ("Porte-avions", 5),
        ("Croiseur", 4),
        ("Contre-torpilleur", 3),
        ("Sous-marin", 3),
        ("Torpilleur", 2)
    ];

    public static readonly IReadOnlyDictionary<string, PowerUpType> PowerUpByShipName = new Dictionary<string, PowerUpType>
    {
        ["Porte-avions"] = PowerUpType.Recon,
        ["Croiseur"] = PowerUpType.DoubleStrike,
        ["Contre-torpilleur"] = PowerUpType.Decoy,
        ["Sous-marin"] = PowerUpType.TwinStrike,
        ["Torpilleur"] = PowerUpType.Torpedo,
    };
}
