using FluentValidation;
using FluentValidation.Results;

namespace BattleShip.API.Validation;

public static class ValidationExtensions
{
    public static async Task<IResult?> ValidateAsResultAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        ValidationResult result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
            return null;

        return TypedResults.ValidationProblem(result.ToDictionary());
    }
}
