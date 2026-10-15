namespace Ecommerce.Core.Entities;

/// <summary>
/// Sellable catalog item.
/// </summary>
/// <remarks>
/// Pricing contract (fixes the legacy double-discount defect identified in the audit):
/// <see cref="Price"/> is the authoritative <b>current selling price</b>. It is never
/// re-discounted downstream. <see cref="DiscountPercent"/> is presentation-only and is used to
/// derive the "was" price. The cart and the order both use <see cref="Price"/> verbatim.
/// </remarks>
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Authoritative selling price. decimal(18,2). Never float.</summary>
    public decimal Price { get; set; }

    /// <summary>Available units. Guarded by the server at cart-add and order time.</summary>
    public int Stock { get; set; }

    /// <summary>Primary image, required by the assignment data model.</summary>
    public string ImageUrl { get; set; } = "/images/products/placeholder.jpg";

    /// <summary>Presentation-only discount used to derive the "was" price.</summary>
    public int? DiscountPercent { get; set; }

    public decimal? RatingNum { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    /// <summary>True when the product can currently be purchased.</summary>
    public bool IsInStock => IsActive && Stock > 0;

    /// <summary>
    /// The pre-discount "was" price implied by <see cref="DiscountPercent"/>, or null when the
    /// product is not on sale. Presentation only — never used for money arithmetic.
    /// </summary>
    public decimal? WasPrice =>
        DiscountPercent is > 0 and < 100
            ? decimal.Round(Price / (1m - (DiscountPercent.Value / 100m)), 2)
            : null;
}