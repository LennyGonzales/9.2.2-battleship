namespace BattleShip.Models.Contracts;

public record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);

public record ValidationProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    IReadOnlyDictionary<string, string[]>? Errors);
