using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Domain;

/// <summary>
/// Single authority for all money arithmetic. Every amount is <see cref="decimal"/> and rounded to
/// two places exactly once, at the boundary. Nothing here touches a database or HTTP context.
/// </summary>
public static class PricingCalculator
{
    /// <summary>Subtotal of a set of priced lines.</summary>
    public static decimal Subtotal(IEnumerable<(decimal UnitPrice, int Quantity)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        decimal total = 0m;
        foreach (var (unitPrice, quantity) in lines)
        {
            total += LineTotal(unitPrice, quantity);
        }

        return Round(total);
    }

    /// <summary>One line's total, rounded to the currency scale.</summary>
    public static decimal LineTotal(decimal unitPrice, int quantity) =>
        Round(unitPrice * quantity);

    /// <summary>Resolves the shipping charge for a method (unknown methods fall back to Standard).</summary>
    public static decimal ShippingCost(string? method) =>
        ShippingOptions.CostFor(method);

    /// <summary>Order total. Shipping is never negative.</summary>
    public static decimal Total(decimal subtotal, decimal shippingCost) =>
        Round(subtotal + Math.Max(0m, shippingCost));

    /// <summary>Applies a variant price delta to a base price.</summary>
    public static decimal UnitPriceFor(decimal basePrice, decimal? variantPriceDelta) =>
        Round(basePrice + (variantPriceDelta ?? 0m));

    /// <summary>Currency-scale rounding (banker's-free, half-up) used consistently for money.</summary>
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Re-derives a line price from current catalogue data. Used at order time so a stale
    /// client-supplied price can never corrupt an order.
    /// </summary>
    public static decimal CurrentUnitPrice(Product product, ProductVariant? variant) =>
        UnitPriceFor(product.Price, variant?.PriceDelta);
}