namespace Ecommerce.Core.Entities;

/// <summary>Shipping methods and their prices, carried over from the legacy price rules.</summary>
public static class ShippingOptions
{
    public const string Standard = "Standard";
    public const string Express = "Express";

    /// <summary>Legacy rule preserved: Standard delivery ₹150.</summary>
    public const decimal StandardCost = 150m;

    /// <summary>Legacy rule preserved: Express delivery ₹350.</summary>
    public const decimal ExpressCost = 350m;

    public static readonly (string Code, string Label, decimal Cost, string Estimate)[] All =
    [
        (Standard, "Standard delivery", StandardCost, "5–7 days"),
        (Express, "Express delivery", ExpressCost, "1–2 days"),
    ];

    public static bool IsValid(string? method) =>
        method is Standard or Express;

    /// <summary>
    /// Resolves the cost for a method. Anything other than <see cref="Express"/> is treated as
    /// <see cref="Standard"/>, mirroring the legacy coercion rule.
    /// </summary>
    public static decimal CostFor(string? method) =>
        method == Express ? ExpressCost : StandardCost;
}