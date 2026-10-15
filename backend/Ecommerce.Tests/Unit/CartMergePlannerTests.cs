using Ecommerce.Core.Domain;

namespace Ecommerce.Tests.Unit;

/// <summary>
/// Unit tests for the guest-to-user cart merge. These cover the edge cases the assignment calls out:
/// duplicate products, invalid quantities, deleted products and stock restrictions.
/// </summary>
public class CartMergePlannerTests
{
    private static MergeCandidate Candidate(
        int productId, int quantity, int stock = 10, bool exists = true, bool active = true, decimal price = 100m)
        => new(productId, null, quantity, exists, active, stock, price, $"Product {productId}");

    [Fact]
    public void Guest_items_are_added_to_an_empty_cart()
    {
        var result = CartMergePlanner.Merge([], [Candidate(1, 2), Candidate(2, 1)]);

        result.Lines.Should().HaveCount(2);
        result.HasWarnings.Should().BeFalse();
        result.Lines.Sum(l => l.Quantity).Should().Be(3);
    }

    [Fact]
    public void Duplicate_products_across_carts_are_summed()
    {
        var existing = new[] { new MergedCartLine(1, null, 2, 100m, "Product 1") };

        var result = CartMergePlanner.Merge(existing, [Candidate(1, 3)]);

        result.Lines.Should().ContainSingle();
        result.Lines[0].Quantity.Should().Be(5);
    }

    [Fact]
    public void Duplicate_merges_are_summed_and_capped_at_the_per_line_maximum()
    {
        var existing = new[] { new MergedCartLine(1, null, 50, 100m, "Product 1") };

        var result = CartMergePlanner.Merge(existing, [Candidate(1, 50, stock: 1000)]);

        result.Lines[0].Quantity.Should().Be(CartMergePlanner.MaxQuantityPerLine);
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void Guest_price_replaces_a_stale_existing_price()
    {
        var existing = new[] { new MergedCartLine(1, null, 1, 50m, "Product 1") };

        var result = CartMergePlanner.Merge(existing, [Candidate(1, 1, price: 120m)]);

        result.Lines[0].UnitPrice.Should().Be(120m);
    }

    [Fact]
    public void Deleted_products_are_dropped_with_a_warning()
    {
        var result = CartMergePlanner.Merge([], [Candidate(1, 1, exists: false)]);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain(CartMergePlanner.ProductUnavailableWarning);
    }

    [Fact]
    public void Inactive_products_are_dropped_with_a_warning()
    {
        var result = CartMergePlanner.Merge([], [Candidate(1, 1, active: false)]);

        result.Lines.Should().BeEmpty();
        result.HasWarnings.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Invalid_quantities_are_ignored(int quantity)
    {
        var result = CartMergePlanner.Merge([], [Candidate(1, quantity)]);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain(CartMergePlanner.InvalidQuantityWarning);
    }

    [Fact]
    public void Quantity_is_clamped_to_available_stock()
    {
        var result = CartMergePlanner.Merge([], [Candidate(1, 10, stock: 3)]);

        result.Lines.Should().ContainSingle();
        result.Lines[0].Quantity.Should().Be(3);
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain(CartMergePlanner.StockLimitedWarning);
    }

    [Fact]
    public void Out_of_stock_products_remove_the_line_entirely()
    {
        var existing = new[] { new MergedCartLine(1, null, 2, 100m, "Product 1") };

        var result = CartMergePlanner.Merge(existing, [Candidate(1, 1, stock: 0)]);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().ContainSingle()
            .Which.Should().Contain(CartMergePlanner.OutOfStockWarning);
    }

    [Fact]
    public void Merged_lines_are_ordered_deterministically()
    {
        var result = CartMergePlanner.Merge([], [Candidate(5, 1), Candidate(2, 1), Candidate(9, 1)]);

        result.Lines.Select(l => l.ProductId).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Variants_are_kept_as_separate_lines()
    {
        var existing = new[] { new MergedCartLine(1, 100, 1, 100m, "Product 1") };

        var result = CartMergePlanner.Merge(existing,
            [new MergeCandidate(1, 200, 1, true, true, 10, 130m, "Product 1")]);

        result.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void NormalizeQuantity_clamps_into_the_allowed_range()
    {
        CartMergePlanner.NormalizeQuantity(0).Should().Be(1);
        CartMergePlanner.NormalizeQuantity(null).Should().Be(1);
        CartMergePlanner.NormalizeQuantity(-3).Should().Be(1);
        CartMergePlanner.NormalizeQuantity(5).Should().Be(5);
        CartMergePlanner.NormalizeQuantity(1000).Should().Be(CartMergePlanner.MaxQuantityPerLine);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        FluentActions.Invoking(() => CartMergePlanner.Merge(null!, [])).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => CartMergePlanner.Merge([], null!)).Should().Throw<ArgumentNullException>();
    }
}