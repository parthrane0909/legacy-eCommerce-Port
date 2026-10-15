namespace Ecommerce.Core.Entities;

/// <summary>Hierarchical product category. Self-referencing via <see cref="ParentId"/>.</summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Controls storefront visibility (legacy <c>IsActive</c>).</summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public int? ParentId { get; set; }

    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}