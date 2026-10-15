using Ecommerce.Core.Common;
using Ecommerce.Core.Domain;
using Ecommerce.Core.Dtos.Catalog;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Read-side catalogue queries. All filtering, searching, sorting and paging happens in the
/// database — the SPA never downloads the whole catalogue to filter client-side.
/// </summary>
public sealed class CatalogService(EcommerceDbContext db) : ICatalogService
{
    public async Task<PagedResult<ProductSummaryDto>> GetProductsAsync(ProductQuery query, CancellationToken ct = default)
    {
        var page = Paging.NormalizePage(query.Page);
        var pageSize = Paging.NormalizePageSize(query.PageSize);

        var q = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (query.CategoryId is { } categoryId)
        {
            var ids = query.IncludeDescendantCategories
                ? await GetCategoryAndDescendantIdsAsync(categoryId, ct)
                : [categoryId];

            q = q.Where(p => ids.Contains(p.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }

        if (query.MinPrice is { } min)
        {
            q = q.Where(p => p.Price >= min);
        }

        if (query.MaxPrice is { } max)
        {
            q = q.Where(p => p.Price <= max);
        }

        if (query.InStockOnly == true)
        {
            q = q.Where(p => p.Stock > 0);
        }

        var totalCount = await q.CountAsync(ct);

        q = ApplySort(q, ProductSort.Normalize(query.Sort));

        var items = (await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductCardRow(
                p.Id, p.Name, p.Slug, p.Price, p.DiscountPercent, p.ImageUrl,
                p.Category != null ? p.Category.Name : "", p.IsFeatured, p.Stock, p.RatingNum))
            .ToListAsync(ct))
            .Select(ToSummary)
            .ToList();

        return new PagedResult<ProductSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ProductDetailDto?> GetProductAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);

        if (product is null)
        {
            return null;
        }

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.IsPrimary, i.SortOrder))
            .ToList();

        var variants = product.Variants
            .OrderBy(v => v.PriceDelta).ThenBy(v => v.Id)
            .Select(v => new ProductVariantDto(v.Id, v.Name, v.Sku, v.PriceDelta, v.Stock))
            .ToList();

        return new ProductDetailDto(
            product.Id, product.Name, product.Slug, product.Description, product.Price,
            product.WasPrice, product.DiscountPercent, product.ImageUrl, product.Stock,
            product.Stock > 0, product.IsFeatured, product.RatingNum,
            product.CategoryId, product.Category?.Name ?? string.Empty, images, variants);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await BuildCategoryDtosAsync(ct, recursiveCounts: true);

    /// <summary>Builds the nested tree used by the sidebar and the home page category tiles.</summary>
    public async Task<IReadOnlyList<CategoryDto>> GetCategoryTreeAsync(CancellationToken ct = default)
    {
        var flat = (await BuildCategoryDtosAsync(ct, recursiveCounts: true)).ToList();

        var childrenByParent = flat
            .Where(c => c.ParentId is not null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CategoryDto>)g.ToList());

        CategoryDto Build(CategoryDto node) => node with
        {
            Children = childrenByParent.TryGetValue(node.Id, out var kids)
                ? kids.Select(Build).ToList()
                : [],
        };

        return flat.Where(c => c.ParentId is null).Select(Build).ToList();
    }

    /// <summary>
    /// Loads active categories and their direct active-product counts.
    /// When <paramref name="recursiveCounts"/> is set, each node's count is rolled up to include its
    /// descendants, preserving the legacy <c>GetProductCountRecursive</c> behaviour. The roll-up runs
    /// in memory over a single query, so it avoids the legacy implementation's N+1 problem.
    /// </summary>
    private async Task<IReadOnlyList<CategoryDto>> BuildCategoryDtosAsync(
        CancellationToken ct, bool recursiveCounts)
    {
        var rows = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryRow(
                c.Id, c.Name, c.Slug, c.Description, c.IsActive, c.SortOrder, c.ParentId,
                c.Parent != null ? c.Parent.Name : null,
                c.Products.Count(p => p.IsActive)))
            .ToListAsync(ct);

        if (!recursiveCounts)
        {
            return rows
                .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.IsActive,
                    c.SortOrder, c.ParentId, c.ProductCount, []))
                .ToList();
        }

        var childrenOf = rows
            .Where(c => c.ParentId is not null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rolledUp = new Dictionary<int, int>();

        int RollUp(int id)
        {
            if (rolledUp.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var total = rows.First(r => r.Id == id).ProductCount;
            if (childrenOf.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    total += RollUp(child.Id);
                }
            }

            rolledUp[id] = total;
            return total;
        }

        return rows
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.IsActive,
                c.SortOrder, c.ParentId, RollUp(c.Id), []))
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetCategoryAndDescendantIdsAsync(int categoryId, CancellationToken ct = default)
    {
        var ids = new List<int> { categoryId };

        // Breadth-first walk over the hierarchy, materialising each level to avoid N+1 queries.
        var frontier = new List<int> { categoryId };
        while (frontier.Count > 0)
        {
            var next = await db.Categories.AsNoTracking()
                .Where(c => c.ParentId != null && frontier.Contains(c.ParentId!.Value))
                .Select(c => c.Id)
                .ToListAsync(ct);

            var fresh = next.Where(id => !ids.Contains(id)).ToList();
            if (fresh.Count == 0)
            {
                break;
            }

            ids.AddRange(fresh);
            frontier = fresh;
        }

        return ids;
    }

    public Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedAsync(int take, CancellationToken ct = default) =>
        QueryCards(db.Products.AsNoTracking().Where(p => p.IsFeatured && p.IsActive)
            .OrderByDescending(p => p.RatingNum).ThenBy(p => p.Id), take, ct);

    public Task<IReadOnlyList<ProductSummaryDto>> GetNewArrivalsAsync(int take, CancellationToken ct = default) =>
        QueryCards(db.Products.AsNoTracking().Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id), take, ct);

    public Task<IReadOnlyList<ProductSummaryDto>> GetDealsAsync(int take, CancellationToken ct = default) =>
        QueryCards(db.Products.AsNoTracking().Where(p => p.IsActive && p.DiscountPercent != null && p.DiscountPercent > 0)
            .OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.Id), take, ct);

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string sort) => sort switch
    {
        // The secondary key must follow the primary sort direction, otherwise ties break ascending
        // regardless of the requested order.
        ProductSort.PriceAsc => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
        ProductSort.PriceDesc => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
        ProductSort.NameAsc => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
        ProductSort.NameDesc => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
        ProductSort.RatingDesc => query.OrderByDescending(p => p.RatingNum).ThenBy(p => p.Id),
        _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
    };

    /// <summary>Projection for the flat category query (the tree is assembled in memory).</summary>
    private sealed record CategoryRow(
        int Id, string Name, string Slug, string? Description, bool IsActive, int SortOrder,
        int? ParentId, string? ParentName, int ProductCount);

    private static async Task<IReadOnlyList<ProductSummaryDto>> QueryCards(
        IQueryable<Product> query, int take, CancellationToken ct) =>
        (await query.Take(Math.Clamp(take, 1, 60))
            .Select(p => new ProductCardRow(
                p.Id, p.Name, p.Slug, p.Price, p.DiscountPercent, p.ImageUrl,
                p.Category != null ? p.Category.Name : "", p.IsFeatured, p.Stock, p.RatingNum))
            .ToListAsync(ct))
        .Select(ToSummary)
        .ToList();

    /// <summary>Minimal projection used for list queries; keeps SQL translatable and cheap.</summary>
    private sealed record ProductCardRow(
        int Id, string Name, string Slug, decimal Price, int? DiscountPercent, string ImageUrl,
        string CategoryName, bool IsFeatured, int Stock, decimal? RatingNum);

    private static ProductSummaryDto ToSummary(ProductCardRow r) => new(
        r.Id, r.Name, r.Slug, r.Price, WasPrice(r.Price, r.DiscountPercent), r.DiscountPercent,
        r.ImageUrl, r.CategoryName, r.IsFeatured, r.Stock > 0, r.Stock, r.RatingNum);

    /// <summary>
    /// Presentation-only "was" price. Computed in memory because <c>Math.Round</c> over a decimal
    /// projection is not reliably translatable to SQL across providers.
    /// </summary>
    private static decimal? WasPrice(decimal price, int? discountPercent) =>
        discountPercent is > 0 and < 100
            ? PricingCalculator.Round(price / (1m - (discountPercent.Value / 100m)))
            : null;
}