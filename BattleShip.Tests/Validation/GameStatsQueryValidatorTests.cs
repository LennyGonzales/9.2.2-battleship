using BattleShip.API.Validation;
using BattleShip.Grpc;
using FluentValidation.TestHelper;

namespace BattleShip.Tests.Validation;

public class GameStatsQueryValidatorTests
{
    private readonly GameStatsQueryValidator _validator = new();

    [Fact]
    public void EmptyGameId_HasError()
    {
        var result = _validator.TestValidate(new GameStatsQuery { GameId = "" });
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void InvalidGameId_HasError()
    {
        var result = _validator.TestValidate(new GameStatsQuery { GameId = "not-a-guid" });
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void ValidGameId_IsValid()
    {
        var result = _validator.TestValidate(new GameStatsQuery
        {
            GameId = Guid.NewGuid().ToString()
        });
        result.ShouldNotHaveAnyValidationErrors();
    }
}
