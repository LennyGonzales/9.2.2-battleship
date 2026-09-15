using BattleShip.API.Services;

namespace BattleShip.Tests.Services;

public class PlayerTokenServiceTests
{
    [Fact]
    public void GenerateToken_ReturnsNonEmptyUniqueValues()
    {
        var service = new PlayerTokenService();

        var first = service.GenerateToken();
        var second = service.GenerateToken();

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.NotEqual(first, second);
    }
}
