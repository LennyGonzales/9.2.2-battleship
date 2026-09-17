namespace BattleShip.Models.Contracts;

public record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
