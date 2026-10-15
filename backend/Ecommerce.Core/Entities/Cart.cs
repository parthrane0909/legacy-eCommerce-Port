namespace Ecommerce.Core.Entities;

/// <summary>
/// A shopping cart. Exactly one of <see cref="CustomerId"/> / <see cref="AnonymousId"/> is set:
/// an authenticated cart is owned by a customer, a guest cart by an anonymous browser id.
/// This replaces the legacy ASP.NET Session cart entirely.
/// </summary>
public class Cart
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Opaque identifier issued to guest browsers. Null for authenticated carts.</summary>
    public string? AnonymousId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

    public bool IsGuest => CustomerId is null;
}