using System.Text.Json.Serialization;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

public record CreateGameRequest(
    [property: JsonPropertyName("boardSize")] int? BoardSize,
    [property: JsonPropertyName("difficulty")] Difficulty? Difficulty,
    [property: JsonPropertyName("mode")] GameMode? Mode);
