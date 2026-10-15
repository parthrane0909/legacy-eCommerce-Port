using Ecommerce.Core.Common;
using Ecommerce.Core.Dtos.Catalog;

namespace Ecommerce.Core.Interfaces.Services;

public interface ICatalogService
{
    Task<PagedResult<ProductSummaryDto>> GetProductsAsync(ProductQuery query, CancellationToken ct = default);

    Task<ProductDetailDto?> GetProductAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryDto>> GetCategoryTreeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Returns the category id plus every descendant id, for hierarchical filtering.</summary>
    Task<IReadOnlyList<int>> GetCategoryAndDescendantIdsAsync(int categoryId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedAsync(int take, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSummaryDto>> GetNewArrivalsAsync(int take, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSummaryDto>> GetDealsAsync(int take, CancellationToken ct = default);
}