using Ecommerce.Core.Dtos.Cart;

namespace Ecommerce.Core.Interfaces.Services;

/// <summary>
/// Cart operations. Identity is passed explicitly (customer id when authenticated, anonymous id
/// otherwise) so the service has no dependency on ASP.NET Session or HttpContext.
/// </summary>
public interface ICartService
{
    Task<CartDto> GetCartAsync(int? customerId, string? anonymousId, CancellationToken ct = default);

    Task<CartDto> AddItemAsync(int? customerId, string? anonymousId, AddCartItemRequest request, CancellationToken ct = default);

    Task<CartDto> UpdateItemAsync(int? customerId, string? anonymousId, int productId, UpdateCartItemRequest request, CancellationToken ct = default);

    Task<CartDto> RemoveItemAsync(int? customerId, string? anonymousId, int productId, CancellationToken ct = default);

    Task<CartDto> ClearAsync(int? customerId, string? anonymousId, CancellationToken ct = default);

    /// <summary>
    /// Folds the anonymous guest cart into the authenticated customer's cart and discards the guest cart.
    /// </summary>
    Task<CartDto> MergeAsync(int customerId, string? anonymousId, CancellationToken ct = default);
}