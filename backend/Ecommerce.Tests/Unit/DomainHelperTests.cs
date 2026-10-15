using Ecommerce.Core.Common;
using Ecommerce.Core.Domain;
using Ecommerce.Core.Entities;

namespace Ecommerce.Tests.Unit;

/// <summary>Unit tests for the pure domain helpers: slugs, order numbers, status transitions, password policy.</summary>
public class DomainHelperTests
{
    [Theory]
    [InlineData("Wireless Headphones Pro", "wireless-headphones-pro")]
    [InlineData("Home & Living", "home-living")]
    [InlineData("  Spaced   Out  ", "spaced-out")]
    [InlineData("Crème Brûlée", "creme-brulee")]
    [InlineData("100% Cotton", "100-cotton")]
    [InlineData("---", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SlugGenerator_produces_url_safe_slugs(string? input, string expected) =>
        SlugGenerator.Generate(input).Should().Be(expected);

    [Fact]
    public async Task EnsureUnique_appends_a_numeric_suffix()
    {
        var taken = new HashSet<string> { "shoes", "shoes-2" };

        var slug = await SlugGenerator.EnsureUnique("shoes", candidate => Task.FromResult(taken.Contains(candidate)));

        slug.Should().Be("shoes-3");
    }

    [Fact]
    public async Task EnsureUnique_returns_the_base_slug_when_free()
    {
        var slug = await SlugGenerator.EnsureUnique("boots", _ => Task.FromResult(false));

        slug.Should().Be("boots");
    }

    [Fact]
    public async Task EnsureUnique_defaults_a_blank_slug()
    {
        var slug = await SlugGenerator.EnsureUnique("   ", _ => Task.FromResult(false));

        slug.Should().Be("item");
    }

    [Fact]
    public void Order_numbers_are_unique_across_many_calls()
    {
        var numbers = Enumerable.Range(0, 2000)
            .Select(_ => OrderNumberGenerator.Generate(DateTime.UtcNow))
            .ToList();

        numbers.Distinct().Should().HaveCount(2000);
    }

    [Fact]
    public void Order_numbers_embed_the_generation_date()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var number = OrderNumberGenerator.Generate(now);

        number.Should().StartWith("ORD-20261014-");
        OrderNumberGenerator.ExtractDate(number).Should().Be(new DateOnly(2026, 10, 14));
    }

    [Fact]
    public void ExtractDate_returns_default_for_malformed_numbers()
    {
        OrderNumberGenerator.ExtractDate("garbage").Should().Be(default);
        OrderNumberGenerator.ExtractDate("").Should().Be(default);
    }

    [Theory]
    [InlineData(null, "Pending", true)]
    [InlineData("Pending", "Paid", true)]
    [InlineData("Paid", "Shipped", true)]
    [InlineData("Shipped", "Delivered", true)]
    [InlineData("Pending", "Cancelled", true)]
    [InlineData("Paid", "Cancelled", true)]
    public void Allowed_status_transitions(string? from, string to, bool expected) =>
        OrderStatus.CanTransition(from, to).Should().Be(expected);

    [Theory]
    [InlineData("Delivered", "Pending")]
    [InlineData("Shipped", "Paid")]
    [InlineData("Cancelled", "Pending")]
    [InlineData("Cancelled", "Delivered")]
    [InlineData("Pending", "Delivered")]
    [InlineData("Delivered", "Shipped")]
    public void Forbidden_status_transitions(string from, string to) =>
        OrderStatus.CanTransition(from, to).Should().BeFalse();

    [Fact]
    public void Cancelled_is_terminal() =>
        OrderStatus.CanTransition("Cancelled", "Cancelled").Should().BeFalse();

    [Theory]
    [InlineData("Str0ng!Pass", true)]
    [InlineData("Sh0pHub!Modern", true)]
    [InlineData("Sh0!A", false)]          // under the 8 character minimum
    [InlineData("Passw0rd", false)]      // meets every class but lacks a non-alphanumeric
    [InlineData("alllowercase1!", false)] // no uppercase
    [InlineData("ALLUPPERCASE1!", false)] // no lowercase
    [InlineData("NoDigitsHere!", false)]  // no digit
    [InlineData("NoSymbol12345", false)]  // no non-alphanumeric
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PasswordPolicy_enforces_complexity(string? password, bool expected) =>
        PasswordPolicy.IsValid(password).Should().Be(expected);

    [Fact]
    public void Password_policy_rejects_overlong_input() =>
        PasswordPolicy.IsValid(new string('a', PasswordPolicy.MaximumLength + 1)).Should().BeFalse();

    [Theory]
    [InlineData("Standard", true)]
    [InlineData("Express", true)]
    [InlineData("standard", false)]
    [InlineData("Next-Day", false)]
    [InlineData(null, false)]
    public void ShippingOptions_validates_methods(string? method, bool expected) =>
        ShippingOptions.IsValid(method).Should().Be(expected);

    [Fact]
    public void IsInStock_requires_active_and_positive_stock()
    {
        new Product { Stock = 5, IsActive = true }.IsInStock.Should().BeTrue();
        new Product { Stock = 0, IsActive = true }.IsInStock.Should().BeFalse();
        new Product { Stock = 5, IsActive = false }.IsInStock.Should().BeFalse();
    }

    [Fact]
    public void ProductSort_normalizes_unknown_input_to_newest()
    {
        ProductSort.Normalize("price_asc").Should().Be(ProductSort.PriceAsc);
        ProductSort.Normalize("PRICE_ASC").Should().Be(ProductSort.PriceAsc);
        ProductSort.Normalize("nonsense").Should().Be(ProductSort.Newest);
        ProductSort.Normalize(null).Should().Be(ProductSort.Newest);
    }

    [Fact]
    public void Paging_bounds_are_enforced()
    {
        Paging.NormalizePage(null).Should().Be(1);
        Paging.NormalizePage(0).Should().Be(1);
        Paging.NormalizePage(-3).Should().Be(1);
        Paging.NormalizePage(4).Should().Be(4);

        Paging.NormalizePageSize(null).Should().Be(Paging.DefaultPageSize);
        Paging.NormalizePageSize(0).Should().Be(Paging.DefaultPageSize);
        Paging.NormalizePageSize(5).Should().Be(5);
        Paging.NormalizePageSize(10_000).Should().Be(Paging.MaxPageSize);
    }
}