using Ecommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Property(c => c.AnonymousId).HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.CustomerId).IsUnique();
        builder.HasIndex(c => c.AnonymousId).IsUnique();

        // Exactly one owner: an authenticated cart has a CustomerId, a guest cart an AnonymousId.
        // Expressed as a boolean OR because SQL Server rejects the <> comparison form here.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Carts_SingleOwner",
                "(CustomerId IS NULL AND AnonymousId IS NOT NULL) OR (CustomerId IS NOT NULL AND AnonymousId IS NULL)");
        });

        builder.HasOne(c => c.Customer)
            .WithMany(cu => cu.Carts)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.IsGuest);
    }
}