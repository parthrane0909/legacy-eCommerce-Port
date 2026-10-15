using Ecommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).ValueGeneratedOnAdd();
        builder.Property(o => o.OrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(o => o.IdempotencyKey).HasMaxLength(100);
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.ShippingMethod).HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentMethod).HasMaxLength(30).IsRequired();
        builder.Property(o => o.OrderDate).IsRequired();

        builder.Property(o => o.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.ShippingCost).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.Total).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(o => o.ShipFirstName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShipLastName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShipLine1).HasMaxLength(300).IsRequired();
        builder.Property(o => o.ShipLine2).HasMaxLength(300);
        builder.Property(o => o.ShipCity).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShipState).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShipPostalCode).HasMaxLength(20).IsRequired();
        builder.Property(o => o.ShipCountry).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShipPhone).HasMaxLength(50);

        builder.Property(o => o.BillFirstName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.BillLastName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.BillLine1).HasMaxLength(300).IsRequired();
        builder.Property(o => o.BillLine2).HasMaxLength(300);
        builder.Property(o => o.BillCity).HasMaxLength(100).IsRequired();
        builder.Property(o => o.BillState).HasMaxLength(100).IsRequired();
        builder.Property(o => o.BillPostalCode).HasMaxLength(20).IsRequired();
        builder.Property(o => o.BillCountry).HasMaxLength(100).IsRequired();

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.IdempotencyKey).IsUnique().HasFilter(null);
        builder.HasIndex(o => new { o.CustomerId, o.OrderDate });
        builder.HasIndex(o => o.Status);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_NonNegativeAmounts",
                "[Subtotal] >= 0 AND [ShippingCost] >= 0 AND [Total] >= 0");
        });

        // Address references are optional links; the snapshot columns above are the order's
        // source of truth, so deleting an address nulls the link but never corrupts the order.
        builder.HasOne(o => o.ShippingAddress)
            .WithMany(a => a.ShippingOrders)
            .HasForeignKey(o => o.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.BillingAddress)
            .WithMany(a => a.BillingOrders)
            .HasForeignKey(o => o.BillingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQL Server rejects SetNull on these two address FKs because it would create a second
        // cascade path (Customer -> Orders -> Addresses -> Orders). Deleting an address instead
        // leaves the order's snapshot columns authoritative, so restricting is safe: the API
        // nulls the reference explicitly before any delete.
    }
}