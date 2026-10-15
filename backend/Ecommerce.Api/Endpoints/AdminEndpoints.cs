using Ecommerce.Api.Auth;
using Ecommerce.Api.Validation;
using Ecommerce.Core.Dtos.Admin;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Endpoints;

/// <summary>
/// /api/admin/* — administrator-only. The legacy application had no admin portal at all, so every
/// behaviour here is new.
/// </summary>
/// <remarks>
/// Authorization is enforced server-side by <see cref="AuthExtensions.AdminPolicy"/>, which requires
/// an authenticated principal carrying the Admin role. Hiding admin links in the UI is a convenience
/// only — direct API calls are rejected with 403.
/// </remarks>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Administration")
            .RequireAuthorization(AuthExtensions.AdminPolicy);

        group.MapGet("/products", GetProductsAsync)
            .WithName("AdminListProducts")
            .WithSummary("All products, including inactive ones")
            .Produces<Core.Common.PagedResult<AdminProductDto>>();

        group.MapPost("/products", CreateProductAsync)
            .WithValidatedRequest<AdminProductRequest>()
            .WithName("AdminCreateProduct")
            .WithSummary("Create a product")
            .Produces<AdminProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/products/{id:int:min(1)}", UpdateProductAsync)
            .WithValidatedRequest<AdminProductRequest>()
            .WithName("AdminUpdateProduct")
            .WithSummary("Update a product")
            .Produces<AdminProductDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/products/{id:int:min(1)}", DeleteProductAsync)
            .WithName("AdminDeleteProduct")
            .WithSummary("Delete a product, or archive it when it is referenced by an order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/categories", GetCategoriesAsync)
            .WithName("AdminListCategories")
            .WithSummary("All categories, including inactive ones")
            .Produces<IReadOnlyList<AdminCategoryDto>>();

        group.MapPost("/categories", CreateCategoryAsync)
            .WithValidatedRequest<AdminCategoryRequest>()
            .WithName("AdminCreateCategory")
            .WithSummary("Create a category")
            .Produces<AdminCategoryDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/categories/{id:int:min(1)}", UpdateCategoryAsync)
            .WithValidatedRequest<AdminCategoryRequest>()
            .WithName("AdminUpdateCategory")
            .WithSummary("Update a category")
            .Produces<AdminCategoryDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/categories/{id:int:min(1)}", DeleteCategoryAsync)
            .WithName("AdminDeleteCategory")
            .WithSummary("Delete an empty category")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/orders", GetOrdersAsync)
            .WithName("AdminListOrders")
            .WithSummary("All orders across all customers")
            .Produces<Core.Common.PagedResult<AdminOrderDto>>();

        group.MapPut("/orders/{id:int:min(1)}/status", UpdateOrderStatusAsync)
            .WithValidatedRequest<UpdateOrderStatusRequest>()
            .WithName("AdminUpdateOrderStatus")
            .WithSummary("Advance an order's status")
            .Produces<AdminOrderDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/dashboard", GetDashboardAsync)
            .WithName("AdminDashboard")
            .WithSummary("Dashboard counts and recent orders")
            .Produces<AdminDashboardDto>();

        return group;
    }

    private static Task<Core.Common.PagedResult<AdminProductDto>> GetProductsAsync(
        int? page, int? pageSize, string? search, IAdminService admin, CancellationToken ct) =>
        admin.GetProductsAsync(page, pageSize, search, ct);

    private static async Task<Created<AdminProductDto>> CreateProductAsync(
        AdminProductRequest request, IAdminService admin, CancellationToken ct)
    {
        var product = await admin.CreateProductAsync(request, ct);
        return TypedResults.Created($"/api/admin/products/{product.Id}", product);
    }

    private static async Task<Results<Ok<AdminProductDto>, NotFound>> UpdateProductAsync(
        int id, AdminProductRequest request, IAdminService admin, CancellationToken ct)
    {
        var product = await admin.UpdateProductAsync(id, request, ct);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteProductAsync(
        int id, IAdminService admin, CancellationToken ct) =>
        await admin.DeleteProductAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(
        IAdminService admin, CancellationToken ct) =>
        admin.GetCategoriesAsync(ct);

    private static async Task<Created<AdminCategoryDto>> CreateCategoryAsync(
        AdminCategoryRequest request, IAdminService admin, CancellationToken ct)
    {
        var category = await admin.CreateCategoryAsync(request, ct);
        return TypedResults.Created($"/api/admin/categories/{category.Id}", category);
    }

    private static async Task<Results<Ok<AdminCategoryDto>, NotFound>> UpdateCategoryAsync(
        int id, AdminCategoryRequest request, IAdminService admin, CancellationToken ct)
    {
        var category = await admin.UpdateCategoryAsync(id, request, ct);
        return category is null ? TypedResults.NotFound() : TypedResults.Ok(category);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteCategoryAsync(
        int id, IAdminService admin, CancellationToken ct) =>
        await admin.DeleteCategoryAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static Task<Core.Common.PagedResult<AdminOrderDto>> GetOrdersAsync(
        int? page, int? pageSize, string? status, IAdminService admin, CancellationToken ct) =>
        admin.GetOrdersAsync(page, pageSize, status, ct);

    private static async Task<Results<Ok<AdminOrderDto>, NotFound>> UpdateOrderStatusAsync(
        int id, UpdateOrderStatusRequest request, IAdminService admin, CancellationToken ct)
    {
        var order = await admin.UpdateOrderStatusAsync(id, request.Status, ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    private static Task<AdminDashboardDto> GetDashboardAsync(IAdminService admin, CancellationToken ct) =>
        admin.GetDashboardAsync(ct);
}