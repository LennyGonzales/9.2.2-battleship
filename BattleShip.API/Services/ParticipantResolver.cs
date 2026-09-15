using BattleShip.Models.Domain;

namespace BattleShip.API.Services;

public sealed class ParticipantResolver
{
    public Participant? Resolve(Game game, string? token)
    {
        if (game.Mode == GameMode.VsComputer && string.IsNullOrWhiteSpace(token))
            return Participant.Player1;

        return game.TryResolveParticipant(token);
    }
}
