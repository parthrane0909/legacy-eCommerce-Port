using Ecommerce.Core.Common;
using Ecommerce.Core.Dtos.Admin;

namespace Ecommerce.Core.Interfaces.Services;

/// <summary>Administrative catalogue, order and reporting operations. Always invoked behind an Admin policy.</summary>
public interface IAdminService
{
    Task<PagedResult<AdminProductDto>> GetProductsAsync(int? page, int? pageSize, string? search, CancellationToken ct = default);

    /// <summary>Creates a product. Returns non-null: a successful save always yields a readable row.</summary>
    Task<AdminProductDto> CreateProductAsync(AdminProductRequest request, CancellationToken ct = default);

    Task<AdminProductDto?> UpdateProductAsync(int id, AdminProductRequest request, CancellationToken ct = default);

    /// <summary>Returns false when not found. Products referenced by orders are archived instead of deleted.</summary>
    Task<bool> DeleteProductAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Creates a category. Returns non-null: a successful save always yields a readable row.</summary>
    Task<AdminCategoryDto> CreateCategoryAsync(AdminCategoryRequest request, CancellationToken ct = default);

    Task<AdminCategoryDto?> UpdateCategoryAsync(int id, AdminCategoryRequest request, CancellationToken ct = default);

    Task<bool> DeleteCategoryAsync(int id, CancellationToken ct = default);

    Task<PagedResult<AdminOrderDto>> GetOrdersAsync(int? page, int? pageSize, string? status, CancellationToken ct = default);

    /// <summary>Returns null when the order or status is unknown, or the transition is not allowed.</summary>
    Task<AdminOrderDto?> UpdateOrderStatusAsync(int orderId, string status, CancellationToken ct = default);

    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}