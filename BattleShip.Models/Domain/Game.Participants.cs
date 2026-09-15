namespace BattleShip.Models.Domain;

public static class GameParticipants
{
    public static Board GetBoard(this Game game, Participant participant) =>
        participant switch
        {
            Participant.Player1 => game.Player1Board
                ?? throw new InvalidOperationException("La grille du joueur 1 n'est pas initialisee."),
            Participant.Player2 => game.Player2Board
                ?? throw new InvalidOperationException("La grille du joueur 2 n'est pas initialisee."),
            _ => throw new ArgumentOutOfRangeException(nameof(participant))
        };

    public static Board GetOpponentBoard(this Game game, Participant participant) =>
        GetBoard(game, participant == Participant.Player1 ? Participant.Player2 : Participant.Player1);

    public static Participant? TryResolveParticipant(this Game game, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (token == game.Player1Token)
            return Participant.Player1;

        if (token == game.Player2Token)
            return Participant.Player2;

        return null;
    }

    public static PlayerSide? MapToPlayerSide(this Game game, Participant? participant)
    {
        if (participant is null)
            return null;

        return game.Mode switch
        {
            GameMode.VsComputer => participant switch
            {
                Participant.Player1 => PlayerSide.Player,
                Participant.Player2 => PlayerSide.Computer,
                _ => null
            },
            GameMode.VsPlayer => participant switch
            {
                Participant.Player1 => PlayerSide.Player1,
                Participant.Player2 => PlayerSide.Player2,
                _ => null
            },
            _ => null
        };
    }

    public static bool IsFinished(this Game game) =>
        game.Status is GameStatus.PlayerWon
            or GameStatus.ComputerWon
            or GameStatus.Player1Won
            or GameStatus.Player2Won;
}
