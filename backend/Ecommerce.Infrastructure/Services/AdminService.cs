using Ecommerce.Core.Common;
using Ecommerce.Core.Domain;
using Ecommerce.Core.Dtos.Admin;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Administrative catalogue, order and reporting operations.
/// </summary>
/// <remarks>
/// Every method here is reachable only through an endpoint carrying
/// <c>RequireAuthorization(policy =&gt; policy.RequireRole(Roles.Admin))</c>; the service itself does
/// not re-check the role because it is never invoked from an unauthenticated path.
/// The legacy application had no admin surface at all, so all of this is new behaviour.
/// </remarks>
public sealed class AdminService(EcommerceDbContext db) : IAdminService
{
    private const int LowStockThreshold = 12; // Preserves the legacy "Only N left" presentation rule.

    public async Task<PagedResult<AdminProductDto>> GetProductsAsync(
        int? page, int? pageSize, string? search, CancellationToken ct = default)
    {
        var p = Paging.NormalizePage(page);
        var size = Paging.NormalizePageSize(pageSize);

        var q = db.Products.AsNoTracking().Include(x => x.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.Name.Contains(term) || x.Slug.Contains(term));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(x => new AdminProductDto(
                x.Id, x.Name, x.Slug, x.Description, x.Price, x.Stock, x.ImageUrl, x.DiscountPercent,
                x.RatingNum, x.IsFeatured, x.IsActive, x.CategoryId,
                x.Category != null ? x.Category.Name : "", x.Variants.Count, x.Images.Count, x.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminProductDto> { Items = items, Page = p, PageSize = size, TotalCount = total };
    }

    public async Task<AdminProductDto> CreateProductAsync(AdminProductRequest request, CancellationToken ct = default)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new CategoryNotFoundException(request.CategoryId);
        }

        var slug = await ResolveProductSlugAsync(request.Slug, request.Name, null, ct);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            Price = PricingCalculator.Round(request.Price),
            Stock = request.Stock,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl)
                ? "/images/products/placeholder.jpg"
                : request.ImageUrl.Trim(),
            DiscountPercent = request.DiscountPercent,
            RatingNum = request.RatingNum,
            IsFeatured = request.IsFeatured,
            IsActive = request.IsActive,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return await LoadProductAsync(product.Id, ct)
            ?? throw new InvalidOperationException($"Product {product.Id} was saved but could not be reloaded.");
    }

    public async Task<AdminProductDto?> UpdateProductAsync(int id, AdminProductRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return null;
        }

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new CategoryNotFoundException(request.CategoryId);
        }

        product.Name = request.Name.Trim();
        product.Slug = await ResolveProductSlugAsync(request.Slug, request.Name, id, ct);
        product.Description = request.Description?.Trim();
        product.Price = PricingCalculator.Round(request.Price);
        product.Stock = request.Stock;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            product.ImageUrl = request.ImageUrl.Trim();
        }
        product.DiscountPercent = request.DiscountPercent;
        product.RatingNum = request.RatingNum;
        product.IsFeatured = request.IsFeatured;
        product.IsActive = request.IsActive;
        product.CategoryId = request.CategoryId;

        await db.SaveChangesAsync(ct);
        return await LoadProductAsync(product.Id, ct);
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return false;
        }

        // Products referenced by an order must keep existing so order history stays readable.
        // Archive them instead of deleting.
        if (await db.OrderItems.AnyAsync(i => i.ProductId == id, ct))
        {
            product.IsActive = false;
            await db.SaveChangesAsync(ct);
            return true;
        }

        // Drop cart lines that point at the product before removing it, so the cart stays consistent.
        var cartLines = await db.CartItems.Where(c => c.ProductId == id).ToListAsync(ct);
        if (cartLines.Count > 0)
        {
            db.CartItems.RemoveRange(cartLines);
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.ParentId,
                c.Parent != null ? c.Parent.Name : null, c.Products.Count))
            .ToListAsync(ct);

    public async Task<AdminCategoryDto> CreateCategoryAsync(AdminCategoryRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is { } parentId && !await db.Categories.AnyAsync(c => c.Id == parentId, ct))
        {
            throw new CategoryNotFoundException(parentId);
        }

        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = await ResolveCategorySlugAsync(request.Slug, request.Name, null, ct),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            ParentId = request.ParentId,
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return await LoadCategoryAsync(category.Id, ct)
            ?? throw new InvalidOperationException($"Category {category.Id} was saved but could not be reloaded.");
    }

    public async Task<AdminCategoryDto?> UpdateCategoryAsync(int id, AdminCategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return null;
        }

        // Guard against making a category its own ancestor, which would corrupt the tree.
        if (request.ParentId is { } parentId)
        {
            if (parentId == id)
            {
                throw new InvalidCategoryHierarchyException("A category cannot be its own parent.");
            }

            if (await IsDescendantAsync(id, parentId, ct))
            {
                throw new InvalidCategoryHierarchyException("A category cannot be moved under one of its own children.");
            }

            if (!await db.Categories.AnyAsync(c => c.Id == parentId, ct))
            {
                throw new CategoryNotFoundException(parentId);
            }
        }

        category.Name = request.Name.Trim();
        category.Slug = await ResolveCategorySlugAsync(request.Slug, request.Name, id, ct);
        category.Description = request.Description?.Trim();
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;
        category.ParentId = request.ParentId;

        await db.SaveChangesAsync(ct);
        return await LoadCategoryAsync(category.Id, ct);
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return false;
        }

        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
        {
            throw new InvalidCategoryHierarchyException("Move or delete the child categories first.");
        }

        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
        {
            throw new InvalidCategoryHierarchyException("This category still contains products.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<AdminOrderDto>> GetOrdersAsync(
        int? page, int? pageSize, string? status, CancellationToken ct = default)
    {
        var p = Paging.NormalizePage(page);
        var size = Paging.NormalizePageSize(pageSize);

        var q = db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && OrderStatus.All.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            var normalized = OrderStatus.All.First(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
            q = q.Where(o => o.Status == normalized);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(o => new AdminOrderDto(
                o.Id, o.OrderNumber, o.OrderDate, o.Status, o.Total, o.Items.Sum(i => i.Quantity),
                o.Customer != null ? o.Customer.FirstName + " " + o.Customer.LastName : "",
                o.Customer != null ? o.Customer.Email : ""))
            .ToListAsync(ct);

        return new PagedResult<AdminOrderDto> { Items = items, Page = p, PageSize = size, TotalCount = total };
    }

    public async Task<AdminOrderDto?> UpdateOrderStatusAsync(int orderId, string status, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Customer).Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null)
        {
            return null;
        }

        if (!OrderStatus.All.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOrderStatusException(status);
        }

        var normalized = OrderStatus.All.First(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (!OrderStatus.CanTransition(order.Status, normalized))
        {
            throw new InvalidOrderStatusTransitionException(order.Status, normalized);
        }

        order.Status = normalized;
        await db.SaveChangesAsync(ct);

        return new AdminOrderDto(
            order.Id, order.OrderNumber, order.OrderDate, order.Status, order.Total,
            order.Items.Sum(i => i.Quantity),
            order.Customer is null ? "" : order.Customer.FullName,
            order.Customer?.Email ?? "");
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var productCount = await db.Products.CountAsync(ct);
        var categoryCount = await db.Categories.CountAsync(ct);
        var customerCount = await db.Customers.CountAsync(ct);
        var orderCount = await db.Orders.CountAsync(ct);
        var lowStock = await db.Products.CountAsync(p => p.Stock <= LowStockThreshold, ct);

        var revenue = await db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var recent = await db.Orders.AsNoTracking()
            .Include(o => o.Customer).Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Take(10)
            .Select(o => new AdminOrderDto(
                o.Id, o.OrderNumber, o.OrderDate, o.Status, o.Total, o.Items.Sum(i => i.Quantity),
                o.Customer != null ? o.Customer.FirstName + " " + o.Customer.LastName : "",
                o.Customer != null ? o.Customer.Email : ""))
            .ToListAsync(ct);

        return new AdminDashboardDto(
            productCount, categoryCount, customerCount, orderCount, lowStock,
            PricingCalculator.Round(revenue), recent);
    }

    // ---- helpers ---------------------------------------------------------------

    private async Task<AdminProductDto?> LoadProductAsync(int id, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Include(p => p.Category).Include(p => p.Variants).Include(p => p.Images)
            .Where(p => p.Id == id)
            .Select(p => new AdminProductDto(
                p.Id, p.Name, p.Slug, p.Description, p.Price, p.Stock, p.ImageUrl, p.DiscountPercent,
                p.RatingNum, p.IsFeatured, p.IsActive, p.CategoryId,
                p.Category != null ? p.Category.Name : "", p.Variants.Count, p.Images.Count, p.CreatedAt))
            .FirstOrDefaultAsync(ct);

    private async Task<AdminCategoryDto?> LoadCategoryAsync(int id, CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new AdminCategoryDto(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.ParentId,
                c.Parent != null ? c.Parent.Name : null, c.Products.Count))
            .FirstOrDefaultAsync(ct);

    private async Task<string> ResolveProductSlugAsync(
        string? explicitSlug, string name, int? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug(explicitSlug, name);

        return await SlugGenerator.EnsureUnique(baseSlug, candidate =>
            db.Products.AnyAsync(p => p.Slug == candidate && (excludeId == null || p.Id != excludeId), ct));
    }

    private async Task<string> ResolveCategorySlugAsync(
        string? explicitSlug, string name, int? excludeId, CancellationToken ct)
    {
        var baseSlug = Slug(explicitSlug, name);

        return await SlugGenerator.EnsureUnique(baseSlug, candidate =>
            db.Categories.AnyAsync(c => c.Slug == candidate && (excludeId == null || c.Id != excludeId), ct));
    }

    private static string Slug(string? explicitSlug, string name) =>
        SlugGenerator.Generate(explicitSlug) is { Length: > 0 } explicitValue
            ? explicitValue
            : SlugGenerator.Generate(name) is { Length: > 0 } generated ? generated : "item";

    /// <summary>True when <paramref name="candidateId"/> sits beneath <paramref name="ancestorId"/>.</summary>
    private async Task<bool> IsDescendantAsync(int ancestorId, int candidateId, CancellationToken ct)
    {
        var current = candidateId;
        for (var i = 0; i < 32; i++)
        {
            var parentId = await db.Categories
                .Where(c => c.Id == current)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(ct);

            if (parentId is null)
            {
                return false;
            }

            if (parentId == ancestorId)
            {
                return true;
            }

            current = parentId.Value;
        }

        return false;
    }
}

public sealed class CategoryNotFoundException(int id)
    : Exception($"Category {id} was not found.");

public sealed class InvalidCategoryHierarchyException(string message) : Exception(message);

public sealed class InvalidOrderStatusException(string status)
    : Exception($"'{status}' is not a valid order status.");

public sealed class InvalidOrderStatusTransitionException(string from, string to)
    : Exception($"An order cannot move from '{from}' to '{to}'.");