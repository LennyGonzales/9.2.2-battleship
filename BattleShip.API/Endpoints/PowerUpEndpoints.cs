using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class PowerUpEndpoints
{
    public static RouteGroupBuilder MapPowerUpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("PowerUps");

        group.MapPost("/{id:guid}/powerups", UsePowerUpAsync)
            .WithName("UsePowerUp")
            .Produces<PowerUpResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/computer-turn", PlayComputerTurnAsync)
            .WithName("PlayComputerTurn")
            .Produces<ComputerTurnResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> UsePowerUpAsync(
        Guid id,
        UsePowerUpRequest? request,
        IGameEngine engine,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        IValidator<UsePowerUpRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shipName"] = ["Corps de requete requis."],
            });
        }

        var validationProblem = await validator.ValidateAsResultAsync(request, cancellationToken);
        if (validationProblem is not null)
            return validationProblem;

        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }

        var token = httpContext.Request.Headers[PlayerTokenHeaders.HeaderName].FirstOrDefault();
        var caller = participantResolver.Resolve(game, token);

        try
        {
            var result = await engine.UsePowerUpAsync(id, caller, request, cancellationToken);
            return TypedResults.Ok(PowerUpMapper.ToDto(game, result));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidPlayerTokenException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ShotOutOfBoundsException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> PlayComputerTurnAsync(
        Guid id,
        IGameEngine engine,
        IGameRepository repository,
        CancellationToken cancellationToken)
    {
        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var result = await engine.PlayComputerTurnAsync(id, cancellationToken);
            return TypedResults.Ok(PowerUpMapper.ToDto(game, result));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(detail: "Partie inconnue", statusCode: StatusCodes.Status404NotFound);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
