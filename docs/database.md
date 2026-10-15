# Database

Target: **SQL Server 2022** (Developer or Express edition; LocalDB also works).

Database name: **`ShopHubModernDb`** — entirely separate from the legacy `LegacyEcommerceDb`.
The legacy database is never read, written, migrated, or referenced by this project.

---

## 1. Schema overview

11 tables: the 8 required entities, 2 justified supporting entities, and the EF migration ledger.

```
Categories ──self-ref──┐
      │                │
      │ 1:N            │ 1:N
      ▼                │
   Products ◄──────────┘
      │  1:N        │  1:N        │  1:N
      │             │             │
      ▼             ▼             ▼
ProductImages  ProductVariants  OrderItems ──► Products (Restrict)
                                     │
CartItems ──► Products               │
   │                                │
   ▼                                │
Carts ──► Customers ◄───────────────┘
   │           │  1:N
   │           ├──────────► Addresses ◄──── Orders (ShippingAddressId, BillingAddressId)
   │           ├──────────► Carts
   │           ├──────────► Orders
   │           └──────────► RefreshTokens
   │
   └─ exactly one of CustomerId / AnonymousId   (CHECK constraint)
```

| Table | Purpose | Required by assignment |
|---|---|---|
| `Categories` | Hierarchical catalogue taxonomy | ✔ |
| `Products` | Sellable items | ✔ |
| `Customers` | Accounts, roles, password hashes | ✔ |
| `Addresses` | Saved shipping/billing addresses | ✔ |
| `Carts` | Guest and authenticated carts | ✔ |
| `CartItems` | Cart lines with price snapshots | ✔ |
| `Orders` | Placed orders with address snapshots | ✔ |
| `OrderItems` | Order lines with purchase-time snapshots | ✔ |
| `ProductImages` | Product gallery | Supporting |
| `ProductVariants` | Purchasable options (size/colour) | Supporting |
| `RefreshTokens` | Hashed refresh tokens for rotation | Supporting |

---

## 2. The eight required entities

### Category

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `Name` | `nvarchar(100)` | required |
| `Slug` | `nvarchar(140)` | **unique index** |
| `Description` | `nvarchar(500)` | |
| `IsActive` | `bit` | Storefront visibility |
| `SortOrder` | `int` | |
| `ParentId` | `int` FK → `Categories` | **self-referencing**, `ON DELETE RESTRICT` |

`ParentId IS NULL` marks a root. `RESTRICT` means a parent with children cannot be deleted — the
API returns `409` instead, so the caller gets a useful error rather than an FK violation.

Indexes: `Slug` (unique), `ParentId`, `(IsActive, SortOrder)`.

### Product

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `Name` | `nvarchar(200)` | required |
| `Slug` | `nvarchar(220)` | **unique index** |
| `Description` | `nvarchar(4000)` | |
| `Price` | `decimal(18,2)` | **authoritative selling price** |
| `Stock` | `int` | |
| `ImageUrl` | `nvarchar(500)` | required |
| `DiscountPercent` | `int?` | presentation only |
| `RatingNum` | `decimal(3,2)?` | |
| `IsFeatured` | `bit` | |
| `IsActive` | `bit` | |
| `CreatedAt` | `datetime2` | |
| `CategoryId` | `int` FK → `Categories` | `ON DELETE RESTRICT` |

Indexes: `Slug` (unique), `(CategoryId, IsActive)`, `IsFeatured`, `Stock`.

`WasPrice` and `IsInStock` are **computed in C#**, not stored — they are derived and would
otherwise risk drifting from their inputs.

### Customer

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `Email` | `nvarchar(256)` | **unique index** |
| `PasswordHash` | `nvarchar(512)` | Identity `PasswordHasher` output, never plaintext |
| `FirstName` | `nvarchar(100)` | |
| `LastName` | `nvarchar(100)` | |
| `Role` | `nvarchar(20)` | `Customer` or `Admin` |
| `CreatedAt` | `datetime2` | |

`FullName` is computed in C#. `Role` is written server-side only; there is no request contract
that can set it.

### Address

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `CustomerId` | `int` FK → `Customers` | `ON DELETE CASCADE` |
| `FirstName`, `LastName` | `nvarchar(100)` | |
| `Line1` | `nvarchar(300)` | required |
| `Line2` | `nvarchar(300)?` | |
| `City`, `State` | `nvarchar(100)` | required |
| `PostalCode` | `nvarchar(20)` | required |
| `Country` | `nvarchar(100)` | required |
| `Phone` | `nvarchar(50)?` | Preserved from the legacy checkout form |
| `IsDefault` | `bit` | |

### Cart and CartItem

`Cart`:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `CustomerId` | `int?` FK → `Customers` | **unique index** |
| `AnonymousId` | `nvarchar(64)?` | **unique index** |
| `CreatedAt`, `UpdatedAt` | `datetime2` | |

```sql
CONSTRAINT CK_Carts_SingleOwner CHECK (
    (CustomerId IS NULL AND AnonymousId IS NOT NULL)
 OR (CustomerId IS NOT NULL AND AnonymousId IS NULL))
```

Exactly one owner. This is the structural replacement for the ASP.NET Session cart.

> The constraint is written as a boolean `OR` rather than `XOR (<>)`. SQL Server rejects `<>` here,
> and the boolean form is portable to SQLite, which the integration tests use.

`CartItem`:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `CartId` | `int` FK → `Carts` | `ON DELETE CASCADE` |
| `ProductId` | `int` FK → `Products` | `ON DELETE CASCADE` |
| `ProductVariantId` | `int?` FK → `ProductVariants` | `ON DELETE RESTRICT` |
| `Quantity` | `int` | `CHECK (Quantity > 0)` |
| `UnitPrice` | `decimal(18,2)` | snapshot, re-validated against the catalogue |
| `ProductName` | `nvarchar(200)` | snapshot |
| `ImageUrl` | `nvarchar(500)?` | snapshot |

```sql
CONSTRAINT CK_CartItems_PositiveQuantity CHECK (Quantity > 0)
UNIQUE (CartId, ProductId, ProductVariantId)
```

The unique index is what makes "add the same product twice" merge into one line instead of
creating duplicates. A negative or zero quantity is impossible at the storage layer, not merely
rejected in application code.

### Order and OrderItem

`Order` header:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `OrderNumber` | `nvarchar(40)` | **unique index** |
| `IdempotencyKey` | `nvarchar(100)?` | **unique index** (filtered to non-null) |
| `CustomerId` | `int` FK → `Customers` | `ON DELETE RESTRICT` |
| `OrderDate` | `datetime2` | |
| `Status` | `nvarchar(20)` | `Pending`/`Paid`/`Shipped`/`Delivered`/`Cancelled` |
| `Subtotal`, `ShippingCost`, `Total` | `decimal(18,2)` | |
| `ShippingMethod` | `nvarchar(20)` | |
| `PaymentMethod` | `nvarchar(30)` | Demonstration value; no card data |
| `ShippingAddressId` | `int?` FK → `Addresses` | `ON DELETE RESTRICT` |
| `BillingAddressId` | `int?` FK → `Addresses` | `ON DELETE RESTRICT` |

Plus **full address snapshots**: `ShipFirstName` … `ShipPhone` and `BillFirstName` …
`BillCountry`.

```sql
CONSTRAINT CK_Orders_NonNegativeAmounts CHECK (
    Subtotal >= 0 AND ShippingCost >= 0 AND Total >= 0)
```

> **Why snapshots as well as foreign keys?** The snapshot columns are the order's source of truth
> for history. The optional FKs are a convenience link to the customer's saved address. Deleting an
> address nulls the reference in the service layer before the row goes, and the snapshot is
> unaffected either way — an order's address can never be silently rewritten.

`OrderItem`:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `OrderId` | `int` FK → `Orders` | `ON DELETE CASCADE` |
| `ProductId` | `int` FK → `Products` | **`ON DELETE RESTRICT`** |
| `ProductName` | `nvarchar(200)` | snapshot |
| `VariantName` | `nvarchar(100)?` | snapshot |
| `Quantity` | `int` | `CHECK (Quantity > 0)` |
| `UnitPrice` | `decimal(18,2)` | snapshot |

```sql
CONSTRAINT CK_OrderItems_PositiveQuantity CHECK (Quantity > 0)
```

`RESTRICT` on `ProductId` means a product can never be deleted out from under order history — the
admin service archives it instead.

---

## 3. Supporting entities

### ProductImage

`Id`, `ProductId` (cascade), `Url`, `AltText`, `IsPrimary`, `SortOrder`. Indexed on
`(ProductId, SortOrder)`.

### ProductVariant

`Id`, `ProductId` (cascade), `Name`, `Sku` (unique), `PriceDelta decimal(18,2)`, `Stock`.
`PriceDelta` is added to the product price, so a variant can be cheaper or dearer.

### RefreshToken

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` PK | Identity |
| `CustomerId` | `int` FK → `Customers` | cascade |
| `TokenHash` | `nvarchar(128)` | **unique index** — SHA-256 hex digest |
| `ExpiresAt`, `CreatedAt`, `RevokedAt` | `datetime2` | |
| `ReplacedByTokenHash` | `nvarchar(128)?` | for reuse detection |

Only the digest is stored. A database leak yields no usable credentials.

---

## 4. Money and precision

Every monetary column is `decimal(18,2)`. No `float` or `double` is used anywhere in the
application.

`PricingCalculator` is the single place money is computed. It rounds once, at the boundary, with
`MidpointRounding.AwayFromZero` — so `10.005` becomes `10.01`, not `10.00`.

```csharp
public static decimal Subtotal(IEnumerable<(decimal UnitPrice, int Quantity)> lines)
public static decimal LineTotal(decimal unitPrice, int quantity)
public static decimal ShippingCost(string? method)   // 150 Standard / 350 Express
public static decimal Total(decimal subtotal, decimal shippingCost)
public static decimal UnitPriceFor(decimal basePrice, decimal? variantPriceDelta)
```

---

## 5. Delete behaviour, and why

SQL Server rejects a schema with two cascade paths into the same table. Each decision below was
made to satisfy that constraint **and** to keep history intact.

| Relationship | Behaviour | Reason |
|---|---|---|
| Category → Products | `RESTRICT` | A category with products should not vanish |
| Category → Children | `RESTRICT` | Same |
| Product → Images, Variants | `CASCADE` | Owned by the product; no independent meaning |
| Cart → CartItems | `CASCADE` | Same |
| Product → CartItems | `CASCADE` | A cart line is meaningless without its product |
| ProductVariant → CartItem | `RESTRICT` | Avoids a second cascade path into `CartItems` |
| Product → OrderItems | `RESTRICT` | **Order history must survive** |
| Customer → Addresses, Carts, RefreshTokens | `CASCADE` | Purely owned by the account |
| Customer → Orders | `RESTRICT` | Order history |
| Order → OrderItems | `CASCADE` | Owned by the order |
| Order → Addresses | `RESTRICT` | See the snapshot note above |

---

## 6. Migrations

Two migrations, both code-first:

```
20261014104819_InitialCreate
20261014111931_AddIdempotencyKeyAndCartConstraints
```

### Applying them

Automatically on API start-up (`Database:AutoMigrate`, default `true`), or manually:

```bash
cd backend
export ConnectionStrings__DefaultConnection='<your connection string>'

dotnet ef database update \
  --project Ecommerce.Infrastructure \
  --startup-project Ecommerce.Api
```

### Adding one

```bash
dotnet ef migrations add <Name> \
  --project Ecommerce.Infrastructure \
  --startup-project Ecommerce.Api \
  --output-dir Persistence/Migrations
```

A design-time factory (`EcommerceDbContextFactory`) resolves the connection string from
configuration or the environment, so no password is ever committed.

### Verifying the model is in sync

```bash
dotnet ef migrations has-pending-model-changes \
  --project Ecommerce.Infrastructure \
  --startup-project Ecommerce.Api
# "No changes have been made to the model since the last migration."
```

### Reverting

```bash
dotnet ef database update 0 --project Ecommerce.Infrastructure --startup-project Ecommerce.Api
```

> **Never point these commands at the legacy database.** `dotnet ef` operates on whatever
> `ConnectionStrings:DefaultConnection` says.

---

## 7. Seed data

Runs automatically on start-up and is **idempotent** — every write is guarded by an existence
check, so restarts never duplicate rows.

| Entity | Count | Source |
|---|---|---|
| Categories | 27 | Ported from `db/seed_generic.sql` (hierarchical: 7 roots, 20 children) |
| Products | 25 | 22 from the legacy seed + 3 added to meet the assignment minimum |
| ProductImages | 50 | Two per product |
| ProductVariants | 43 | |
| Customers | 2 | 1 admin, 1 sample customer |

### Content vs. code

The seed is **content-preserving, not code-preserving**. Names, slugs, descriptions, prices,
discounts, ratings, stock levels, image associations and category hierarchy were carried over
from the legacy canonical seed. The legacy SQL procedures were not.

Three products were added because the assignment requires at least 25 and the legacy seed has 22.
They are genuine catalogue items, not duplicates:

| Product | Price |
|---|---|
| Studio Microphone USB | ₹5,499 |
| Adjustable Standing Desk | ₹18,999 |
| Trail Running Shoes | ₹6,499 |

### Categories use a two-pass insert

Categories are inserted first, then parent links are wired by slug:

```csharp
await db.SaveChangesAsync(ct);
var bySlug = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Slug, c => c.Id, ct);
// then set ParentId where the seed declares a parent
```

This avoids depending on insertion order for a self-referencing relationship.

### Seed credentials

Configured by `Seed:*`. The defaults are development-only demo credentials and are documented in
the README. **No production password or secret is seeded.**

---

## 8. Verifying the live database

```bash
# Table inventory
docker exec shophub-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<password>' -C -d ShopHubModernDb -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;"

# Row counts
docker exec shophub-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<password>' -C -d ShopHubModernDb -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT 'products='+CAST(COUNT(*) AS VARCHAR) FROM Products;"

# Constraint and index inventory
docker exec shophub-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<password>' -C -d ShopHubModernDb -h -1 -W \
  -Q "SET NOCOUNT ON;
     SELECT 'indexes='+CAST(COUNT(DISTINCT i.name) AS VARCHAR)
       FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id
       WHERE t.name NOT LIKE '%Migrations%';
     SELECT 'foreign_keys='+CAST(COUNT(*) AS VARCHAR) FROM sys.foreign_keys;
     SELECT 'check_constraints='+CAST(COUNT(*) AS VARCHAR) FROM sys.check_constraints;"
```

Expected on a fresh database: 12 tables (including the migration ledger), 38 indexes, 15 foreign
keys, 4 check constraints.

---

## 9. Test database

Integration tests run against **SQLite in-memory**, which the assignment explicitly permits.

> SQLite does not reproduce two SQL Server behaviours, and both were caught by the Playwright
> suite rather than by the unit tests:
>
> 1. `EnableRetryOnFailure` combined with a user-initiated transaction — order placement returned
>    HTTP 500 on SQL Server while passing on SQLite.
> 2. Multiple cascade paths — SQL Server rejects schemas with two cascade routes into one table.
>
> Both are fixed and documented. The E2E journeys exercise the real SQL Server path. Were more
> time available, the right improvement would be a second integration-test host targeting SQL
> Server so these are caught by the fast suite instead of the slow one.
