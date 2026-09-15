using BattleShip.Models.Domain;
using BattleShip.Models.Services;

namespace BattleShip.API.Services;

public sealed class RandomComputerOpponent : IComputerOpponent
{
    public (int X, int Y) ChooseShot(Game game) =>
        throw new NotImplementedException("La logique de tir ordinateur sera implementee avec POST /shots.");
}
