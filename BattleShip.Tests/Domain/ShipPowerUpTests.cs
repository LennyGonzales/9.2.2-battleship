using BattleShip.Models.Domain;

namespace BattleShip.Tests.Domain;

public class ShipPowerUpTests
{
    [Theory]
    [InlineData("Porte-avions", PowerUpType.Recon)]
    [InlineData("Croiseur", PowerUpType.DoubleStrike)]
    [InlineData("Contre-torpilleur", PowerUpType.Decoy)]
    [InlineData("Sous-marin", PowerUpType.TwinStrike)]
    [InlineData("Torpilleur", PowerUpType.Torpedo)]
    public void PowerUpType_MatchesShipName(string name, PowerUpType expected)
    {
        var ship = new Ship { Name = name, Length = 2 };

        Assert.Equal(expected, ship.PowerUpType);
    }

    [Fact]
    public void CanUsePowerUp_NewShip_IsTrue()
    {
        var ship = new Ship { Name = "Torpilleur", Length = 2 };

        Assert.True(ship.CanUsePowerUp);
    }

    [Fact]
    public void CanUsePowerUp_AfterMarkPowerUpUsed_IsFalse()
    {
        var ship = new Ship { Name = "Torpilleur", Length = 2 };

        ship.MarkPowerUpUsed();

        Assert.False(ship.CanUsePowerUp);
        Assert.True(ship.PowerUpUsed);
    }

    [Fact]
    public void CanUsePowerUp_OnSunkShip_IsFalse()
    {
        var board = new Board(10);
        var ship = new Ship { Name = "Torpilleur", Length = 1 };
        board.PlaceShip(ship, 0, 0, horizontal: true);

        board.ResolveShot(0, 0);

        Assert.True(ship.IsSunk);
        Assert.False(ship.CanUsePowerUp);
    }
}
