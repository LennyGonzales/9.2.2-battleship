using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Services;

public interface IComputerOpponent
{
    (int X, int Y) ChooseShot(Game game);
    UsePowerUpRequest? ChoosePowerUp(Game game);
}
