using BattleShip.API.Services;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Services;

public class ParticipantResolverTests
{
    private readonly ParticipantResolver _resolver = new();

    [Fact]
    public void Resolve_VsComputerWithoutToken_ReturnsPlayer1()
    {
        var game = new Game
        {
            Mode = GameMode.VsComputer,
            Player1Board = new Board(10),
            Player2Board = new Board(10),
            BoardSize = 10
        };

        var participant = _resolver.Resolve(game, null);

        Assert.Equal(Participant.Player1, participant);
    }

    [Fact]
    public void Resolve_VsPlayerWithValidToken_ReturnsParticipant()
    {
        var game = new Game
        {
            Mode = GameMode.VsPlayer,
            BoardSize = 10,
            Player1Token = "token-p1",
            Player2Token = "token-p2"
        };

        Assert.Equal(Participant.Player1, _resolver.Resolve(game, "token-p1"));
        Assert.Equal(Participant.Player2, _resolver.Resolve(game, "token-p2"));
    }

    [Fact]
    public void Resolve_VsPlayerWithoutToken_ReturnsNull()
    {
        var game = new Game
        {
            Mode = GameMode.VsPlayer,
            BoardSize = 10,
            Player1Token = "token-p1"
        };

        Assert.Null(_resolver.Resolve(game, null));
    }
}
