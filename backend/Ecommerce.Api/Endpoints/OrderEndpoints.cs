using Ecommerce.Api.Auth;
using Ecommerce.Api.Validation;
using Ecommerce.Core.Dtos.Orders;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Endpoints;

/// <summary>/api/orders/* — authenticated customers can only see and place their own orders.</summary>
public static class OrderEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPost("/", PlaceOrderAsync)
            .WithValidatedRequest<PlaceOrderRequest>()
            .WithName("PlaceOrder")
            .WithSummary("Place an order from the current cart")
            .WithDescription(
                "Creates the order, its items and the stock decrement inside a single transaction. "
                + "Send an Idempotency-Key header to make a repeated submission return the original order.")
            .Produces<OrderDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", GetOrdersAsync)
            .WithName("ListOrders")
            .WithSummary("Paged order history for the current customer")
            .Produces<Core.Common.PagedResult<OrderSummaryDto>>();

        group.MapGet("/{id:int:min(1)}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Order detail; 404 when the order does not exist or belongs to someone else")
            .Produces<OrderDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/shipping-options", GetShippingOptions)
            .WithName("GetShippingOptions")
            .WithSummary("Available shipping methods and prices")
            .Produces<IReadOnlyList<ShippingOptionDto>>();

        return group;
    }

    private static async Task<Results<Created<OrderDetailDto>, UnauthorizedHttpResult>> PlaceOrderAsync(
        PlaceOrderRequest request,
        ICurrentUser current,
        IOrderService orders,
        HttpContext http,
        CancellationToken ct)
    {
        var key = http.Request.Headers.TryGetValue(IdempotencyHeader, out var raw)
            ? raw.ToString()
            : null;

        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var order = await orders.PlaceOrderAsync(customerId, request, key, ct);

        return TypedResults.Created($"/api/orders/{order.Id}", order);
    }

    private static Task<Core.Common.PagedResult<OrderSummaryDto>> GetOrdersAsync(
        int? page, int? pageSize, ICurrentUser current, IOrderService orders, CancellationToken ct) =>
        orders.GetOrdersAsync(current.CustomerId ?? 0, page, pageSize, ct);

    private static async Task<Results<Ok<OrderDetailDto>, NotFound, UnauthorizedHttpResult>> GetOrderAsync(
        int id, ICurrentUser current, IOrderService orders, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var order = await orders.GetOrderAsync(customerId, id, ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    private static IReadOnlyList<ShippingOptionDto> GetShippingOptions(IOrderService orders) =>
        orders.GetShippingOptions();
}