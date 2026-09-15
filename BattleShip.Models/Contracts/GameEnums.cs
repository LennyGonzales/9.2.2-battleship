namespace BattleShip.Models.Contracts;

public enum VisibleCellState
{
    Unknown,
    Empty,
    Ship,
    Miss,
    Hit,
    Sunk,
}

public enum BoardOwner
{
    Player,
    Opponent,
}
