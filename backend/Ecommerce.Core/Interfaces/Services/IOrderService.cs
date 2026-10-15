using Ecommerce.Core.Common;
using Ecommerce.Core.Dtos.Orders;

namespace Ecommerce.Core.Interfaces.Services;

public interface IOrderService
{
    /// <summary>
    /// Places an order from the customer's cart. Pass <paramref name="idempotencyKey"/> to make
    /// repeated submissions of the same checkout collapse onto a single order.
    /// </summary>
    Task<OrderDetailDto> PlaceOrderAsync(int customerId, PlaceOrderRequest request, string? idempotencyKey = null, CancellationToken ct = default);

    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(int customerId, int? page, int? pageSize, CancellationToken ct = default);

    /// <summary>Returns the order only when it belongs to <paramref name="customerId"/>; otherwise null.</summary>
    Task<OrderDetailDto?> GetOrderAsync(int customerId, int orderId, CancellationToken ct = default);

    IReadOnlyList<ShippingOptionDto> GetShippingOptions();
}