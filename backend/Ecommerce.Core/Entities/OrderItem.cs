namespace Ecommerce.Core.Entities;

/// <summary>An order line. <see cref="ProductName"/> and <see cref="UnitPrice"/> are purchase-time snapshots.</summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    /// <summary>No cascade delete: a deleted product must not destroy order history.</summary>
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Snapshot — the name at the time of purchase.</summary>
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>Snapshot — the unit price charged at the time of purchase.</summary>
    public decimal UnitPrice { get; set; }

    public string? VariantName { get; set; }

    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2);
}