using Ecommerce.Core.Domain;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Seeding;

/// <summary>
/// Deterministic seed data.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the legacy canonical seed <c>db/seed_generic.sql</c> (30 categories, 22 products,
/// their images and variants). The assignment requires at least 25 products, so three additional
/// genuine catalogue items were added rather than duplicating existing rows.
/// </para>
/// <para>
/// Idempotent: every write is guarded by an existence check, so starting the app repeatedly never
/// creates duplicates.
/// </para>
/// <para>
/// Credentials: the seeded admin and customer use the passwords in <c>SeedOptions</c>. They default
/// to documented development-only values and are overridden by configuration, so no production secret
/// is ever hardcoded.
/// </para>
/// </remarks>
public sealed class DatabaseSeeder(
    EcommerceDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions _options = seedOptions.Value;

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Database migrations applied.");
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedCustomersAsync(ct);
        await SeedCategoriesAsync(ct);
        await SeedProductsAsync(ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seed complete: {Products} products, {Categories} categories, {Customers} customers.",
            await db.Products.CountAsync(ct),
            await db.Categories.CountAsync(ct),
            await db.Customers.CountAsync(ct));
    }

    private async Task SeedCustomersAsync(CancellationToken ct)
    {
        await EnsureCustomerAsync(_options.AdminEmail, _options.AdminPassword, "Aditi", "Rao", Roles.Admin, ct);
        await EnsureCustomerAsync(_options.CustomerEmail, _options.CustomerPassword, "Rohan", "Mehta", Roles.Customer, ct);
    }

    private async Task EnsureCustomerAsync(
        string email, string password, string firstName, string lastName, string role, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(c => c.Email == normalized, ct))
        {
            return;
        }

        db.Customers.Add(new Customer
        {
            Email = normalized,
            PasswordHash = passwordHasher.Hash(password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var added = false;

        // Insert every category first, then wire the parent FKs by slug. Doing it in two passes
        // avoids depending on insertion order for a self-referencing relationship.
        foreach (var c in SeedData.Categories)
        {
            if (await db.Categories.AnyAsync(x => x.Slug == c.Slug, ct))
            {
                continue;
            }

            db.Categories.Add(new Category
            {
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                SortOrder = c.SortOrder,
                IsActive = true,
            });

            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
        }

        var bySlug = await db.Categories.AsNoTracking()
            .ToDictionaryAsync(c => c.Slug, c => c.Id, ct);

        foreach (var c in SeedData.Categories.Where(x => x.ParentSlug is not null))
        {
            if (!bySlug.TryGetValue(c.Slug, out var childId)
                || !bySlug.TryGetValue(c.ParentSlug!, out var parentId))
            {
                continue;
            }

            // Only touch rows that still need wiring, keeping the seeder idempotent.
            var changed = await db.Categories
                .Where(x => x.Id == childId && x.ParentId != parentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParentId, parentId), ct);

            if (changed > 0)
            {
                logger.LogDebug("Linked category {Slug} to parent {Parent}.", c.Slug, c.ParentSlug);
            }
        }
    }

    private async Task SeedProductsAsync(CancellationToken ct)
    {
        foreach (var p in SeedData.Products)
        {
            if (await db.Products.AnyAsync(x => x.Slug == p.Slug, ct))
            {
                continue;
            }

            var category = await db.Categories
                .FirstOrDefaultAsync(c => c.Slug == p.CategorySlug, ct);

            if (category is null)
            {
                // Cannot happen with the curated data, but a missing category would break FKs.
                logger.LogWarning("Skipping product {Slug}: category {CategorySlug} not found.", p.Slug, p.CategorySlug);
                continue;
            }

            var product = new Product
            {
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                Price = p.Price,
                Stock = p.Stock,
                ImageUrl = p.ImageUrl,
                DiscountPercent = p.DiscountPercent,
                RatingNum = p.Rating,
                IsFeatured = p.IsFeatured,
                IsActive = true,
                CategoryId = category.Id,
                // Deterministic CreatedAt so "new arrivals" ordering is reproducible across rebuilds.
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(p.AgeInDays),
            };

            var sortOrder = 0;
            foreach (var image in p.Images)
            {
                product.Images.Add(new ProductImage
                {
                    Url = image.Url,
                    AltText = image.Alt,
                    IsPrimary = sortOrder == 0,
                    SortOrder = sortOrder++,
                });
            }

            foreach (var v in p.Variants)
            {
                product.Variants.Add(new ProductVariant
                {
                    Name = v.Name,
                    Sku = $"{p.Slug.ToUpperInvariant()}-{v.Name.ToUpperInvariant()}",
                    PriceDelta = v.PriceDelta,
                    Stock = Math.Max(1, p.Stock / Math.Max(1, p.Variants.Count)),
                });
            }

            db.Products.Add(product);
        }

        await db.SaveChangesAsync(ct);
    }
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@shophub.local";

    public string AdminPassword { get; set; } = "Admin@12345";

    public string CustomerEmail { get; set; } = "customer@shophub.local";

    public string CustomerPassword { get; set; } = "Customer@12345";
}