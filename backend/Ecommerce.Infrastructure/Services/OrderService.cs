using Ecommerce.Core.Common;
using Ecommerce.Core.Domain;
using Ecommerce.Core.Dtos.Orders;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Order placement and retrieval.
/// </summary>
/// <remarks>
/// Correctness properties (all defects called out in the audit are addressed here):
/// <list type="bullet">
/// <item>Order header, order items, stock decrements and cart finalisation run inside one
/// <see cref="System.Data.Common.DbTransaction"/>; any failure rolls everything back.</item>
/// <item>Prices, totals and stock are re-derived from the database. Nothing is trusted from the request.</item>
/// <item>Stock is actually decremented (the legacy application never did this).</item>
/// <item>An idempotency key collapses repeated submissions of the same checkout.</item>
/// <item>A row-level concurrency token on Product.Stock prevents lost updates under concurrency.</item>
/// </list>
/// </remarks>
public sealed class OrderService(EcommerceDbContext db) : IOrderService
{
    public async Task<OrderDetailDto> PlaceOrderAsync(
        int customerId, PlaceOrderRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (!ShippingOptions.IsValid(request.ShippingMethod))
        {
            throw new InvalidShippingMethodException(request.ShippingMethod);
        }

        // Idempotency: a replayed submission returns the original order rather than creating a second one.
        // Idempotency is checked before the cart is inspected, so a replay of an already-completed
        // checkout succeeds even though its cart is now empty.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var key = idempotencyKey.Trim();
            var existing = await db.Orders.AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == key, ct);

            if (existing is not null)
            {
                return ToDetail(existing);
            }
        }

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new EmptyCartException();
        }

        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Validate every line against live catalogue state before opening the transaction.
        var lines = new List<(CartItem Line, Product Product, ProductVariant? Variant, decimal UnitPrice)>();
        foreach (var item in cart.Items)
        {
            var product = products.GetValueOrDefault(item.ProductId);

            if (product is null || !product.IsActive)
            {
                throw new ProductUnavailableException(item.ProductName);
            }

            var variant = item.ProductVariantId is { } vid
                ? product.Variants.FirstOrDefault(v => v.Id == vid)
                : null;

            if (item.Quantity < 1)
            {
                throw new InvalidQuantityException(item.ProductId);
            }

            var stock = variant is not null ? variant.Stock : product.Stock;
            if (stock < item.Quantity)
            {
                throw new InsufficientStockException(item.ProductName, stock, item.Quantity);
            }

            // Authoritative price from the catalogue — never the stale cart snapshot.
            lines.Add((item, product, variant, PricingCalculator.CurrentUnitPrice(product, variant)));
        }

        var shippingCost = PricingCalculator.ShippingCost(request.ShippingMethod);
        var subtotal = PricingCalculator.Subtotal(lines.Select(l => (l.UnitPrice, l.Line.Quantity)));
        var total = PricingCalculator.Total(subtotal, shippingCost);

// The SQL Server retry-on-failure execution strategy does not support user-initiated
// transactions, so the entire placement runs inside the strategy as one retriable unit.
// Without this, order placement returns 500 on SQL Server even though it passes on SQLite.
// Billing is either the shipping address or a separately supplied one.
        var billing = request.BillingSameAsShipping ? request.ShippingAddress : request.BillingAddress;

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            try
            {
                foreach (var (item, product, variant, _) in lines)
                {
                    var qty = item.Quantity;
                    int affected;

                    if (variant is not null)
                    {
                        affected = await db.ProductVariants
                            .Where(v => v.Id == variant.Id && v.Stock >= qty)
                            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - qty), ct);
                    }
                    else
                    {
                        affected = await db.Products
                            .Where(p => p.Id == product.Id && p.Stock >= qty)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty), ct);
                    }

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(product.Name, 0, qty);
                    }
                }

                var order = new Order
                {
                    OrderNumber = OrderNumberGenerator.Generate(DateTime.UtcNow),
                    IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
                    CustomerId = customerId,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    Subtotal = subtotal,
                    ShippingCost = shippingCost,
                    Total = total,
                    ShippingMethod = request.ShippingMethod,
                    PaymentMethod = "Card",

                    ShipFirstName = request.ShippingAddress.FirstName.Trim(),
                    ShipLastName = request.ShippingAddress.LastName.Trim(),
                    ShipLine1 = request.ShippingAddress.Line1.Trim(),
                    ShipLine2 = request.ShippingAddress.Line2?.Trim(),
                    ShipCity = request.ShippingAddress.City.Trim(),
                    ShipState = request.ShippingAddress.State.Trim(),
                    ShipPostalCode = request.ShippingAddress.PostalCode.Trim(),
                    ShipCountry = request.ShippingAddress.Country.Trim(),
                    ShipPhone = request.ShippingAddress.Phone?.Trim(),

                    BillFirstName = billing.FirstName.Trim(),
                    BillLastName = billing.LastName.Trim(),
                    BillLine1 = billing.Line1.Trim(),
                    BillLine2 = billing.Line2?.Trim(),
                    BillCity = billing.City.Trim(),
                    BillState = billing.State.Trim(),
                    BillPostalCode = billing.PostalCode.Trim(),
                    BillCountry = billing.Country.Trim(),
                };

                foreach (var (item, product, variant, unitPrice) in lines)
                {
                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        VariantName = variant?.Name,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                    });
                }

                db.Orders.Add(order);

                db.CartItems.RemoveRange(cart.Items);
                cart.Items.Clear();
                cart.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return ToDetail(order);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(
        int customerId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var p = Paging.NormalizePage(page);
        var size = Paging.NormalizePageSize(pageSize);

        var q = db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId);

        var total = await q.CountAsync(ct);

        var orders = await q
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.OrderDate, o.Status, o.Subtotal, o.ShippingCost, o.Total,
                o.ShippingMethod, o.Items.Sum(i => i.Quantity)))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>
        {
            Items = orders, Page = p, PageSize = size, TotalCount = total,
        };
    }

    public async Task<OrderDetailDto?> GetOrderAsync(int customerId, int orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, ct);

        return order is null ? null : ToDetail(order);
    }

    public IReadOnlyList<ShippingOptionDto> GetShippingOptions() =>
        ShippingOptions.All
            .Select(o => new ShippingOptionDto(o.Code, $"{o.Label} — ₹{o.Cost:0.##} · {o.Estimate}", o.Cost, o.Estimate))
            .ToList();

    private static OrderDetailDto ToDetail(Order o) => new(
        o.Id, o.OrderNumber, o.OrderDate, o.Status, o.Subtotal, o.ShippingCost, o.Total,
        o.ShippingMethod, o.PaymentMethod,
        new AddressSnapshotDto(
            $"{o.ShipFirstName} {o.ShipLastName}".Trim(), o.ShipLine1, o.ShipLine2, o.ShipCity,
            o.ShipState, o.ShipPostalCode, o.ShipCountry, o.ShipPhone),
        new AddressSnapshotDto(
            $"{o.BillFirstName} {o.BillLastName}".Trim(), o.BillLine1, o.BillLine2, o.BillCity,
            o.BillState, o.BillPostalCode, o.BillCountry, null),
        o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto(
                i.Id, i.ProductId, i.ProductName, i.VariantName, i.Quantity, i.UnitPrice,
                PricingCalculator.LineTotal(i.UnitPrice, i.Quantity)))
            .ToList());
}

public sealed class EmptyCartException() : Exception("Your cart is empty.");

public sealed class InvalidShippingMethodException(string method)
    : Exception($"'{method}' is not a valid shipping method.");

public sealed class ProductUnavailableException(string name)
    : Exception($"\"{name}\" is no longer available. Please remove it from your cart.");

public sealed class InsufficientStockException(string name, int available, int requested)
    : Exception($"Only {available} unit(s) of \"{name}\" remain in stock, but {requested} were requested.");

public sealed class InvalidQuantityException(int productId)
    : Exception($"Quantity for product {productId} must be at least 1.");