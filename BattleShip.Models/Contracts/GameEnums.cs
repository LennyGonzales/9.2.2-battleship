namespace BattleShip.Models.Contracts;

public enum VisibleCellState
{
    Unknown,
    Empty,
    Ship,
    Miss,
    Hit,
    Sunk,
    Obstacle,
    ObstacleHit,
}

public enum BoardOwner
{
    Player,
    Opponent,
}
