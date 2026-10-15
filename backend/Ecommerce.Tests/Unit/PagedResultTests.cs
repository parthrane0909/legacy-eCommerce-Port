using Ecommerce.Core.Common;

namespace Ecommerce.Tests.Unit;

/// <summary>Unit tests for the pagination envelope returned by every list endpoint.</summary>
public class PagedResultTests
{
    [Fact]
    public void TotalPages_rounds_up()
    {
        var result = new PagedResult<string> { Page = 1, PageSize = 10, TotalCount = 25 };

        result.TotalPages.Should().Be(3);
        result.HasNext.Should().BeTrue();
        result.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void TotalPages_is_zero_for_a_non_positive_page_size() =>
        new PagedResult<string> { PageSize = 0, TotalCount = 10 }.TotalPages.Should().Be(0);

    [Fact]
    public void Exact_multiple_has_no_extra_page()
    {
        var result = new PagedResult<string> { Page = 2, PageSize = 10, TotalCount = 20 };

        result.TotalPages.Should().Be(2);
        result.HasNext.Should().BeFalse();
        result.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void Empty_result_is_well_formed()
    {
        var result = PagedResult<string>.Empty(page: 3, pageSize: 8);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.Page.Should().Be(3);
    }
}