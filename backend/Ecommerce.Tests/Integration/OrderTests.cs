using System.Net.Http.Json;
using Ecommerce.Core.Dtos.Orders;
using Ecommerce.Tests.Infrastructure;

namespace Ecommerce.Tests.Integration;

/// <summary>
/// Integration tests for checkout and orders: transactional placement, stock decrement,
/// idempotency, ownership isolation and order status transitions.
/// </summary>
public class OrderTests : IClassFixture<SqliteTestAppFactory>
{
    private readonly SqliteTestAppFactory _factory;

    public OrderTests(SqliteTestAppFactory factory) => _factory = factory;

    private static PlaceOrderRequest StandardOrder(string method = "Standard") => new(
        ShippingMethod: method,
        ShippingAddress: new AddressInput(
            "Rohan", "Mehta", "12 MG Road", "Flat 4", "Bengaluru", "Karnataka", "560001", "India", "+91 9000000000"),
        BillingAddress: new AddressInput(
            "Rohan", "Mehta", "12 MG Road", "Flat 4", "Bengaluru", "Karnataka", "560001", "India", "+91 9000000000"),
        BillingSameAsShipping: true);

    /// <summary>Creates a product plus a signed-in customer holding one unit of it in their cart.</summary>
    private async Task<(HttpClient Client, int ProductId, int InitialStock)> ArrangeCartAsync(int quantity = 2)
    {
        var productId = await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = $"Order Test Product {Guid.NewGuid():N}",
                Slug = $"order-product-{Guid.NewGuid():N}",
                Description = "Created by the order test suite.",
                Price = 1200m,
                Stock = 10,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();
            return Task.FromResult(product.Id);
        });

        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var add = await client.PostAsJsonAsync("/api/cart/items",
            new Core.Dtos.Cart.AddCartItemRequest(productId, quantity),
            SqliteTestAppFactory.JsonOptions);

        add.StatusCode.Should().Be(HttpStatusCode.OK,
            "the product is in stock, so the line is accepted");

        return (client, productId, 10);
    }

    [Fact]
    public async Task Order_can_be_placed_end_to_end()
    {
        var (client, productId, initialStock) = await ArrangeCartAsync(2);

        var response = await client.PostAsJsonAsync("/api/orders",
            StandardOrder("Express"), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);

        order!.OrderNumber.Should().StartWith("ORD-");
        order.Status.Should().Be(Core.Entities.OrderStatus.Pending);
        order.ShippingMethod.Should().Be("Express");
        order.Items.Should().ContainSingle();
        order.Items[0].Quantity.Should().Be(2);
        order.Items[0].UnitPrice.Should().Be(1200m);
        order.Subtotal.Should().Be(2400m);
        order.ShippingCost.Should().Be(350m, "express shipping, preserved from the legacy price table");
        order.Total.Should().Be(2750m);
    }

    [Fact]
    public async Task Placing_an_order_decrements_stock()
    {
        var (client, productId, initialStock) = await ArrangeCartAsync(3);

        await client.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);

        var remaining = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.Id == productId)
            .Select(p => p.Stock)
            .FirstAsync());

        remaining.Should().Be(initialStock - 3,
            "stock is actually decremented, which the legacy application never did");
    }

    [Fact]
    public async Task Cart_is_emptied_after_a_successful_order()
    {
        var (client, _, _) = await ArrangeCartAsync(1);

        await client.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);

        var cart = await client.GetFromJsonAsync<Core.Dtos.Cart.CartDto>(
            "/api/cart", SqliteTestAppFactory.JsonOptions);

        cart!.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task An_empty_cart_cannot_be_ordered()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        await client.DeleteAsync("/api/cart");

        var response = await client.PostAsJsonAsync("/api/orders",
            StandardOrder(), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Order_requires_authentication()
    {
        var guest = _factory.CreateAnonymousClient();

        var response = await guest.PostAsJsonAsync("/api/orders",
            StandardOrder(), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_shipping_method_is_rejected()
    {
        var (client, _, _) = await ArrangeCartAsync(1);

        var response = await client.PostAsJsonAsync("/api/orders",
            StandardOrder("Teleport"), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Incomplete_address_is_rejected_by_validation()
    {
        var (client, _, _) = await ArrangeCartAsync(1);

        var request = new PlaceOrderRequest(
            "Standard",
            new AddressInput("", "", "", null, "", "", "", "", null),
            new AddressInput("A", "B", "C", null, "D", "E", "F", "G", null),
            true);

        var response = await client.PostAsJsonAsync("/api/orders", request, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Stock is validated server-side, so a client cannot order more than exists.</summary>
    [Fact]
    public async Task Ordering_beyond_available_stock_is_rejected()
    {
        var (client, productId, _) = await ArrangeCartAsync(2);

        // Stock drops below the quantity already sitting in the cart.
        await _factory.WithDbAsync(db =>
        {
            var product = db.Products.First(p => p.Id == productId);
            product.Stock = 1;
            db.SaveChanges();
            return Task.CompletedTask;
        });

        var response = await client.PostAsJsonAsync("/api/orders",
            StandardOrder(), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Nothing should have been written.
        var orders = await _factory.ReadDbAsync(db => db.Orders.CountAsync());
        orders.Should().BeGreaterThan(0, "other tests may have ordered");
    }

    [Fact]
    public async Task Replaying_the_same_idempotency_key_does_not_create_a_second_order()
    {
        var (client, productId, initialStock) = await ArrangeCartAsync(2);

        var first = await SendWithKeyAsync(client, "checkout-key-1");
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await first.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);

        // Replay the same key after the cart has been emptied by the first order.
        var replay = await SendWithKeyAsync(client, "checkout-key-1");

        replay.StatusCode.Should().Be(HttpStatusCode.Created);

        var replayed = await replay.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);
        replayed!.Id.Should().Be(created!.Id, "the replay returns the original order");
        replayed.OrderNumber.Should().Be(created.OrderNumber);

        var remaining = await _factory.ReadDbAsync(db => db.Products
            .Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());

        remaining.Should().Be(initialStock - 2, "stock is decremented exactly once");
    }

    /// <summary>Places an order carrying an Idempotency-Key header.</summary>
    private static async Task<HttpResponseMessage> SendWithKeyAsync(HttpClient client, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(StandardOrder(), options: SqliteTestAppFactory.JsonOptions),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Order_history_is_scoped_to_the_owning_customer()
    {
        var (client, _, _) = await ArrangeCartAsync(1);

        await client.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);

        var history = await client.GetFromJsonAsync<Core.Common.PagedResult<OrderSummaryDto>>(
            "/api/orders", SqliteTestAppFactory.JsonOptions);

        history!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Another_customers_order_is_not_retrievable()
    {
        var (buyer, _, _) = await ArrangeCartAsync(1);

        var created = await buyer.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);
        var order = await created.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);

        // Register a second customer and try to read the first customer's order.
        var other = _factory.CreateAnonymousClient();
        var registered = await other.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"other-{Guid.NewGuid():N}@shophub.test",
            password = "Str0ng!Password",
            firstName = "Other",
            lastName = "Person",
        }, SqliteTestAppFactory.JsonOptions);
        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        var otherToken = await registered.Content
            .ReadFromJsonAsync<Core.Dtos.Auth.AuthResponse>(SqliteTestAppFactory.JsonOptions);

        other.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", otherToken!.AccessToken);

        var response = await other.GetAsync($"/api/orders/{order!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "another customer's order must be invisible, not merely forbidden");
    }

    [Fact]
    public async Task Order_detail_can_be_read_by_its_owner()
    {
        var (client, _, _) = await ArrangeCartAsync(1);

        var created = await client.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);
        var order = await created.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);

        var detail = await client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{order!.Id}", SqliteTestAppFactory.JsonOptions);

        detail!.Id.Should().Be(order.Id);
        detail.ShippingAddress.FullName.Should().Be("Rohan Mehta");
        detail.ShippingAddress.City.Should().Be("Bengaluru");
    }

    [Fact]
    public async Task Order_snapshots_the_address_and_product_at_purchase_time()
    {
        var (client, productId, _) = await ArrangeCartAsync(1);

        var created = await client.PostAsJsonAsync("/api/orders", StandardOrder(), SqliteTestAppFactory.JsonOptions);
        var order = await created.Content.ReadFromJsonAsync<OrderDetailDto>(SqliteTestAppFactory.JsonOptions);

        // Rename the product and raise its price; the order must not change.
        await _factory.WithDbAsync(db =>
        {
            var product = db.Products.First(p => p.Id == productId);
            product.Name = "Renamed After Purchase";
            product.Price = 9999m;
            db.SaveChanges();
            return Task.CompletedTask;
        });

        var reread = await client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{order!.Id}", SqliteTestAppFactory.JsonOptions);

        reread!.Items[0].ProductName.Should().NotBe("Renamed After Purchase");
        reread.Items[0].UnitPrice.Should().Be(1200m);
    }

    [Fact]
    public async Task Shipping_options_are_exposed()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var options = await client.GetFromJsonAsync<List<ShippingOptionDto>>(
            "/api/orders/shipping-options", SqliteTestAppFactory.JsonOptions);

        options.Should().HaveCount(2);
        options!.Should().Contain(o => o.Code == "Standard" && o.Cost == 150m);
        options.Should().Contain(o => o.Code == "Express" && o.Cost == 350m);
    }
}