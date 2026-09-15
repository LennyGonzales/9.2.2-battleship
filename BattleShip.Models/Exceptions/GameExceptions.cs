namespace BattleShip.Models.Exceptions;

public sealed class GameNotFoundException : Exception
{
    public GameNotFoundException() : base("Partie inconnue") { }
}

public sealed class GameConflictException : Exception
{
    public GameConflictException(string? message = null)
        : base(message ?? "Coup refuse") { }
}

public sealed class InvalidPlayerTokenException : Exception
{
    public InvalidPlayerTokenException() : base("Token joueur invalide") { }
}

public sealed class ShotOutOfBoundsException : Exception
{
    public ShotOutOfBoundsException(int x, int y)
        : base($"Coordonnees hors grille : ({x},{y}).") { }
}

public sealed class FleetPlacementException : Exception
{
    public FleetPlacementException(string message) : base(message) { }
}
