namespace BattleShip.Models.Domain;

public static class GameOptions
{
    public const int DefaultBoardSize = 10;
    public const int MinBoardSize = 5;
    public const int MaxBoardSize = 20;
    public const int MaxPlacementAttemptsPerShip = 100;

    public static readonly (string Name, int Length)[] DefaultFleet =
    [
        ("Porte-avions", 5),
        ("Croiseur", 4),
        ("Contre-torpilleur", 3),
        ("Sous-marin", 3),
        ("Torpilleur", 2)
    ];
}
