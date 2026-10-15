using Ecommerce.Api.Auth;
using Ecommerce.Api.Validation;
using Ecommerce.Api.Infrastructure;
using Ecommerce.Core.Dtos.Auth;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce.Api.Endpoints;

/// <summary>
/// POST/GET /api/auth/* — registration, login, refresh-token rotation, logout and current user.
/// </summary>
public static class AuthEndpoints
{
    public const string RefreshCookieName = "shophub_refresh";
    public const string AuthRateLimitPolicy = "auth";

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting(AuthRateLimitPolicy);

        group.MapPost("/register", RegisterAsync)
            .WithValidatedRequest<RegisterRequest>()
            .AllowAnonymous()
            .WithName("Register")
            .WithSummary("Register a new customer")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", LoginAsync)
            .WithValidatedRequest<LoginRequest>()
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Authenticate and receive a JWT access token")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Rotate the refresh token and issue a new access token")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Revoke the current refresh token")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", GetMe)
            .RequireAuthorization()
            .WithName("Me")
            .WithSummary("Current authenticated user")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthService auth,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        SetRefreshCookie(http, result);

        return Results.Created($"/api/auth/me", result.Response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService auth,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        SetRefreshCookie(http, result);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest? request,
        IAuthService auth,
        HttpContext http,
        CancellationToken ct)
    {
        // Prefer the HttpOnly cookie; fall back to an explicit body token (documented for Swagger).
        var token = http.Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            token = request?.RefreshToken;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidRefreshTokenException("No refresh token was supplied.");
        }

        var result = await auth.RefreshAsync(token, ct);
        SetRefreshCookie(http, result);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> LogoutAsync(
        IAuthService auth,
        HttpContext http,
        CancellationToken ct)
    {
        var token = http.Request.Cookies[RefreshCookieName];
        await auth.LogoutAsync(token, ct);
        http.Response.Cookies.Delete(RefreshCookieName);

        return Results.NoContent();
    }

    private static async Task<IResult> GetMe(
        ICurrentUser current,
        IAuthService auth,
        CancellationToken ct)
    {
        // RequireAuthorization guarantees a principal, but a valid token carrying no
        // resolvable customer id must still produce a 401 rather than a 500.
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var user = await auth.GetUserAsync(customerId, ct);
        return user is null ? Results.NotFound() : Results.Ok(user);
    }

    /// <summary>
    /// Issues the refresh token as an HttpOnly, SameSite cookie. The access token is returned in the
    /// body and kept in memory by the SPA, so it is never reachable from JavaScript or stored on disk.
    /// </summary>
    private static void SetRefreshCookie(HttpContext http, AuthResult result) =>
        http.Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = http.Request.IsHttps,
            IsEssential = true,
            Expires = result.RefreshTokenExpiresAt,
            Path = "/api/auth",
        });
}