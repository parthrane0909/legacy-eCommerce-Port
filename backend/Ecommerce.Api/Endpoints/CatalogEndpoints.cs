using Ecommerce.Api.Auth;
using Ecommerce.Core.Dtos.Catalog;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Endpoints;

/// <summary>GET /api/categories and GET /api/products/* — anonymous storefront catalogue.</summary>
public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catalog");

        group.MapGet("/categories", GetCategoriesAsync)
            .AllowAnonymous()
            .WithName("ListCategories")
            .WithSummary("Hierarchical category tree with product counts")
            .Produces<IReadOnlyList<CategoryDto>>();

        group.MapGet("/categories/flat", GetCategoriesFlatAsync)
            .AllowAnonymous()
            .WithName("ListCategoriesFlat")
            .WithSummary("Flat list of active categories")
            .Produces<IReadOnlyList<CategoryDto>>();

        group.MapGet("/products", GetProductsAsync)
            .AllowAnonymous()
            .WithName("ListProducts")
            .WithSummary("Paged, filtered, searchable, sorted product list")
            .Produces<Core.Common.PagedResult<ProductSummaryDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/products/{id:int:min(1)}", GetProductAsync)
            .AllowAnonymous()
            .WithName("GetProduct")
            .WithSummary("Product detail with images and variants")
            .Produces<ProductDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/products/home", GetHomeAsync)
            .AllowAnonymous()
            .WithName("HomeProducts")
            .WithSummary("Featured, new-arrival and deal rails for the home page")
            .Produces<HomeProductsDto>();

        return group;
    }

    private static Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(
        ICatalogService catalog, CancellationToken ct) =>
        catalog.GetCategoryTreeAsync(ct);

    private static Task<IReadOnlyList<CategoryDto>> GetCategoriesFlatAsync(
        ICatalogService catalog, CancellationToken ct) =>
        catalog.GetCategoriesAsync(ct);

    private static Task<Core.Common.PagedResult<ProductSummaryDto>> GetProductsAsync(
        [AsParameters] ProductQueryParameters query,
        ICatalogService catalog,
        CancellationToken ct) =>
        catalog.GetProductsAsync(query.ToQuery(), ct);

    private static async Task<Results<Ok<ProductDetailDto>, NotFound>> GetProductAsync(
        int id, ICatalogService catalog, CancellationToken ct)
    {
        var product = await catalog.GetProductAsync(id, ct);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<HomeProductsDto> GetHomeAsync(ICatalogService catalog, CancellationToken ct)
    {
        // Sequential, not Task.WhenAll: all four queries share one scoped DbContext, and EF Core
        // forbids concurrent operations on a single context instance. The queries are small and
        // indexed, so the wall-clock cost of serialising them is negligible.
        // Ten per collection rather than the previous eight. The underlying pools are larger than
        // these limits (65 active products for arrivals, 13 discounted for deals), so the caps
        // were what truncated the rails rather than a shortage of eligible products. Every
        // collection keeps its existing eligibility rule and ordering; only the count grows.
        var featured = await catalog.GetFeaturedAsync(12, ct);
        var arrivals = await catalog.GetNewArrivalsAsync(10, ct);
        var deals = await catalog.GetDealsAsync(10, ct);
        var categories = await catalog.GetCategoryTreeAsync(ct);

        return new HomeProductsDto(featured, arrivals, deals, categories);
    }
}

/// <summary>
/// Query-string binding for GET /api/products.
/// </summary>
/// <remarks>
/// <c>includeDescendantCategories</c> defaults to <c>true</c> so that selecting a parent category
/// (for example "Electronics") returns products from its children too. This preserves the legacy
/// behaviour, where <c>CollectCategoryIds</c> walked the hierarchy before filtering.
/// </remarks>
public sealed record ProductQueryParameters(
    int? Page,
    int? PageSize,
    int? CategoryId,
    string? Search,
    string? Sort,
    bool IncludeDescendantCategories = true,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? InStockOnly = null)
{
    public ProductQuery ToQuery() => new(
        Page, PageSize, CategoryId, Search, Sort, IncludeDescendantCategories, MinPrice, MaxPrice, InStockOnly);
}

/// <summary>Storefront rails for the home page.</summary>
public record HomeProductsDto(
    IReadOnlyList<ProductSummaryDto> Featured,
    IReadOnlyList<ProductSummaryDto> NewArrivals,
    IReadOnlyList<ProductSummaryDto> Deals,
    IReadOnlyList<CategoryDto> Categories);