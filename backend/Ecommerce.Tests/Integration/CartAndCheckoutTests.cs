using Ecommerce.Core.Dtos.Cart;
using Ecommerce.Core.Dtos.Orders;
using Ecommerce.Tests.Infrastructure;

namespace Ecommerce.Tests.Integration;

/// <summary>
/// Integration tests for the cart: guest carts, stock validation, totals and the guest-to-user merge.
/// </summary>
public class CartTests : IClassFixture<SqliteTestAppFactory>
{
    private readonly SqliteTestAppFactory _factory;

    public CartTests(SqliteTestAppFactory factory) => _factory = factory;

    private async Task<(int ProductId, decimal Price, int Stock)> PickProductAsync(string slugContains = "")
    {
        var page = await _factory.ReadDbAsync(db =>
            db.Products.AsNoTracking()
                .Where(p => p.IsActive && (slugContains == "" || p.Name.Contains(slugContains)))
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Price, p.Stock })
                .FirstAsync());

        return (page.Id, page.Price, page.Stock);
    }

    [Fact]
    public async Task Anonymous_visitor_can_add_an_item_and_see_the_cart()
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        var add = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 2), SqliteTestAppFactory.JsonOptions);

        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await add.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.ItemCount.Should().Be(2);
        cart.IsGuest.Should().BeTrue();
        cart.Subtotal.Should().Be(product.Price * 2);
        cart.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Anonymous_carts_are_isolated_between_browsers()
    {
        var first = _factory.CreateAnonymousClient();
        var second = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        await first.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 1), SqliteTestAppFactory.JsonOptions);

        var otherCart = await second.GetFromJsonAsync<CartDto>("/api/cart", SqliteTestAppFactory.JsonOptions);

        otherCart!.ItemCount.Should().Be(0, "a separate browser must not see another guest's cart");
    }

    [Fact]
    public async Task Adding_the_same_product_twice_merges_into_one_line()
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 1), SqliteTestAppFactory.JsonOptions);
        var second = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 2), SqliteTestAppFactory.JsonOptions);

        var cart = await second.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);

        cart!.Items.Should().ContainSingle();
        cart.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Quantity_can_be_updated()
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 1), SqliteTestAppFactory.JsonOptions);

        var update = await client.PutAsJsonAsync($"/api/cart/items/{product.ProductId}",
            new UpdateCartItemRequest(5), SqliteTestAppFactory.JsonOptions);

        var cart = await update.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task An_item_can_be_removed()
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 1), SqliteTestAppFactory.JsonOptions);

        var remove = await client.DeleteAsync($"/api/cart/items/{product.ProductId}");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await remove.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task Cart_can_be_cleared()
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 1), SqliteTestAppFactory.JsonOptions);

        var clear = await client.DeleteAsync("/api/cart");
        clear.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await clear.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.ItemCount.Should().Be(0);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Quantity_is_clamped_to_available_stock()
    {
        var client = _factory.CreateAnonymousClient();

        var low = await _factory.ReadDbAsync(db => db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Stock > 0)
            .OrderBy(p => p.Stock)
            .Select(p => new { p.Id, p.Stock })
            .FirstAsync());

        var add = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(low.Id, 99), SqliteTestAppFactory.JsonOptions);

        var cart = await add.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);

        cart!.Items[0].Quantity.Should().Be(low.Stock,
            "a line cannot exceed the stock on hand");
    }

    [Fact]
    public async Task Out_of_stock_product_cannot_be_added()
    {
        var client = _factory.CreateAnonymousClient();

        // Insert a dedicated product so the mutation cannot disturb the shared catalogue.
        var outOfStock = await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = "Sold Out Test Product",
                Slug = $"sold-out-{Guid.NewGuid():N}",
                Description = "Created by the cart test suite.",
                Price = 250m,
                Stock = 0,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();
            return Task.FromResult(product.Id);
        });

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(outOfStock, 1), SqliteTestAppFactory.JsonOptions);

        // The line is rejected, leaving an empty but valid cart.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(999_999, 1), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task Invalid_quantity_is_rejected_by_validation(int quantity)
    {
        var client = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, quantity), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_an_absent_item_returns_404()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PutAsJsonAsync("/api/cart/items/424242",
            new UpdateCartItemRequest(1), SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cart_prices_come_from_the_catalogue_not_the_client()
    {
        var client = _factory.CreateAnonymousClient();

        // A product created for this test, so the price change cannot disturb other cases.
        var productId = await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = "Repricing Test Product",
                Slug = $"repricing-{Guid.NewGuid():N}",
                Price = 1000m,
                Stock = 25,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();
            return Task.FromResult(product.Id);
        });

        var add = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(productId, 1), SqliteTestAppFactory.JsonOptions);

        add.StatusCode.Should().Be(HttpStatusCode.OK);

        // Change the catalogue price after the item is in the cart.
        var expected = await _factory.WithDbAsync(db =>
        {
            var entity = db.Products.First(p => p.Id == productId);
            entity.Price += 500m;
            db.SaveChanges();
            return Task.FromResult(entity.Price);
        });

        var update = await client.PutAsJsonAsync($"/api/cart/items/{productId}",
            new UpdateCartItemRequest(1), SqliteTestAppFactory.JsonOptions);

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await update.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);

        cart!.Items.Should().ContainSingle();
        cart.Items[0].UnitPrice.Should().Be(expected,
            "the cart re-prices from the catalogue so a stale price cannot survive");
    }

    // ---- guest cart merge ----------------------------------------------------

    [Fact]
    public async Task Guest_cart_merges_into_the_authenticated_cart_on_login()
    {
        var guest = _factory.CreateAnonymousClient();
        var product = await PickProductAsync();

        var added = await guest.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(product.ProductId, 2), SqliteTestAppFactory.JsonOptions);

        var anonymousId = GuestCartId(added);

        // Log in with a fresh session, then merge the guest cart explicitly.
        var authed = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var merge = await authed.PostAsJsonAsync("/api/cart/merge",
            new { anonymousId }, SqliteTestAppFactory.JsonOptions);

        merge.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await merge.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);
        cart!.ItemCount.Should().Be(2);
        cart.IsGuest.Should().BeFalse();
    }

    [Fact]
    public async Task Merge_combines_quantities_for_the_same_product()
    {
        // A product created for this test, so the two carts can be made to agree on it exactly.
        var productId = await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = "Merge Test Product",
                Slug = $"merge-product-{Guid.NewGuid():N}",
                Price = 500m,
                Stock = 40,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();
            return Task.FromResult(product.Id);
        });

        // Pre-existing line in the customer's own cart.
        await _factory.WithDbAsync(db =>
        {
            var customer = db.Customers.First(c => c.Email == SqliteTestAppFactory.CustomerEmail);
            var product = db.Products.First(p => p.Id == productId);

            var existing = db.Carts.FirstOrDefault(c => c.CustomerId == customer.Id);
            if (existing is null)
            {
                existing = new Core.Entities.Cart { CustomerId = customer.Id };
                db.Carts.Add(existing);
            }

            existing.Items.Add(new Core.Entities.CartItem
            {
                Cart = existing,
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = 1,
            });

            db.SaveChanges();
            return Task.CompletedTask;
        });

        var guest = _factory.CreateAnonymousClient();

        var added = await guest.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequest(productId, 2), SqliteTestAppFactory.JsonOptions);

        var anonymousId = GuestCartId(added);

        // Snapshot only the customer's own cart; the guest cart is merged in by the request below.
        var preExisting = await _factory.ReadDbAsync(db => db.Carts
            .Include(c => c.Items)
            .Where(c => c.CustomerId != null)
            .SelectMany(c => c.Items)
            .Where(i => i.ProductId == productId)
            .SumAsync(i => i.Quantity));

        var authed = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var merge = await authed.PostAsJsonAsync("/api/cart/merge",
            new { anonymousId }, SqliteTestAppFactory.JsonOptions);

        var cart = await merge.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);

        // The customer's cart may already hold unrelated lines from other tests in this class,
// so assert on the merged line specifically.
        var merged = cart!.Items.Should().ContainSingle(i => i.ProductId == productId).Subject;
        merged.Quantity.Should().Be(preExisting + 2, "existing line + 2 guest units");

        preExisting.Should().BeGreaterThan(0, "the customer's pre-existing line was persisted before the merge");
    }

    /// <summary>
    /// A guest line whose product has been deactivated since it was added must be dropped, and the
    /// shopper must be told. (Deactivation, rather than deletion, because deleting a product
    /// cascades its cart lines away - which is correct behaviour but would hide the merge path.)
    /// </summary>
    [Fact]
    public async Task Merge_drops_items_whose_product_is_no_longer_available()
    {
        var guestCartId = $"merge-unavailable-{Guid.NewGuid():N}";

        var productId = await _factory.WithDbAsync(db =>
        {
            var categoryId = db.Categories.Select(c => c.Id).First();

            var product = new Core.Entities.Product
            {
                Name = "Delisted Test Product",
                Slug = $"delisted-{Guid.NewGuid():N}",
                Price = 400m,
                Stock = 5,
                ImageUrl = "/images/products/placeholder.jpg",
                IsActive = true,
                CategoryId = categoryId,
            };

            db.Products.Add(product);
            db.SaveChanges();

            var guestCart = new Core.Entities.Cart { AnonymousId = guestCartId };
            guestCart.Items.Add(new Core.Entities.CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = 2,
            });

            db.Carts.Add(guestCart);

            // The product is delisted after the guest added it.
            product.IsActive = false;
            db.SaveChanges();

            return Task.FromResult(product.Id);
        });

        var authed = await _factory.CreateAuthenticatedClientAsync(
            SqliteTestAppFactory.CustomerEmail, SqliteTestAppFactory.CustomerPassword);

        var merge = await authed.PostAsJsonAsync("/api/cart/merge",
            new { anonymousId = guestCartId }, SqliteTestAppFactory.JsonOptions);

        merge.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await merge.Content.ReadFromJsonAsync<CartDto>(SqliteTestAppFactory.JsonOptions);

        cart!.Items.Should().NotContain(i => i.ProductId == productId);
        cart.Warnings.Should().NotBeEmpty(
            "the shopper is told which items could not be carried over");
    }

    [Fact]
    public async Task Merge_requires_authentication()
    {
        var guest = _factory.CreateAnonymousClient();

        var response = await guest.PostAsJsonAsync("/api/cart/merge",
            new { anonymousId = "anything" }, SqliteTestAppFactory.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Extracts the guest cart id from the response that first issued the cookie.
    /// </summary>
    /// <remarks>
    /// Later responses on the same client deliberately omit Set-Cookie, because the API only mints
    /// an id when the request does not already carry one.
    /// </remarks>
    private static string GuestCartId(HttpResponseMessage response) =>
        ExtractCookie(response, "shophub_cart")
        ?? throw new InvalidOperationException("The anonymous cart cookie was not set.");

    private static string? ExtractCookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith($"{name}=", StringComparison.Ordinal))
                ?.Split(';')[0][(name.Length + 1)..]
            : null;
}