using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation.TestHelper;

namespace BattleShip.Tests.Validation;

public class CreateGameRequestValidatorTests
{
    private readonly CreateGameRequestValidator _validator = new();

    [Fact]
    public void EmptyRequest_IsValid()
    {
        var result = _validator.TestValidate(new CreateGameRequest(null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(21)]
    public void BoardSize_OutOfRange_HasError(int boardSize)
    {
        var result = _validator.TestValidate(new CreateGameRequest(boardSize, null));
        result.ShouldHaveValidationErrorFor(x => x.BoardSize);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void BoardSize_InRange_IsValid(int boardSize)
    {
        var result = _validator.TestValidate(new CreateGameRequest(boardSize, Difficulty.Normal));
        result.ShouldNotHaveValidationErrorFor(x => x.BoardSize);
    }

    [Fact]
    public void InvalidDifficulty_HasError()
    {
        var request = new CreateGameRequest(10, (Difficulty)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Difficulty);
    }
}
