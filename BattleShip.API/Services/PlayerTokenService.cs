namespace BattleShip.API.Services;

public sealed class PlayerTokenService
{
    public string GenerateToken() => Guid.NewGuid().ToString("N");
}
