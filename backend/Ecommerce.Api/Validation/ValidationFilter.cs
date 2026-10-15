using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Validation;

/// <summary>
/// Minimal API endpoint filter that runs the matching FluentValidation validator and turns failures
/// into a 400 <c>ProblemDetails</c> response.
/// </summary>
/// <remarks>
/// Minimal APIs apply neither data annotations nor FluentValidation automatically. Registering the
/// validators with DI alone is not enough - this filter is what actually enforces them, which is
/// why weak passwords and malformed payloads would otherwise reach the service layer.
/// </remarks>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validators = context.HttpContext.RequestServices.GetServices<IValidator<TRequest>>();

        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            if (context.Arguments[0] is not TRequest request)
            {
                continue;
            }

            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(context);
        }

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

        return Results.ValidationProblem(
            errors,
            title: "One or more validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest,
            instance: context.HttpContext.Request.Path);
    }
}

/// <summary>Applies <see cref="ValidationFilter{TRequest}"/> to a single endpoint.</summary>
public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidatedRequest<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>();
}