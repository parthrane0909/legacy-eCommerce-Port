using Ecommerce.Core.Domain;

namespace Ecommerce.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PricingCalculator"/>, the single authority for money arithmetic.
/// Covers the shipping rules inherited from the legacy price table and the discount defect fix.
/// </summary>
public class PricingCalculatorTests
{
    [Theory]
    [InlineData("Standard", 150)]
    [InlineData("Express", 350)]
    [InlineData("standard", 150)]      // Legacy rule: anything but "Express" is Standard.
    [InlineData("EXPRESS", 150)]      // Case-sensitive by design, mirroring the legacy coercion.
    [InlineData("", 150)]
    [InlineData(null, 150)]
    public void ShippingCost_maps_method_to_price(string? method, int expected) =>
        PricingCalculator.ShippingCost(method).Should().Be(expected);

    [Fact]
    public void Subtotal_multiplies_and_rounds_each_line()
    {
        var total = PricingCalculator.Subtotal([
            (4999m, 2),
            (1999m, 1),
        ]);

        total.Should().Be(11997m);
    }

    [Fact]
    public void Subtotal_rounds_half_up_to_two_places()
    {
        // 10.005 rounds away from zero, not to even.
        PricingCalculator.Subtotal([(10.005m, 1)]).Should().Be(10.01m);
    }

    [Fact]
    public void Subtotal_of_empty_input_is_zero() =>
        PricingCalculator.Subtotal([]).Should().Be(0m);

    [Fact]
    public void Subtotal_throws_on_null()
    {
        var act = () => PricingCalculator.Subtotal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Total_never_includes_negative_shipping()
    {
        PricingCalculator.Total(1000m, -50m).Should().Be(1000m);
    }

    [Fact]
    public void LineTotal_is_quantity_times_unit_price() =>
        PricingCalculator.LineTotal(249.995m, 3).Should().Be(749.99m);

    [Fact]
    public void UnitPriceFor_adds_variant_delta()
    {
        PricingCalculator.UnitPriceFor(1000m, 250m).Should().Be(1250m);
        PricingCalculator.UnitPriceFor(1000m, -100m).Should().Be(900m);
        PricingCalculator.UnitPriceFor(1000m, null).Should().Be(1000m);
    }

    /// <summary>
    /// Regression test for the legacy double-discount defect: <c>Product.Price</c> is already the
    /// selling price, so pricing must never re-apply <c>DiscountPercent</c>.
    /// </summary>
    [Fact]
    public void Price_is_not_discounted_twice()
    {
        var product = new Core.Entities.Product
        {
            Price = 4999m,
            DiscountPercent = 15,
        };

        // The legacy code did Price * 0.85 = 4249.15 here, then discounted a second time.
        PricingCalculator.CurrentUnitPrice(product, variant: null)
            .Should().Be(4999m, "Price is the authoritative selling price and is never re-discounted");

        // Was-price is presentation only: 4999 / (1 - 0.15).
        product.WasPrice.Should().Be(5881.18m);
    }

    [Fact]
    public void WasPrice_is_null_outside_the_discount_range()
    {
        new Core.Entities.Product { Price = 100m, DiscountPercent = null }.WasPrice.Should().BeNull();
        new Core.Entities.Product { Price = 100m, DiscountPercent = 0 }.WasPrice.Should().BeNull();
        new Core.Entities.Product { Price = 100m, DiscountPercent = 100 }.WasPrice.Should().BeNull();
        new Core.Entities.Product { Price = 100m, DiscountPercent = 120 }.WasPrice.Should().BeNull();
    }

    [Fact]
    public void WasPrice_reconstructs_the_pre_discount_price()
    {
        // 20% off a 80 base means the original was 100.
        new Core.Entities.Product { Price = 80m, DiscountPercent = 20 }.WasPrice.Should().Be(100m);
    }

    [Fact]
    public void CurrentUnitPrice_includes_the_variant_delta()
    {
        var product = new Core.Entities.Product { Price = 200m };
        var variant = new Core.Entities.ProductVariant { PriceDelta = 25m };

        PricingCalculator.CurrentUnitPrice(product, variant).Should().Be(225m);
    }
}