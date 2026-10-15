namespace Ecommerce.Core.Dtos.Admin;

public record AdminProductDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int Stock,
    string ImageUrl,
    int? DiscountPercent,
    decimal? RatingNum,
    bool IsFeatured,
    bool IsActive,
    int CategoryId,
    string CategoryName,
    int? VariantCount,
    int? ImageCount,
    DateTime CreatedAt);

public record AdminProductRequest(
    string Name,
    string? Slug,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    int? DiscountPercent,
    decimal? RatingNum,
    bool IsFeatured,
    bool IsActive,
    int CategoryId);

public record AdminCategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int SortOrder,
    int? ParentId,
    string? ParentName,
    int ProductCount);

public record AdminCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    bool IsActive,
    int SortOrder,
    int? ParentId);

public record AdminOrderDto(
    int Id,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    decimal Total,
    int ItemCount,
    string CustomerName,
    string CustomerEmail);

public record UpdateOrderStatusRequest(string Status);

public record AdminDashboardDto(
    int ProductCount,
    int CategoryCount,
    int CustomerCount,
    int OrderCount,
    int LowStockCount,
    decimal TotalRevenue,
    IReadOnlyList<AdminOrderDto> RecentOrders);