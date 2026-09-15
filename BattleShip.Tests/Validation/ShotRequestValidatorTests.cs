using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using FluentValidation.TestHelper;

namespace BattleShip.Tests.Validation;

public class ShotRequestValidatorTests
{
    private readonly ShotRequestValidator _validator = new();

    [Fact]
    public void NegativeX_HasError()
    {
        var result = _validator.TestValidate(new ShotRequest(-1, 0));
        result.ShouldHaveValidationErrorFor(x => x.X);
    }

    [Fact]
    public void NegativeY_HasError()
    {
        var result = _validator.TestValidate(new ShotRequest(0, -1));
        result.ShouldHaveValidationErrorFor(x => x.Y);
    }

    [Fact]
    public void ValidCoordinates_IsValid()
    {
        var result = _validator.TestValidate(new ShotRequest(4, 7));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyRequest_IsValid()
    {
        var result = _validator.TestValidate(new ShotRequest(null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
