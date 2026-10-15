namespace Ecommerce.Core.Dtos.Cart;

public record AddCartItemRequest(int ProductId, int Quantity = 1, int? ProductVariantId = null);

public record UpdateCartItemRequest(int Quantity);

/// <summary>
/// Body for POST /api/cart/merge.
/// </summary>
/// <param name="AnonymousId">
/// Optional. The browser's guest cart id is normally read from the HttpOnly cookie the API issued,
/// so most clients send nothing. It is accepted explicitly so the merge can also be driven from a
/// client that holds the guest id itself (and so Swagger documents the field).
/// </param>
public record MergeCartRequest(string? AnonymousId);

public record CartItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string? VariantName,
    string ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    int AvailableStock,
    bool InStock);

public record CartDto(
    int Id,
    bool IsGuest,
    int ItemCount,
    decimal Subtotal,
    IReadOnlyList<CartItemDto> Items)
{
    /// <summary>
    /// Non-fatal adjustments, currently populated when a guest-cart merge had to drop or clamp items.
    /// Empty on every other operation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}