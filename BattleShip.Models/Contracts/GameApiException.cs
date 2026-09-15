namespace BattleShip.Models.Contracts;

public sealed class GameApiException(ProblemDetailsDto problem, int statusCode)
    : Exception(problem.Detail ?? problem.Title ?? $"API error ({statusCode})")
{
    public ProblemDetailsDto Problem { get; } = problem;
    public int StatusCode { get; } = statusCode;
}
