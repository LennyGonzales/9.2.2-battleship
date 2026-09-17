using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation.TestHelper;

namespace BattleShip.Tests.Validation;

public class UsePowerUpRequestValidatorTests
{
    private readonly UsePowerUpRequestValidator _validator = new();

    [Fact]
    public void UnknownShipName_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Fregate", null, null, null, null));
        result.ShouldHaveValidationErrorFor(x => x.ShipName);
    }

    [Fact]
    public void Recon_WithoutOrientation_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Porte-avions", null, 3, null, null));
        result.ShouldHaveValidationErrorFor(x => x.Orientation);
    }

    [Fact]
    public void Recon_WithOrientationAndIndex_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Porte-avions", Orientation.Row, 3, null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Torpedo_WithoutEntryEdge_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Torpilleur", Orientation.Column, 2, null, null));
        result.ShouldHaveValidationErrorFor(x => x.EntryEdge);
    }

    [Fact]
    public void Torpedo_Complete_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest("Torpilleur", Orientation.Column, 2, Edge.High, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TwinStrike_WithOneCell_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0)]));
        result.ShouldHaveValidationErrorFor(x => x.Cells);
    }

    [Fact]
    public void TwinStrike_WithTwoCells_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Sous-marin", null, null, null, [new CellTarget(0, 0), new CellTarget(0, 1)]));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Decoy_WithTwoCells_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Contre-torpilleur", null, null, null, [new CellTarget(0, 0), new CellTarget(0, 1)]));
        result.ShouldHaveValidationErrorFor(x => x.Cells);
    }

    [Fact]
    public void Decoy_WithOneCell_IsValid()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Contre-torpilleur", null, null, null, [new CellTarget(0, 0)]));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NegativeCellCoordinate_HasError()
    {
        var result = _validator.TestValidate(new UsePowerUpRequest(
            "Croiseur", null, null, null, [new CellTarget(-1, 0), new CellTarget(0, 1)]));
        result.ShouldHaveValidationErrorFor("Cells[0].X");
    }
}
