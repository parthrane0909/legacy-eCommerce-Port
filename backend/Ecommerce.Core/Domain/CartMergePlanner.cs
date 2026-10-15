namespace Ecommerce.Core.Domain;

/// <summary>A product/variant pair considered during a cart merge, with its current catalogue state.</summary>
public record MergeCandidate(
    int ProductId,
    int? ProductVariantId,
    int RequestedQuantity,
    bool ProductExists,
    bool IsActive,
    int AvailableStock,
    decimal CurrentUnitPrice,
    string ProductName);

/// <summary>A line in the merged cart.</summary>
public record MergedCartLine(
    int ProductId,
    int? ProductVariantId,
    int Quantity,
    decimal UnitPrice,
    string ProductName);

/// <summary>Result of a merge, including human-readable notes for anything dropped or clamped.</summary>
public record MergeOutcome(
    IReadOnlyList<MergedCartLine> Lines,
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

/// <summary>
/// Pure guest-to-user cart merge.
///
/// Handles the cases the assignment calls out: duplicate products, invalid quantities, deleted
/// products and stock restrictions. Everything is re-priced from the current catalogue, so a stale
/// guest cart can never carry an old price into the authenticated cart.
/// </summary>
public static class CartMergePlanner
{
    /// <summary>Upper bound on a single line, independent of stock.</summary>
    public const int MaxQuantityPerLine = 99;

    // Warning fragments, centralised so tests and callers match on stable text.
    public const string ProductUnavailableWarning = "is no longer available";
    public const string InvalidQuantityWarning = "An invalid quantity for";
    public const string OutOfStockWarning = "is out of stock";
    public const string StockLimitedWarning = "remain in stock";
    public const string LineCapWarning = "can be ordered";

    public static MergeOutcome Merge(
        IEnumerable<MergedCartLine> existingLines,
        IEnumerable<MergeCandidate> guestCandidates)
    {
        ArgumentNullException.ThrowIfNull(existingLines);
        ArgumentNullException.ThrowIfNull(guestCandidates);

        var warnings = new List<string>();
        var merged = new Dictionary<(int ProductId, int? VariantId), MergedCartLine>();

        // 1. Carry the customer's existing lines across, clamped to current stock.
        foreach (var line in existingLines)
        {
            var key = (line.ProductId, line.ProductVariantId);
            if (merged.TryGetValue(key, out var already))
            {
                merged[key] = already with { Quantity = Math.Min(already.Quantity + line.Quantity, MaxQuantityPerLine) };
            }
            else
            {
                merged[key] = line with { Quantity = Math.Clamp(line.Quantity, 0, MaxQuantityPerLine) };
            }
        }

        // 2. Fold guest lines in, re-pricing from the catalogue.
        foreach (var candidate in guestCandidates)
        {
            if (!candidate.ProductExists)
            {
                warnings.Add($"\"{candidate.ProductName}\" is no longer available and was removed from your cart.");
                continue;
            }

            if (!candidate.IsActive)
            {
                warnings.Add($"\"{candidate.ProductName}\" is no longer available and was removed from your cart.");
                continue;
            }

            var requested = candidate.RequestedQuantity;
            if (requested < 1)
            {
                warnings.Add($"An invalid quantity for \"{candidate.ProductName}\" was ignored.");
                continue;
            }

            var key = (candidate.ProductId, candidate.ProductVariantId);
            var currentQty = merged.TryGetValue(key, out var prior) ? prior.Quantity : 0;
            var desired = currentQty + requested;
            var capped = Math.Min(desired, MaxQuantityPerLine);

            if (capped < desired)
            {
                warnings.Add($"Only {MaxQuantityPerLine} units of \"{candidate.ProductName}\" can be ordered.");
            }

            var available = Math.Max(0, candidate.AvailableStock);
            if (available == 0)
            {
                warnings.Add($"\"{candidate.ProductName}\" is out of stock and was removed from your cart.");
                merged.Remove(key);
                continue;
            }

            var finalQty = Math.Min(capped, available);
            if (finalQty < desired)
            {
                warnings.Add($"Only {available} unit(s) of \"{candidate.ProductName}\" remain in stock.");
            }

            if (finalQty < 1)
            {
                merged.Remove(key);
                continue;
            }

            merged[key] = new MergedCartLine(
                candidate.ProductId,
                candidate.ProductVariantId,
                finalQty,
                candidate.CurrentUnitPrice,
                candidate.ProductName);
        }

        // 3. Drop lines that ended up empty.
        var lines = merged.Values
            .Where(l => l.Quantity > 0)
            .OrderBy(l => l.ProductId)
            .ToList();

        return new MergeOutcome(lines, warnings);
    }

    /// <summary>Clamps a requested quantity into the allowed range.</summary>
    public static int NormalizeQuantity(int? requested) =>
        requested is null or < 1 ? 1 : Math.Min(requested.Value, MaxQuantityPerLine);
}