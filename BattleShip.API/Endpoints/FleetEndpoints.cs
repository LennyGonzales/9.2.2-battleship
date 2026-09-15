using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class FleetEndpoints
{
    public static RouteGroupBuilder MapFleetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Fleet");

        group.MapPost("/{id:guid}/fleet", PlaceFleetAsync)
            .WithName("PlaceFleet")
            .Produces<GameDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> PlaceFleetAsync(
        Guid id,
        PlaceFleetRequest? request,
        IGameEngine engine,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        IValidator<PlaceFleetRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        request ??= new PlaceFleetRequest([]);

        var validationProblem = await validator.ValidateAsResultAsync(request, cancellationToken);
        if (validationProblem is not null)
            return validationProblem;

        var game = await repository.GetByIdAsync(id, cancellationToken);
        if (game is null)
        {
            return TypedResults.Problem(
                detail: "Partie inconnue",
                statusCode: StatusCodes.Status404NotFound);
        }

        var token = httpContext.Request.Headers[PlayerTokenHeaders.HeaderName].FirstOrDefault();
        var caller = participantResolver.Resolve(game, token);

        try
        {
            var updated = await engine.PlaceFleetAsync(id, caller, request, cancellationToken);
            return TypedResults.Ok(GameMapper.ToDto(updated));
        }
        catch (GameNotFoundException)
        {
            return TypedResults.Problem(
                detail: "Partie inconnue",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidPlayerTokenException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (FleetPlacementException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
