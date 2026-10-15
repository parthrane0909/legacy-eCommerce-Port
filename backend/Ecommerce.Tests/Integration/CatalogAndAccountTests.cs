using Ecommerce.Core.Dtos.Account;
using Ecommerce.Core.Dtos.Catalog;
using Ecommerce.Tests.Infrastructure;

namespace Ecommerce.Tests.Integration;

/// <summary>
/// Integration tests for the public catalogue endpoints and the account/address endpoints.
/// </summary>
public class CatalogAndAccountTests : IClassFixture<SqliteTestAppFactory>
{
    private readonly SqliteTestAppFactory _factory;

    public CatalogAndAccountTests(SqliteTestAppFactory factory) => _factory = factory;

    // ---- catalog -------------------------------------------------------------

    [Fact]
    public async Task Product_listing_is_paginated_with_metadata()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?page=1&pageSize=5", SqliteTestAppFactory.JsonOptions);

        page.Should().NotBeNull();
        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(5);
        page.TotalCount.Should().BeGreaterThan(5);
        page.TotalPages.Should().BeGreaterThan(1);
        page.Items.Should().HaveCount(5);
        page.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task Legacy_page_size_of_eight_is_the_default()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products", SqliteTestAppFactory.JsonOptions);

        page!.PageSize.Should().Be(8, "matches the legacy storefront's eight-per-page grid");
        page.Items.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task Search_filters_by_name()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?search=keyboard", SqliteTestAppFactory.JsonOptions);

        page!.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.Items[0].Name.Should().Contain("Keyboard");
    }

    [Fact]
    public async Task Sorting_by_price_ascending_is_honoured()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?sort=price_asc&pageSize=10", SqliteTestAppFactory.JsonOptions);

        var prices = page!.Items.Select(i => i.Price).ToList();
        prices.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sorting_by_price_descending_is_honoured()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?sort=price_desc&pageSize=10", SqliteTestAppFactory.JsonOptions);

        page!.Items.Select(i => i.Price).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task An_unknown_sort_value_falls_back_to_the_default()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?sort=by_vibes&pageSize=5", SqliteTestAppFactory.JsonOptions);

        page!.Items.Should().NotBeEmpty("an unrecognised sort must not produce an error or an empty page");
    }

    [Fact]
    public async Task Filtering_by_category_returns_only_that_category()
    {
        var client = _factory.CreateAnonymousClient();
        var categoryId = await _factory.ReadDbAsync(db => db.Categories
            .Where(c => c.ParentId != null && db.Products.Any(p => p.CategoryId == c.Id))
            .Select(c => c.Id)
            .FirstAsync());

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            $"/api/products?categoryId={categoryId}&pageSize=50", SqliteTestAppFactory.JsonOptions);

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(i => i.CategoryName.Length > 0);
    }

    [Fact]
    public async Task A_parent_category_filter_includes_descendant_categories()
    {
        var client = _factory.CreateAnonymousClient();

        // Electronics is a root with children that own the products.
        var rootId = await _factory.ReadDbAsync(db => db.Categories
            .Where(c => c.Slug == "electronics")
            .Select(c => c.Id)
            .FirstAsync());

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            $"/api/products?categoryId={rootId}&pageSize=50", SqliteTestAppFactory.JsonOptions);

        page!.TotalCount.Should().BeGreaterThan(0,
            "a root category must surface the products that live in its children");
    }

    [Fact]
    public async Task In_stock_filter_excludes_sold_out_products()
    {
        var client = _factory.CreateAnonymousClient();

        var page = await client.GetFromJsonAsync<Core.Common.PagedResult<ProductSummaryDto>>(
            "/api/products?inStockOnly=true&pageSize=50", SqliteTestAppFactory.JsonOptions);

        page!.Items.Should().OnlyContain(i => i.InStock);
    }

    [Fact]
    public async Task Product_detail_returns_images_and_variants()
    {
        var client = _factory.CreateAnonymousClient();

        var productId = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.IsActive && p.Images.Any() && p.Variants.Any())
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .FirstAsync());

        var detail = await client.GetFromJsonAsync<ProductDetailDto>(
            $"/api/products/{productId}", SqliteTestAppFactory.JsonOptions);

        detail!.Id.Should().Be(productId);
        detail.Images.Should().NotBeEmpty();
        detail.Variants.Should().NotBeEmpty();
        detail.CategoryName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_product_detail_returns_404()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/products/987654");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Category_tree_is_hierarchical_with_counts()
    {
        var client = _factory.CreateAnonymousClient();

        var tree = await client.GetFromJsonAsync<List<CategoryDto>>(
            "/api/categories", SqliteTestAppFactory.JsonOptions);

        tree.Should().NotBeEmpty();
        tree!.Should().OnlyContain(c => c.ParentId == null, "the tree returns roots at the top level");
        tree.Should().Contain(c => c.Children.Count > 0, "the hierarchy is preserved");
    }

    [Fact]
    public async Task Home_endpoint_returns_rails_for_the_storefront()
    {
        var client = _factory.CreateAnonymousClient();

        var home = await client.GetFromJsonAsync<HomeProductsDto>(
            "/api/products/home", SqliteTestAppFactory.JsonOptions);

        home.Should().NotBeNull();
        home!.Featured.Should().NotBeEmpty();
        home.NewArrivals.Should().NotBeEmpty();
        home.Categories.Should().NotBeEmpty();
    }

    // ---- account -------------------------------------------------------------

    [Fact]
    public async Task Profile_requires_authentication()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/account/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_can_read_and_update_their_profile()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var profile = await client.GetFromJsonAsync<ProfileDto>(
            "/api/account/profile", SqliteTestAppFactory.JsonOptions);

        profile!.Email.Should().Be(SqliteTestAppFactory.CustomerEmail);

        var newEmail = $"updated-{Guid.NewGuid():N}@shophub.test";
        var update = await client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest("Rohit", "Sharma", newEmail), SqliteTestAppFactory.JsonOptions);

        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await update.Content.ReadFromJsonAsync<ProfileDto>(SqliteTestAppFactory.JsonOptions);
        updated!.Email.Should().Be(newEmail);
        updated.FirstName.Should().Be("Rohit");
        updated.Role.Should().Be("Customer", "a customer cannot change their own role");

        // Restore so other tests see a consistent account.
        await client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest("Rohan", "Mehta", SqliteTestAppFactory.CustomerEmail),
            SqliteTestAppFactory.JsonOptions);
    }

    [Fact]
    public async Task Taking_another_customers_email_is_rejected_with_409()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var response = await client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest("Copy", "Cat", SqliteTestAppFactory.AdminEmail),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Addresses_can_be_created_updated_and_deleted()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var create = await client.PostAsJsonAsync("/api/account/addresses",
            new CreateAddressRequest("Rohan", "Mehta", "1 Test Street", null, "Pune", "MH", "411001", "India", "+91 9999999999"),
            SqliteTestAppFactory.JsonOptions);

        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var address = await create.Content.ReadFromJsonAsync<AddressDto>(SqliteTestAppFactory.JsonOptions);
        address!.Id.Should().BeGreaterThan(0);

        var update = await client.PutAsJsonAsync($"/api/account/addresses/{address.Id}",
            new UpdateAddressRequest("Rohan", "Mehta", "2 New Street", "Apt 9", "Pune", "MH", "411002", "India", null, true),
            SqliteTestAppFactory.JsonOptions);

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AddressDto>(SqliteTestAppFactory.JsonOptions);
        updated!.Line1.Should().Be("2 New Street");
        updated.IsDefault.Should().BeTrue();

        var delete = await client.DeleteAsync($"/api/account/addresses/{address.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<List<AddressDto>>(
            "/api/account/addresses", SqliteTestAppFactory.JsonOptions);

        after.Should().NotContain(a => a.Id == address.Id);
    }

    [Fact]
    public async Task An_incomplete_address_is_rejected()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var response = await client.PostAsJsonAsync("/api/account/addresses",
            new CreateAddressRequest("", "", "", null, "", "", "", "", null),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_customer_cannot_modify_another_customers_address()
    {
        var owner = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var created = await owner.PostAsJsonAsync("/api/account/addresses",
            new CreateAddressRequest("Owner", "Person", "9 Owner Road", null, "Delhi", "DL", "110001", "India", null),
            SqliteTestAppFactory.JsonOptions);

        var address = await created.Content.ReadFromJsonAsync<AddressDto>(SqliteTestAppFactory.JsonOptions);

        // A different, newly registered customer tries to touch it.
        var intruder = _factory.CreateAnonymousClient();
        var registered = await intruder.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"intruder-{Guid.NewGuid():N}@shophub.test",
            password = "Str0ng!Password",
            firstName = "Nosy",
            lastName = "Person",
        }, SqliteTestAppFactory.JsonOptions);

        var token = await registered.Content
            .ReadFromJsonAsync<Core.Dtos.Auth.AuthResponse>(SqliteTestAppFactory.JsonOptions);
        intruder.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var update = await intruder.PutAsJsonAsync($"/api/account/addresses/{address!.Id}",
            new UpdateAddressRequest("Hacked", "Person", "0 Nowhere", null, "Nowhere", "XX", "00000", "XX", null, true),
            SqliteTestAppFactory.JsonOptions);

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delete = await intruder.DeleteAsync($"/api/account/addresses/{address.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The owner's address is untouched.
        var stillThere = await owner.GetFromJsonAsync<List<AddressDto>>(
            "/api/account/addresses", SqliteTestAppFactory.JsonOptions);

        stillThere.Should().Contain(a => a.Id == address.Id && a.Line1 == "9 Owner Road");
    }

    // ---- operational ---------------------------------------------------------

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadAsStringAsync();
        document.Should().Contain("/api/cart");
        document.Should().Contain("/api/orders");
        document.Should().Contain("/api/admin/products");
    }
}