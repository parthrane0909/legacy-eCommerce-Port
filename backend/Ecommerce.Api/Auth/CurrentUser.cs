using System.Security.Claims;
using Ecommerce.Core.Interfaces.Services;

namespace Ecommerce.Api.Auth;

/// <summary>
/// Resolves the caller's identity for a request.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Customer id from the validated access token; null for anonymous callers.</summary>
    int? CustomerId { get; }

    string? Email { get; }

    string? Role { get; }

    /// <summary>
    /// Stable anonymous identifier for a guest cart, read from (and if necessary written to) an
    /// HttpOnly cookie. This replaces the legacy ASP.NET Session cart key entirely.
    /// </summary>
    string AnonymousCartId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor, ITokenService tokenService) : ICurrentUser
{
    public const string CartCookieName = "shophub_cart";

    private const string CartCacheKey = "__shophub_anonymous_cart__";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true && CustomerId is not null;

    public int? CustomerId
    {
        get
        {
            var principal = Principal;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return tokenService.GetCustomerId(principal);
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
                           ?? Principal?.FindFirstValue("email");

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role)
                           ?? Principal?.FindFirstValue("role");

    /// <summary>
    /// Stable anonymous identifier for the guest cart.
    /// </summary>
    /// <remarks>
    /// Read from the request cookie when present. Otherwise a new id is minted and written to the
    /// response cookie, then cached for the lifetime of the request: several endpoints touched by a
    /// single request would otherwise each append their own Set-Cookie header, and browsers keep only
    /// one value for a given cookie name.
    /// </remarks>
    public string AnonymousCartId
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null)
            {
                return string.Empty;
            }

            if (context.Items.TryGetValue(CartCacheKey, out var cached) && cached is string cachedId)
            {
                return cachedId;
            }

            if (context.Request.Cookies.TryGetValue(CartCookieName, out var existing)
                && Guid.TryParse(existing, out _))
            {
                context.Items[CartCacheKey] = existing;
                return existing;
            }

            var id = Guid.NewGuid().ToString("N");

            context.Response.Cookies.Append(CartCookieName, id, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            });

            context.Items[CartCacheKey] = id;
            return id;
        }
    }
}