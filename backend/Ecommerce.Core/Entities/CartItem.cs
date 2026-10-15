namespace Ecommerce.Core.Entities;

/// <summary>A line in a cart. <see cref="UnitPrice"/> is a snapshot taken when the item was added.</summary>
public class CartItem
{
    public int Id { get; set; }

    public int CartId { get; set; }

    public Cart? Cart { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Quantity of units. Always &gt;= 1; a non-positive quantity is rejected server-side.</summary>
    public int Quantity { get; set; }

    /// <summary>Product price (plus variant delta) captured at add time. Re-validated at order time.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Optional chosen variant.</summary>
    public int? ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    /// <summary>Name captured at add time so the cart renders even if the product is renamed.</summary>
    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2);
}