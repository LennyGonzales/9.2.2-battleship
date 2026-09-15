using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BattleShip.API.Endpoints;

public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Games");

        group.MapPost("/", CreateGameAsync)
            .WithName("CreateGame")
            .Produces<GameDto>(StatusCodes.Status201Created)
            .Produces<GameCreatedDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetGameAsync)
            .WithName("GetGame")
            .Produces<GameDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/join", JoinGameAsync)
            .WithName("JoinGame")
            .Produces<JoinGameDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/board/player", GetPlayerBoardAsync)
            .WithName("GetPlayerBoard")
            .WithTags("Boards")
            .Produces<BoardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/board/opponent", GetOpponentBoardAsync)
            .WithName("GetOpponentBoard")
            .WithTags("Boards")
            .Produces<BoardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> CreateGameAsync(
        CreateGameRequest? request,
        IGameEngine engine,
        IValidator<CreateGameRequest> validator,
        CancellationToken cancellationToken)
    {
        request ??= new CreateGameRequest(null, null);

        var validationProblem = await validator.ValidateAsResultAsync(request, cancellationToken);
        if (validationProblem is not null)
            return validationProblem;

        try
        {
            var game = await engine.CreateGameAsync(request, cancellationToken);

            if (game.Mode == GameMode.VsPlayer)
            {
                var createdDto = GameMapper.ToCreatedDto(game);
                return TypedResults.Created($"/api/games/{createdDto.Id}", createdDto);
            }

            var dto = GameMapper.ToDto(game);
            return TypedResults.Created($"/api/games/{dto.Id}", dto);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetGameAsync(
        Guid id,
        IGameRepository repository,
        CancellationToken cancellationToken)
    {
        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(
                detail: "Partie inconnue",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(GameMapper.ToDto(game));
    }

    private static async Task<IResult> JoinGameAsync(
        Guid id,
        IGameEngine engine,
        CancellationToken cancellationToken)
    {
        try
        {
            var game = await engine.JoinGameAsync(id, cancellationToken);
            return TypedResults.Ok(GameMapper.ToJoinDto(game));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(
                detail: "Partie inconnue",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetPlayerBoardAsync(
        Guid id,
        [FromHeader(Name = PlayerTokenHeaders.HeaderName)] string? playerToken,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        CancellationToken cancellationToken)
    {
        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
            return GameNotFoundProblem();

        if (game.Player1Board is null || game.Player2Board is null)
            return GameNotStartedProblem();

        var participant = participantResolver.Resolve(game, playerToken);
        if (participant is null)
            return GameNotFoundProblem();

        var board = game.GetBoard(participant.Value);
        return TypedResults.Ok(GameMapper.ToBoardDto(board, BoardOwner.Player));
    }

    private static async Task<IResult> GetOpponentBoardAsync(
        Guid id,
        [FromHeader(Name = PlayerTokenHeaders.HeaderName)] string? playerToken,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        CancellationToken cancellationToken)
    {
        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
            return GameNotFoundProblem();

        if (game.Player1Board is null || game.Player2Board is null)
            return GameNotStartedProblem();

        var participant = participantResolver.Resolve(game, playerToken);
        if (participant is null)
            return GameNotFoundProblem();

        var board = game.GetOpponentBoard(participant.Value);
        return TypedResults.Ok(GameMapper.ToBoardDto(board, BoardOwner.Opponent));
    }

    private static IResult GameNotFoundProblem() =>
        TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);

    private static IResult GameNotStartedProblem() =>
        TypedResults.Problem(
            detail: "La partie n'a pas encore commence.",
            statusCode: StatusCodes.Status409Conflict);
}
