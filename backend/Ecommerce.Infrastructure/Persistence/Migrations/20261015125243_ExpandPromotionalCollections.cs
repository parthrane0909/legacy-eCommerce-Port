using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Widens the home page's promotional collections by two products each.
    /// <para>
    /// This is a data-only migration, so EF generated an empty scaffold: the model is unchanged
    /// and the Designer file's target model matches the snapshot exactly. It mirrors the matching
    /// change in <c>SeedData.Products</c>, so a freshly seeded database and a migrated one end up
    /// in the same state.
    /// </para>
    /// <para>
    /// Every product touched here is an existing, active catalogue entry that already owns its
    /// image. Nothing is inserted, so no id, slug or image path is invented and no stock value is
    /// fabricated.
    /// </para>
    /// <para>
    /// Two products are promoted to <c>IsFeatured</c> so the Featured rail can show ten instead of
    /// eight, and two are given a <c>DiscountPercent</c> so they satisfy the existing "on sale"
    /// eligibility rule (<c>DiscountPercent is not null and &gt; 0</c>) and therefore also enter the
    /// Deals collection. The "was" price is a computed property derived from <c>Price</c> and
    /// <c>DiscountPercent</c>, so it needs no stored column and stays consistent automatically.
    /// </para>
    /// <para>
    /// Rows are addressed by <c>Slug</c>, the catalogue's stable natural key, rather than by id,
    /// because ids differ between a fresh seed and an existing database. Each statement is guarded
    /// on the row not already being in the target state, which keeps the migration a no-op once
    /// applied and safe to re-run.
    /// </para>
    /// </summary>
    public partial class ExpandPromotionalCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Featured: "Pour-Over Coffee Set" (rating 4.80) and "The Startup Playbook" (4.60).
            // Both are active and in stock, and both already appear in New Arrivals.
            migrationBuilder.Sql(
                """
                UPDATE [Products]
                SET [IsFeatured] = 1
                WHERE [Slug] IN (N'pour-over-coffee-set', N'the-startup-playbook')
                  AND [IsFeatured] = 0;
                """);

            // On sale: "Premium Hardcover Notebook" at 20% and "Home Gym Dumbbell Set" at 15%.
            // Guarded on a null discount so an already-discounted row is never re-stamped.
            migrationBuilder.Sql(
                """
                UPDATE [Products]
                SET [DiscountPercent] = 20
                WHERE [Slug] = N'premium-hardcover-notebook'
                  AND [DiscountPercent] IS NULL;

                UPDATE [Products]
                SET [DiscountPercent] = 15
                WHERE [Slug] = N'home-gym-dumbbell-set'
                  AND [DiscountPercent] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Products]
                SET [IsFeatured] = 0
                WHERE [Slug] IN (N'pour-over-coffee-set', N'the-startup-playbook');

                UPDATE [Products]
                SET [DiscountPercent] = NULL
                WHERE [Slug] = N'premium-hardcover-notebook';

                UPDATE [Products]
                SET [DiscountPercent] = NULL
                WHERE [Slug] = N'home-gym-dumbbell-set';
                """);
        }
    }
}
