using BattleShip.API.Services;
using BattleShip.API.Validation;
using BattleShip.Models.Contracts;
using BattleShip.Models.Exceptions;
using BattleShip.Models.Services;
using FluentValidation;

namespace BattleShip.API.Endpoints;

public static class ShotEndpoints
{
    public static RouteGroupBuilder MapShotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Shots");

        group.MapPost("/{id:guid}/shots", FireShotAsync)
            .WithName("FireShot")
            .Produces<ShotResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> FireShotAsync(
        Guid id,
        ShotRequest? request,
        IGameEngine engine,
        IGameRepository repository,
        ParticipantResolver participantResolver,
        IValidator<ShotRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        request ??= new ShotRequest(null, null);

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
            var result = await engine.FireShotAsync(id, caller, request.X, request.Y, cancellationToken);
            return TypedResults.Ok(ShotMapper.ToDto(game, result));
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
        catch (GameConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ShotOutOfBoundsException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
