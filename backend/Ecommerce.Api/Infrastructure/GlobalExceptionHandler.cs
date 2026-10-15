using Ecommerce.Core.Domain;
using Ecommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Api.Infrastructure;

/// <summary>
/// Translates domain exceptions into RFC 7807 <see cref="ProblemDetails"/> responses with the
/// appropriate status code. This is the single place that decides the API's error contract.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("{Status} on {Method} {Path}: {Detail}",
                status, httpContext.Request.Method, httpContext.Request.Path, detail);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
            },
        });
    }

    /// <summary>Maps a domain exception onto (status, title, detail). Never leaks internals on 500.</summary>
    internal static (int Status, string Title, string Detail) Map(Exception exception) => exception switch
    {
        DuplicateEmailException => (
            StatusCodes.Status409Conflict, "Email already registered",
            exception.Message),

        InvalidCredentialsException => (
            StatusCodes.Status401Unauthorized, "Authentication failed",
            exception.Message),

        InvalidRefreshTokenException => (
            StatusCodes.Status401Unauthorized, "Invalid refresh token",
            exception.Message),

        PasswordPolicyViolationException => (
            StatusCodes.Status400BadRequest, "Password does not meet policy",
            exception.Message),

        ProductNotFoundException => (
            StatusCodes.Status404NotFound, "Product not found",
            exception.Message),

        CartItemNotFoundException => (
            StatusCodes.Status404NotFound, "Item not in cart",
            exception.Message),

        CategoryNotFoundException => (
            StatusCodes.Status404NotFound, "Category not found",
            exception.Message),

        InvalidCategoryHierarchyException => (
            StatusCodes.Status409Conflict, "Invalid category structure",
            exception.Message),

        InvalidOrderStatusException => (
            StatusCodes.Status400BadRequest, "Invalid order status",
            exception.Message),

        InvalidOrderStatusTransitionException => (
            StatusCodes.Status409Conflict, "Order status transition not allowed",
            exception.Message),

        InvalidShippingMethodException => (
            StatusCodes.Status400BadRequest, "Invalid shipping method",
            exception.Message),

        EmptyCartException => (
            StatusCodes.Status409Conflict, "Cart is empty",
            exception.Message),

        ProductUnavailableException => (
            StatusCodes.Status409Conflict, "Product unavailable",
            exception.Message),

        InsufficientStockException => (
            StatusCodes.Status409Conflict, "Insufficient stock",
            exception.Message),

        InvalidQuantityException => (
            StatusCodes.Status400BadRequest, "Invalid quantity",
            exception.Message),

        CartIdentityRequiredException => (
            StatusCodes.Status400BadRequest, "Cart identity required",
            exception.Message),

        KeyNotFoundException => (
            StatusCodes.Status404NotFound, "Not found",
            "The requested resource was not found."),

        _ => (
            StatusCodes.Status500InternalServerError, "Unexpected error",
            "An unexpected error occurred. Please try again later."),
    };
}

/// <summary>Raised when a submitted password fails <see cref="PasswordPolicy"/>.</summary>
public sealed class PasswordPolicyViolationException(string message) : Exception(message);