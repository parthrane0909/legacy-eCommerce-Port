namespace Ecommerce.Core.Entities;

/// <summary>Supporting entity: a purchasable option (size/colour) that adjusts price and stock.</summary>
public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    /// <summary>Added to <see cref="Product.Price"/> when this variant is chosen. decimal(18,2).</summary>
    public decimal PriceDelta { get; set; }

    public int Stock { get; set; }
}