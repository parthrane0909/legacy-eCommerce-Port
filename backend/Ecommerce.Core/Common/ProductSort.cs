namespace Ecommerce.Core.Common;

/// <summary>Standard sort options for the product listing.</summary>
public static class ProductSort
{
    public const string Newest = "newest";
    public const string PriceAsc = "price_asc";
    public const string PriceDesc = "price_desc";
    public const string NameAsc = "name_asc";
    public const string NameDesc = "name_desc";
    public const string RatingDesc = "rating_desc";

    public static readonly string[] All =
        [Newest, PriceAsc, PriceDesc, NameAsc, NameDesc, RatingDesc];

    public static bool IsValid(string? sort) =>
        !string.IsNullOrWhiteSpace(sort) && Array.Exists(All, s => s.Equals(sort, StringComparison.OrdinalIgnoreCase));

    /// <summary>Normalises user input to a known sort key, defaulting to <see cref="Newest"/>.</summary>
    public static string Normalize(string? sort) =>
        IsValid(sort)
            ? All.First(s => s.Equals(sort, StringComparison.OrdinalIgnoreCase))
            : Newest;
}

/// <summary>Paging bounds shared by the API and the tests.</summary>
public static class Paging
{
    public const int DefaultPageSize = 8;
    public const int MaxPageSize = 60;

    public static int NormalizePage(int? page) => page is null or < 1 ? 1 : page.Value;

    public static int NormalizePageSize(int? pageSize) =>
        pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);
}