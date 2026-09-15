namespace BattleShip.Models.Contracts;

public enum GameStatus
{
    Waiting,
    PlayerTurn,
    ComputerTurn,
    PlayerWon,
    ComputerWon,
}

public enum Player
{
    Player,
    Computer,
}

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk,
}

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

public enum Difficulty
{
    Easy,
    Normal,
    Hard,
}
