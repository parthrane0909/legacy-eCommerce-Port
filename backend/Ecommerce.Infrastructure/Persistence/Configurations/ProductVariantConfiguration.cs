using Ecommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).ValueGeneratedOnAdd();
        builder.Property(v => v.Name).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Sku).HasMaxLength(60);
        builder.Property(v => v.PriceDelta).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(v => v.Stock).IsRequired();

        builder.HasIndex(v => v.ProductId);
        builder.HasIndex(v => v.Sku).IsUnique().HasFilter(null);
    }
}