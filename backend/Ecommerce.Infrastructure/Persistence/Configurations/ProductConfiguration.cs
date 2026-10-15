using Ecommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(220).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000);

        // Money: exact decimal, never float.
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.RatingNum).HasColumnType("decimal(3,2)");
        builder.Property(p => p.DiscountPercent).HasColumnType("int");
        builder.Property(p => p.Stock).IsRequired();
        builder.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.CategoryId, p.IsActive });
        builder.HasIndex(p => p.IsFeatured);
        builder.HasIndex(p => p.Stock);

        // WasPrice and IsInStock are computed, not columns.
        builder.Ignore(p => p.WasPrice);
        builder.Ignore(p => p.IsInStock);

        // Images and variants are owned by the product and are removed with it.
        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CartItems)
            .WithOne(c => c.Product)
            .HasForeignKey(c => c.ProductId)
            // Cascade: a cart line has no meaning without its product, and the product -> images /
            // variants cascades must not reach CartItems (SQL Server rejects two cascade paths).
            // The in-memory collection is cleared first by the service that deletes the product.
            .OnDelete(DeleteBehavior.Cascade);

        // OrderItem.ProductId is Restrict: deleting a product must not destroy order history.
        builder.HasMany(p => p.OrderItems)
            .WithOne(o => o.Product)
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}