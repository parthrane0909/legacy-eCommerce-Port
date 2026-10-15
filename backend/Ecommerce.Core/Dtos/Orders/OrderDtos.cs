namespace Ecommerce.Core.Dtos.Orders;

public record AddressInput(
    string FirstName,
    string LastName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone);

public record PlaceOrderRequest(
    string ShippingMethod,
    AddressInput ShippingAddress,
    AddressInput BillingAddress,
    bool BillingSameAsShipping = true);

public record ShippingOptionDto(string Code, string Label, decimal Cost, string Estimate);

public record OrderItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string? VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string ShippingMethod,
    int ItemCount);

public record OrderDetailDto(
    int Id,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string ShippingMethod,
    string PaymentMethod,
    AddressSnapshotDto ShippingAddress,
    AddressSnapshotDto BillingAddress,
    IReadOnlyList<OrderItemDto> Items);

public record AddressSnapshotDto(
    string FullName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone);