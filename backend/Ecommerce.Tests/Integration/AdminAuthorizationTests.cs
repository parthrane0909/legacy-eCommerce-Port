using Ecommerce.Core.Dtos.Admin;
using Ecommerce.Tests.Infrastructure;

namespace Ecommerce.Tests.Integration;

/// <summary>
/// Integration tests for the administration endpoints and the server-side authorization boundary.
/// </summary>
/// <remarks>
/// Authorization is verified by calling the API directly. Hiding links in the UI is a convenience,
/// not a control, so these tests never rely on the frontend.
/// </remarks>
public class AdminAuthorizationTests : IClassFixture<SqliteTestAppFactory>
{
    private readonly SqliteTestAppFactory _factory;

    public AdminAuthorizationTests(SqliteTestAppFactory factory) => _factory = factory;

    private Task<HttpClient> AdminClientAsync() =>
        _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.AdminEmail, SqliteTestAppFactory.AdminPassword);

    private Task<HttpClient> CustomerClientAsync() =>
        _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

    // ---- authorization boundary ----------------------------------------------

    [Theory]
    [InlineData("GET", "/api/admin/products")]
    [InlineData("POST", "/api/admin/products")]
    [InlineData("PUT", "/api/admin/products/1")]
    [InlineData("DELETE", "/api/admin/products/1")]
    [InlineData("GET", "/api/admin/categories")]
    [InlineData("POST", "/api/admin/categories")]
    [InlineData("PUT", "/api/admin/categories/1")]
    [InlineData("DELETE", "/api/admin/categories/1")]
    [InlineData("GET", "/api/admin/orders")]
    [InlineData("PUT", "/api/admin/orders/1/status")]
    [InlineData("GET", "/api/admin/dashboard")]
    public async Task Anonymous_callers_are_rejected_from_every_admin_endpoint(string method, string path)
    {
        var client = _factory.CreateAnonymousClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonBody(new AdminProductRequest("X", null, null, 1m, 1, null, null, null, false, true, 1)),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/admin/products")]
    [InlineData("POST", "/api/admin/products")]
    [InlineData("PUT", "/api/admin/products/1")]
    [InlineData("DELETE", "/api/admin/products/1")]
    [InlineData("GET", "/api/admin/categories")]
    [InlineData("POST", "/api/admin/categories")]
    [InlineData("PUT", "/api/admin/categories/1")]
    [InlineData("DELETE", "/api/admin/categories/1")]
    [InlineData("GET", "/api/admin/orders")]
    [InlineData("PUT", "/api/admin/orders/1/status")]
    [InlineData("GET", "/api/admin/dashboard")]
    public async Task Ordinary_customers_are_forbidden_from_every_admin_endpoint(string method, string path)
    {
        var client = await CustomerClientAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonBody(new AdminProductRequest("X", null, null, 1m, 1, null, null, null, false, true, 1)),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "authorization is enforced server-side, not merely hidden in the UI");
    }

    [Fact]
    public async Task Admin_can_read_the_dashboard()
    {
        var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- product CRUD --------------------------------------------------------

    [Fact]
    public async Task Admin_can_create_a_product()
    {
        var client = await AdminClientAsync();
        var categoryId = await _factory.ReadDbAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

        var request = new AdminProductRequest(
            Name: $"Admin Created {Guid.NewGuid():N}",
            Slug: null,
            Description: "Created through the admin API.",
            Price: 1499m,
            Stock: 12,
            ImageUrl: "/images/products/placeholder.jpg",
            DiscountPercent: 10,
            RatingNum: null,
            IsFeatured: false,
            IsActive: true,
            CategoryId: categoryId);

        var response = await client.PostAsJsonAsync("/api/admin/products", request, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<AdminProductDto>(SqliteTestAppFactory.JsonOptions);
        created!.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be(request.Name);

        // The slug was generated from the name.
        created.Slug.Should().NotBeNullOrWhiteSpace();

        // And it is really in the catalogue.
        var inDb = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.Id == created.Id)
            .Select(p => new { p.Name, p.Price, p.Stock })
            .FirstAsync());

        inDb.Name.Should().Be(request.Name);
        inDb.Price.Should().Be(1499m);
        inDb.Stock.Should().Be(12);
    }

    [Fact]
    public async Task Created_product_appears_in_the_public_catalogue()
    {
        var admin = await AdminClientAsync();
        var categoryId = await _factory.ReadDbAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

        var name = $"Visible Product {Guid.NewGuid():N}";
        var created = await admin.PostAsJsonAsync("/api/admin/products",
            new AdminProductRequest(name, null, "On sale now.", 999m, 5, "/images/products/placeholder.jpg",
                null, null, false, true, categoryId),
            SqliteTestAppFactory.JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await created.Content.ReadFromJsonAsync<AdminProductDto>(SqliteTestAppFactory.JsonOptions);

        var publicApi = _factory.CreateAnonymousClient();
        var detail = await publicApi.GetFromJsonAsync<Core.Dtos.Catalog.ProductDetailDto>(
            $"/api/products/{product!.Id}", SqliteTestAppFactory.JsonOptions);

        detail.Should().NotBeNull("admin changes are reflected in the real catalogue");
        detail!.Name.Should().Be(name);
    }

    [Fact]
    public async Task Admin_can_update_a_product()
    {
        var admin = await AdminClientAsync();
        var productId = await SeedProductAsync("Update Me");

        var response = await admin.PutAsJsonAsync($"/api/admin/products/{productId}",
            new AdminProductRequest("Updated Name", null, "Updated description.", 2222m, 7, null,
                null, null, true, true, await _factory.ReadDbAsync(db => db.Products
                    .Where(p => p.Id == productId).Select(p => p.CategoryId).FirstAsync())),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var inDb = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.Name, p.Price, p.Stock, p.IsFeatured })
            .FirstAsync());

        inDb.Name.Should().Be("Updated Name");
        inDb.Price.Should().Be(2222m);
        inDb.Stock.Should().Be(7);
        inDb.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public async Task Updating_an_unknown_product_returns_404()
    {
        var admin = await AdminClientAsync();
        var categoryId = await _factory.ReadDbAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

        var response = await admin.PutAsJsonAsync("/api/admin/products/987654",
            new AdminProductRequest("Ghost", null, null, 1m, 1, null, null, null, false, true, categoryId),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_a_product_returns_204_and_removes_it()
    {
        var admin = await AdminClientAsync();
        var productId = await SeedProductAsync("Delete Me");

        var response = await admin.DeleteAsync($"/api/admin/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillThere = await _factory.ReadDbAsync(db => db.Products.AnyAsync(p => p.Id == productId));
        stillThere.Should().BeFalse();
    }

    [Fact]
    public async Task A_product_referenced_by_an_order_is_archived_not_deleted()
    {
        var admin = await AdminClientAsync();
        var productId = await SeedProductAsync("Referenced Product");

        // Attach a synthetic order item so the product counts as referenced.
        await _factory.WithDbAsync(db =>
        {
            var customer = db.Customers.First(c => c.Email == SqliteTestAppFactory.CustomerEmail);
            var product = db.Products.First(p => p.Id == productId);

            var order = new Core.Entities.Order
            {
                OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}",
                CustomerId = customer.Id,
                Status = Core.Entities.OrderStatus.Pending,
                Subtotal = product.Price,
                ShippingCost = 0,
                Total = product.Price,
            };

            order.Items.Add(new Core.Entities.OrderItem
            {
                Order = order,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                UnitPrice = product.Price,
            });

            db.Orders.Add(order);
            db.SaveChanges();
            return Task.CompletedTask;
        });

        var response = await admin.DeleteAsync($"/api/admin/products/{productId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var product = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.IsActive })
            .FirstAsync());

        product.IsActive.Should().BeFalse("the row is kept so order history stays readable");
    }

    [Fact]
    public async Task Creating_a_product_with_an_unknown_category_returns_404()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/products",
            new AdminProductRequest("Orphan", null, null, 10m, 1, null, null, null, false, true, 987654),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_product_with_a_negative_price_is_rejected()
    {
        var admin = await AdminClientAsync();
        var categoryId = await _factory.ReadDbAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

        var response = await admin.PostAsJsonAsync("/api/admin/products",
            new AdminProductRequest("Bad Price", null, null, -5m, 1, null, null, null, false, true, categoryId),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- category CRUD -------------------------------------------------------

    [Fact]
    public async Task Admin_can_create_update_and_delete_a_category()
    {
        var admin = await AdminClientAsync();

        var create = await admin.PostAsJsonAsync("/api/admin/categories",
            new AdminCategoryRequest($"Test Category {Guid.NewGuid():N}", null, "Created by tests", true, 99, null),
            SqliteTestAppFactory.JsonOptions);

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await create.Content.ReadFromJsonAsync<AdminCategoryDto>(SqliteTestAppFactory.JsonOptions);

        var update = await admin.PutAsJsonAsync($"/api/admin/categories/{category!.Id}",
            new AdminCategoryRequest("Renamed Category", null, "Updated", false, 5, null),
            SqliteTestAppFactory.JsonOptions);

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AdminCategoryDto>(SqliteTestAppFactory.JsonOptions);
        updated!.Name.Should().Be("Renamed Category");
        updated.IsActive.Should().BeFalse();

        var delete = await admin.DeleteAsync($"/api/admin/categories/{category.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_category_containing_products_cannot_be_deleted()
    {
        var admin = await AdminClientAsync();

        var categoryId = await _factory.ReadDbAsync(db => db.Categories
            .Where(c => db.Products.Any(p => p.CategoryId == c.Id))
            .Select(c => c.Id)
            .FirstAsync());

        var response = await admin.DeleteAsync($"/api/admin/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_category_cannot_be_moved_under_its_own_child()
    {
        var admin = await AdminClientAsync();

        // Electronics (root) with a child category.
        var pair = await _factory.ReadDbAsync(db => db.Categories
            .Where(c => c.ParentId == null)
            .SelectMany(c => db.Categories.Where(k => k.ParentId == c.Id))
            .Select(c => new { Root = c.ParentId!.Value, Child = c.Id })
            .FirstAsync());

        var rootId = pair.Root;
        var childId = pair.Child;

        var response = await admin.PutAsJsonAsync($"/api/admin/categories/{rootId}",
            new AdminCategoryRequest("Cycle Attempt", null, null, true, 1, childId),
            SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a cycle in the category tree would make the tree unrenderable");
    }

    // ---- orders --------------------------------------------------------------

    [Fact]
    public async Task Admin_can_list_all_orders()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_can_advance_an_order_status()
    {
        var admin = await AdminClientAsync();

        var orderId = await _factory.WithDbAsync(db =>
        {
            var customer = db.Customers.First(c => c.Email == SqliteTestAppFactory.CustomerEmail);
            var order = new Core.Entities.Order
            {
                OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}",
                CustomerId = customer.Id,
                Status = Core.Entities.OrderStatus.Pending,
                Subtotal = 100m,
                ShippingCost = 150m,
                Total = 250m,
            };

            db.Orders.Add(order);
            db.SaveChanges();
            return Task.FromResult(order.Id);
        });

        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Paid"), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await _factory.ReadDbAsync(db => db.Orders
            .Where(o => o.Id == orderId).Select(o => o.Status).FirstAsync());

        status.Should().Be("Paid");
    }

    [Fact]
    public async Task An_invalid_status_transition_is_rejected()
    {
        var admin = await AdminClientAsync();

        var orderId = await _factory.WithDbAsync(db =>
        {
            var customer = db.Customers.First(c => c.Email == SqliteTestAppFactory.CustomerEmail);
            var order = new Core.Entities.Order
            {
                OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}",
                CustomerId = customer.Id,
                Status = Core.Entities.OrderStatus.Pending,
                Subtotal = 100m,
                ShippingCost = 0m,
                Total = 100m,
            };

            db.Orders.Add(order);
            db.SaveChanges();
            return Task.FromResult(order.Id);
        });

        // Pending -> Delivered skips a step.
        var response = await admin.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered"), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_unknown_status_value_is_rejected()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/orders/1/status",
            new UpdateOrderStatusRequest("Teleported"), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<int> SeedProductAsync(string name) =>
        await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = $"{name} {Guid.NewGuid():N}",
                Slug = $"seed-{Guid.NewGuid():N}",
                Description = name,
                Price = 500m,
                Stock = 20,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();
            return Task.FromResult(product.Id);
        });

    private static StringContent JsonBody(object payload) =>
        new(System.Text.Json.JsonSerializer.Serialize(payload, SqliteTestAppFactory.JsonOptions),
            System.Text.Encoding.UTF8, "application/json");
}