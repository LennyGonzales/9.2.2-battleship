using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record ShotRequest(int? X, int? Y);

public record ShotOutcomeDto(int X, int Y, ShotOutcome Outcome, string? SunkShipName);

public record ShotResultDto(ShotOutcomeDto Shot, PlayerSide Shooter, GameStatus Status);
