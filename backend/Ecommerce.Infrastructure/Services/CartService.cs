using Ecommerce.Core.Domain;
using Ecommerce.Core.Dtos.Cart;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// API-backed cart for both guests and authenticated customers. This replaces the legacy
/// <c>HttpContextCartSession</c> entirely: identity is an explicit argument and the contents live in
/// SQL, not ASP.NET Session.
/// </summary>
/// <remarks>
/// Ownership rules:
/// <list type="bullet">
/// <item>An authenticated caller always operates on the cart owned by their customer id.</item>
/// <item>An anonymous caller operates on the cart owned by their anonymous id.</item>
/// <item>A customer id takes precedence over an anonymous id, so a stale guest cookie can never
/// read or mutate another shopper's cart after login.</item>
/// </list>
/// Every price is re-derived from the catalogue; nothing is trusted from the request beyond ids and quantity.
/// </remarks>
public sealed class CartService(EcommerceDbContext db) : ICartService
{
    public async Task<CartDto> GetCartAsync(int? customerId, string? anonymousId, CancellationToken ct = default) =>
        await BuildAsync(await GetOrCreateCartAsync(customerId, anonymousId, ct), ct);

    public async Task<CartDto> AddItemAsync(
        int? customerId, string? anonymousId, AddCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(customerId, anonymousId, ct);

        var product = await db.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, ct);

        if (product is null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        var variant = request.ProductVariantId is { } variantId
            ? product.Variants.FirstOrDefault(v => v.Id == variantId)
            : null;

        if (request.ProductVariantId is not null && variant is null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        var quantity = CartMergePlanner.NormalizeQuantity(request.Quantity);
        var unitPrice = PricingCalculator.CurrentUnitPrice(product, variant);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == product.Id && i.ProductVariantId == variant?.Id);

        if (line is null)
        {
            cart.Items.Add(new CartItem
            {
                Cart = cart,
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = unitPrice,
                Quantity = quantity,
            });
        }
        else
        {
            line.Quantity += quantity;
            line.UnitPrice = unitPrice;
            line.ProductName = product.Name;
            line.ImageUrl = product.ImageUrl;
        }

        cart.UpdatedAt = DateTime.UtcNow;

        await ValidateStockAsync(cart, ct);
        await db.SaveChangesAsync(ct);

        return await BuildAsync(cart, ct);
    }

    public async Task<CartDto> UpdateItemAsync(
        int? customerId, string? anonymousId, int productId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(customerId, anonymousId, ct);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line is null)
        {
            throw new CartItemNotFoundException(productId);
        }

        var quantity = CartMergePlanner.NormalizeQuantity(request.Quantity);
        line.Quantity = quantity;
        cart.UpdatedAt = DateTime.UtcNow;

        await ValidateStockAsync(cart, ct);
        await db.SaveChangesAsync(ct);

        return await BuildAsync(cart, ct);
    }

    public async Task<CartDto> RemoveItemAsync(
        int? customerId, string? anonymousId, int productId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(customerId, anonymousId, ct);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (line is not null)
        {
            cart.Items.Remove(line);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await BuildAsync(cart, ct);
    }

    public async Task<CartDto> ClearAsync(int? customerId, string? anonymousId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(customerId, anonymousId, ct);

        db.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return await BuildAsync(cart, ct);
    }

    /// <summary>
    /// Folds the guest cart into the customer's cart. Handles duplicates, deleted products,
    /// inactive products, invalid quantities and stock limits, then discards the guest cart.
    /// </summary>
    public async Task<CartDto> MergeAsync(int customerId, string? anonymousId, CancellationToken ct = default)
    {
        var userCart = await GetOrCreateCartAsync(customerId, null, ct);

        if (string.IsNullOrWhiteSpace(anonymousId))
        {
            return await BuildAsync(userCart, ct);
        }

        var guestCart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, ct);

        if (guestCart is null)
        {
            return await BuildAsync(userCart, ct);
        }

        var guestProductIds = guestCart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(p => p.Variants)
            .Where(p => guestProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var candidates = guestCart.Items.Select(item =>
        {
            var product = products.GetValueOrDefault(item.ProductId);
            var variant = product?.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId);

            return new MergeCandidate(
                item.ProductId,
                item.ProductVariantId,
                item.Quantity,
                product is not null,
                product?.IsActive ?? false,
                AvailableStock(product, variant),
                product is null ? 0m : PricingCalculator.CurrentUnitPrice(product, variant),
                product?.Name ?? item.ProductName);
        }).ToList();

        var existing = userCart.Items
            .Select(i => new MergedCartLine(i.ProductId, i.ProductVariantId, i.Quantity, i.UnitPrice, i.ProductName))
            .ToList();

        var outcome = CartMergePlanner.Merge(existing, candidates);

        // Rebuild the authenticated cart from the merge plan.
        db.CartItems.RemoveRange(userCart.Items);
        userCart.Items.Clear();

        var productIds = outcome.Lines.Select(l => l.ProductId).Distinct().ToList();
        var mergedProducts = await db.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var line in outcome.Lines)
        {
            var product = mergedProducts.GetValueOrDefault(line.ProductId);
            userCart.Items.Add(new CartItem
            {
                Cart = userCart,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = product?.Name ?? line.ProductName,
                ImageUrl = product?.ImageUrl,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
            });
        }

        userCart.UpdatedAt = DateTime.UtcNow;

        // The guest cart is consumed by the merge.
        if (guestCart.Id != userCart.Id)
        {
            db.Carts.Remove(guestCart);
        }

        await db.SaveChangesAsync(ct);

        var result = await BuildAsync(userCart, ct);

        if (outcome.HasWarnings)
        {
            throw new CartMergeConflictException(result, outcome.Warnings);
        }

        return result;
    }

    // ---- helpers ---------------------------------------------------------------

    private async Task<Cart> GetOrCreateCartAsync(int? customerId, string? anonymousId, CancellationToken ct)
    {
        if (customerId is { } id)
        {
            var owned = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == id, ct);

            if (owned is not null)
            {
                return owned;
            }

            var created = new Cart { CustomerId = id, CreatedAt = DateTime.UtcNow };
            db.Carts.Add(created);
            await db.SaveChangesAsync(ct);
            return created;
        }

        if (string.IsNullOrWhiteSpace(anonymousId))
        {
            throw new CartIdentityRequiredException();
        }

        var guestCart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, ct);

        if (guestCart is not null)
        {
            return guestCart;
        }

        var newGuest = new Cart { AnonymousId = anonymousId, CreatedAt = DateTime.UtcNow };
        db.Carts.Add(newGuest);
        await db.SaveChangesAsync(ct);
        return newGuest;
    }

    /// <summary>
    /// Units currently available for a product, or for a specific variant when one is chosen.
    /// </summary>
    /// <remarks>
    /// Returns the true available count. It must not be clamped to the requested quantity: the merge
    /// path compares a running total against this value, and clamping would cap the merged line at the
    /// quantity the guest happened to ask for.
    /// </remarks>
    private static int AvailableStock(Product? product, ProductVariant? variant)
    {
        if (product is null)
        {
            return 0;
        }

        var stock = variant is not null ? variant.Stock : product.Stock;
        return Math.Max(0, stock);
    }

    /// <summary>
    /// Re-checks every line against live stock and clamps (or throws when a line is now unsatisfiable).
    /// Clamping keeps a partially-available cart usable instead of failing the whole request.
    /// </summary>
    private async Task ValidateStockAsync(Cart cart, CancellationToken ct)
    {
        if (cart.Items.Count == 0)
        {
            return;
        }

        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var toRemove = new List<CartItem>();

        foreach (var line in cart.Items)
        {
            var product = products.GetValueOrDefault(line.ProductId);

            if (product is null || !product.IsActive)
            {
                toRemove.Add(line);
                continue;
            }

            var variant = line.ProductVariantId is { } vid
                ? product.Variants.FirstOrDefault(v => v.Id == vid)
                : null;

            var stock = variant is not null ? variant.Stock : product.Stock;

            if (stock <= 0)
            {
                toRemove.Add(line);
                continue;
            }

            // Re-price from the current catalogue so stale cart prices never survive.
            line.UnitPrice = PricingCalculator.CurrentUnitPrice(product, variant);
            line.ProductName = product.Name;
            line.ImageUrl = product.ImageUrl;

            if (line.Quantity > stock)
            {
                line.Quantity = stock;
            }

            line.Quantity = CartMergePlanner.NormalizeQuantity(line.Quantity);
        }

        foreach (var line in toRemove)
        {
            cart.Items.Remove(line);
        }
    }

    private async Task<CartDto> BuildAsync(Cart cart, CancellationToken ct)
    {
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var items = cart.Items
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var product = products.GetValueOrDefault(i.ProductId);
                var variant = product?.Variants.FirstOrDefault(v => v.Id == i.ProductVariantId);
                var stock = variant is not null ? variant.Stock : product?.Stock ?? 0;

                return new CartItemDto(
                    i.Id, i.ProductId, i.ProductName, variant?.Name, i.ImageUrl ?? product?.ImageUrl ?? "",
                    i.Quantity, i.UnitPrice, PricingCalculator.LineTotal(i.UnitPrice, i.Quantity),
                    stock, stock > 0);
            })
            .ToList();

        return new CartDto(
            cart.Id,
            cart.CustomerId is null,
            items.Sum(i => i.Quantity),
            PricingCalculator.Round(items.Sum(i => i.LineTotal)),
            items);
    }
}

public sealed class CartIdentityRequiredException()
    : Exception("A cart identity is required.");

public sealed class ProductNotFoundException(int productId)
    : Exception($"Product {productId} was not found or is not available.");

public sealed class CartItemNotFoundException(int productId)
    : Exception($"Product {productId} is not in your cart.");

/// <summary>
/// The merge completed but some guest lines had to be dropped or clamped. The resulting cart is
/// attached so the API can return 200 with the cart plus a warning list rather than failing outright.
/// </summary>
public sealed class CartMergeConflictException(CartDto cart, IReadOnlyList<string> warnings)
    : Exception("The cart was merged with adjustments.")
{
    public CartDto Cart { get; } = cart;

    public IReadOnlyList<string> Warnings { get; } = warnings;
}