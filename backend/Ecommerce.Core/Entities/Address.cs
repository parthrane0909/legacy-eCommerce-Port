namespace Ecommerce.Core.Entities;

/// <summary>A saved customer address (shipping or billing).</summary>
public class Address
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    /// <summary>Preserved from the legacy checkout form.</summary>
    public string? Phone { get; set; }

    public bool IsDefault { get; set; }

    public ICollection<Order> ShippingOrders { get; set; } = new List<Order>();

    public ICollection<Order> BillingOrders { get; set; } = new List<Order>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}