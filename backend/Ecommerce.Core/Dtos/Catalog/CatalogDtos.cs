namespace Ecommerce.Core.Dtos.Catalog;

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int SortOrder,
    int? ParentId,
    int ProductCount,
    IReadOnlyList<CategoryDto> Children);

/// <summary>Compact card shape used by grids, rails and the cart drawer.</summary>
public record ProductSummaryDto(
    int Id,
    string Name,
    string Slug,
    decimal Price,
    decimal? WasPrice,
    int? DiscountPercent,
    string ImageUrl,
    string CategoryName,
    bool IsFeatured,
    bool InStock,
    int Stock,
    decimal? RatingNum);

public record ProductVariantDto(
    int Id,
    string Name,
    string? Sku,
    decimal PriceDelta,
    int Stock);

public record ProductImageDto(
    int Id,
    string Url,
    string? AltText,
    bool IsPrimary,
    int SortOrder);

public record ProductDetailDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    decimal? WasPrice,
    int? DiscountPercent,
    string ImageUrl,
    int Stock,
    bool InStock,
    bool IsFeatured,
    decimal? RatingNum,
    int CategoryId,
    string CategoryName,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants);

/// <summary>Query parameters for GET /api/products.</summary>
public record ProductQuery(
    int? Page,
    int? PageSize,
    int? CategoryId,
    string? Search,
    string? Sort,
    bool IncludeDescendantCategories = true,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? InStockOnly = null);