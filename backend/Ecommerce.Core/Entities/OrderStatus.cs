namespace Ecommerce.Core.Entities;

/// <summary>Lifecycle states for an order.</summary>
public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Pending, Paid, Shipped, Delivered, Cancelled];

    /// <summary>
    /// Allowed status transitions. A cancelled order is terminal; a non-cancelled order may not
    /// become cancelled-then-revived, and only moves forward through fulfilment.
    /// </summary>
    public static bool CanTransition(string? from, string to)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return from is null && to == Pending;
        }

        if (from == Cancelled)
        {
            return false;
        }

        if (to == Cancelled)
        {
            return from is Pending or Paid;
        }

        return (from, to) switch
        {
            (Pending, Paid) => true,
            (Paid, Shipped) => true,
            (Shipped, Delivered) => true,
            _ => false,
        };
    }
}