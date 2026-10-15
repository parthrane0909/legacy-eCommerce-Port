using Ecommerce.Api.Auth;
using Ecommerce.Api.Validation;
using Ecommerce.Core.Dtos.Cart;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Endpoints;

/// <summary>
/// /api/cart/* — works for both anonymous and authenticated callers.
/// </summary>
/// <remarks>
/// Identity resolution: an authenticated caller always operates on the cart owned by their customer
/// id; otherwise the anonymous cart cookie is used. There is no ASP.NET Session dependency.
/// </remarks>
public static class CartEndpoints
{
    public static RouteGroupBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        // The group is anonymous because guests must be able to shop. The merge endpoint,
        // which requires an authenticated customer, opts in explicitly below.
        var group = app.MapGroup("/api/cart").WithTags("Cart").AllowAnonymous();

        group.MapGet("/", GetCartAsync)
            .WithName("GetCart")
            .WithSummary("Current cart with totals")
            .Produces<CartDto>();

        group.MapPost("/items", AddItemAsync)
            .WithValidatedRequest<AddCartItemRequest>()
            .WithName("AddCartItem")
            .WithSummary("Add a product to the cart")
            .Produces<CartDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/items/{productId:int:min(1)}", UpdateItemAsync)
            .WithValidatedRequest<UpdateCartItemRequest>()
            .WithName("UpdateCartItem")
            .WithSummary("Set the quantity for a cart line")
            .Produces<CartDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/items/{productId:int:min(1)}", RemoveItemAsync)
            .WithName("RemoveCartItem")
            .WithSummary("Remove a line from the cart")
            .Produces<CartDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/", ClearCartAsync)
            .WithName("ClearCart")
            .WithSummary("Empty the cart")
            .Produces<CartDto>();

        group.MapPost("/merge", MergeAsync)
            .RequireAuthorization()
            .WithName("MergeCart")
            .WithSummary("Merge the guest cart into the authenticated customer's cart")
            .Produces<CartDto>()
            .Produces<CartDto>(StatusCodes.Status200OK);

        return group;
    }

    private static Task<CartDto> GetCartAsync(
        ICurrentUser current, ICartService cart, CancellationToken ct) =>
        cart.GetCartAsync(current.CustomerId, Anonymous(current), ct);

    private static Task<CartDto> AddItemAsync(
        AddCartItemRequest request, ICurrentUser current, ICartService cart, CancellationToken ct) =>
        cart.AddItemAsync(current.CustomerId, Anonymous(current), request, ct);

    private static Task<CartDto> UpdateItemAsync(
        int productId, UpdateCartItemRequest request,
        ICurrentUser current, ICartService cart, CancellationToken ct) =>
        cart.UpdateItemAsync(current.CustomerId, Anonymous(current), productId, request, ct);

    private static Task<CartDto> RemoveItemAsync(
        int productId, ICurrentUser current, ICartService cart, CancellationToken ct) =>
        cart.RemoveItemAsync(current.CustomerId, Anonymous(current), productId, ct);

    private static Task<CartDto> ClearCartAsync(
        ICurrentUser current, ICartService cart, CancellationToken ct) =>
        cart.ClearAsync(current.CustomerId, Anonymous(current), ct);

    /// <summary>
    /// Performs the merge. A merge that had to drop or clamp items returns 200 with a
    /// <c>warnings</c> array rather than failing, so the guest still keeps a usable cart.
    /// </summary>
    private static async Task<IResult> MergeAsync(
        MergeCartRequest? request,
        ICurrentUser current,
        ICartService cart,
        CancellationToken ct)
    {
        // RequireAuthorization guards this endpoint, but a valid token carrying no resolvable
        // customer id must still produce a 401 rather than a 500.
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        // Prefer the guest cart id the API issued in its own cookie; fall back to one supplied by
        // the caller so a client holding the id can drive the merge explicitly.
        var anonymousId = current.AnonymousCartId;

        if (!string.IsNullOrWhiteSpace(request?.AnonymousId))
        {
            anonymousId = request.AnonymousId;
        }

        try
        {
            var merged = await cart.MergeAsync(customerId, anonymousId, ct);
            return Results.Ok(merged);
        }
        catch (CartMergeConflictException ex)
        {
            return Results.Ok(new CartDto(
                ex.Cart.Id, ex.Cart.IsGuest, ex.Cart.ItemCount, ex.Cart.Subtotal, ex.Cart.Items)
            {
                Warnings = ex.Warnings,
            });
        }
    }

    /// <summary>The anonymous cart id is only relevant to anonymous callers.</summary>
    private static string? Anonymous(ICurrentUser current) =>
        current.IsAuthenticated ? null : current.AnonymousCartId;
}