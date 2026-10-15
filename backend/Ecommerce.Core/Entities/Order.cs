namespace Ecommerce.Core.Entities;

/// <summary>A placed order.</summary>
/// <remarks>
/// The shipping/billing fields are <b>snapshots</b> taken at purchase time. They are the source of
/// truth for order history: deleting a customer address sets the corresponding FK to null but can
/// never corrupt an existing order's address (see <c>OrderConfiguration</c>).
/// </remarks>
public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = OrderStatus.Pending;

    public decimal Subtotal { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total { get; set; }

    public string ShippingMethod { get; set; } = ShippingOptions.Standard;

    /// <summary>Payment method. Demonstration only — no card details are ever collected or stored.</summary>
    public string PaymentMethod { get; set; } = "Card";

    /// <summary>
    /// Client-supplied idempotency key for this checkout attempt. Unique when present, so a repeated
    /// submission of the same request collapses onto the original order instead of creating a second one.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    // ---- Shipping address snapshot ----
    public int? ShippingAddressId { get; set; }

    public Address? ShippingAddress { get; set; }

    public string ShipFirstName { get; set; } = string.Empty;

    public string ShipLastName { get; set; } = string.Empty;

    public string ShipLine1 { get; set; } = string.Empty;

    public string? ShipLine2 { get; set; }

    public string ShipCity { get; set; } = string.Empty;

    public string ShipState { get; set; } = string.Empty;

    public string ShipPostalCode { get; set; } = string.Empty;

    public string ShipCountry { get; set; } = string.Empty;

    public string? ShipPhone { get; set; }

    // ---- Billing address snapshot ----
    public int? BillingAddressId { get; set; }

    public Address? BillingAddress { get; set; }

    public string BillFirstName { get; set; } = string.Empty;

    public string BillLastName { get; set; } = string.Empty;

    public string BillLine1 { get; set; } = string.Empty;

    public string? BillLine2 { get; set; }

    public string BillCity { get; set; } = string.Empty;

    public string BillState { get; set; } = string.Empty;

    public string BillPostalCode { get; set; } = string.Empty;

    public string BillCountry { get; set; } = string.Empty;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}