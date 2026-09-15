namespace BattleShip.Models.Exceptions;

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException() : base("Partie inconnue") { }
}

public sealed class GameConflictException : Exception
{
    public GameConflictException(string? message = null)
        : base(message ?? "Operation refusee") { }
}

public sealed class InvalidPlayerTokenException : Exception
{
    public InvalidPlayerTokenException()
        : base("Token joueur invalide ou absent.") { }
}
