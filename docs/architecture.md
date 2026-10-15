# Architecture

How the modernized ShopHub application is put together, and why.

---

## 1. System shape

```
┌──────────────────────────────────────────────────────────────────┐
│  Browser                                                          │
│  Vue 3 SPA  ·  Pinia  ·  Vue Router  ·  Axios                    │
│  access token: in-memory only    refresh token: HttpOnly cookie   │
└───────────────────────────┬──────────────────────────────────────┘
                            │  JSON over HTTP (same origin in dev)
┌───────────────────────────▼──────────────────────────────────────┐
│  ASP.NET Core Minimal API  (.NET 10)                             │
│                                                                   │
│  Middleware pipeline                                             │
│    ExceptionHandler ─▶ Serilog ─▶ Swagger ─▶ CORS ─▶ RateLimiter │
│    ─▶ ConcurrencyGate ─▶ Authentication ─▶ Authorization       │
│                                                                   │
│  Endpoint groups                                                 │
│    /api/auth      /api/catalog    /api/cart    /api/orders      │
│    /api/account   /api/admin      /health      /openapi/v1.json │
└───────────────────────────┬──────────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────────┐
│  Ecommerce.Infrastructure                                       │
│    EcommerceDbContext (EF Core 10, code-first)                   │
│    Migrations  ·  Seeding  ·  Service implementations           │
└───────────────────────────┬──────────────────────────────────────┘
                            │
              ┌─────────────▼─────────────┐
              │  ShopHubModernDb          │   ← separate database
              │  (SQL Server 2022)        │      legacy DB untouched
              └───────────────────────────┘
```

---

## 2. Backend projects

### `Ecommerce.Core` — no framework dependencies

Pure domain. Depends on nothing but the BCL.

| Folder | Contents |
|---|---|
| `Entities/` | The 8 required entities plus `ProductImage`, `ProductVariant`, `RefreshToken` |
| `Dtos/` | Request/response contracts, one folder per feature |
| `Interfaces/Services/` | Service contracts, implemented in Infrastructure |
| `Domain/` | Pure business logic: pricing, cart merge, slugs, order numbers, password policy |
| `Common/` | `PagedResult<T>`, sort keys, paging bounds |

Deliberately contains **no** EF Core types. The domain can be reasoned about and tested without a
database.

### `Ecommerce.Infrastructure` — persistence and orchestration

| Component | Responsibility |
|---|---|
| `EcommerceDbContext` | The 11 `DbSet`s |
| `Persistence/Configurations/` | One `IEntityTypeConfiguration<T>` per entity — keys, FKs, indexes, delete behaviour, check constraints |
| `Persistence/Migrations/` | EF Core 10 code-first migrations |
| `Seeding/` | Idempotent catalogue and account seeding |
| `Services/` | `AuthService`, `CatalogService`, `CartService`, `OrderService`, `AccountService`, `AdminService` |

### `Ecommerce.Api` — transport

Endpoints, middleware, auth wiring, validation, error mapping. Contains no business rules.

### `Ecommerce.Tests` — verification

xUnit + `WebApplicationFactory`, backed by a SQLite in-memory database so the whole HTTP pipeline
runs without provisioning SQL Server.

---

## 3. Why the business logic lives in `Core`

The assignment requires ≥ 70 % line coverage on "Core / Services". More importantly, the rules
that matter are the rules that are *hard* to test through HTTP:

- **`PricingCalculator`** — all money arithmetic, in one place. Never uses `double`.
- **`CartMergePlanner`** — a pure function over (existing lines, guest candidates) → (merged lines,
  warnings). Every edge case the assignment calls out is a branch here, unit-tested directly.
- **`OrderNumberGenerator`** — collision-resistant order numbers.
- **`SlugGenerator`** — URL-safe slugs with uniqueness resolution.
- **`PasswordPolicy`** — the complexity rules.

Because these are pure, they are fast to test and impossible to get wrong by accident. The
services that orchestrate them against the database are tested through the real HTTP pipeline.

---

## 4. Request pipeline

```
Request
   │
   ▼
ExceptionHandler          Domain exceptions → RFC 7807 ProblemDetails
   ▼
SerilogRequestLogging     Structured request logs
   ▼
Swagger (Development)     Interactive API docs
   ▼
Cors                     Only configured trusted origins, with credentials
   ▼
RateLimiter              10/min per IP on /api/auth/*; 300/min globally
   ▼
ConcurrencyGate          Serialises DB access for the request (see §5)
   ▼
Authentication           JWT bearer validation
   ▼
Authorization            Role policies
   ▼
EndpointFilter           FluentValidation, when a DTO is bound
   ▼
Endpoint handler         Resolves services, delegates, returns TypedResults
```

### Error contract

A single `GlobalExceptionHandler` maps domain exceptions to status codes. This is the only place
that decides the error shape.

| Exception | Status |
|---|---|
| `DuplicateEmailException` | 409 |
| `InvalidCredentialsException`, `InvalidRefreshTokenException` | 401 |
| `ProductNotFoundException`, `CategoryNotFoundException`, `CartItemNotFoundException` | 404 |
| `InsufficientStockException`, `EmptyCartException`, `ProductUnavailableException` | 409 |
| `InvalidShippingMethodException`, `InvalidQuantityException`, `InvalidOrderStatusException` | 400 |
| `InvalidCategoryHierarchyException`, `InvalidOrderStatusTransitionException` | 409 |
| Anything else | 500, with a generic message — internals are never leaked |

Every response carries `traceId` and `timestamp`.

---

## 5. `ConcurrencyGate` — why it exists

`EcommerceDbContext` is scoped, so every service in a request shares one instance. EF Core
forbids two concurrent operations on the same context and throws:

> A second operation was started on this context instance before a previous operation completed.

This was found the hard way: `/api/products/home` originally issued four catalogue queries with
`Task.WhenAll` and returned HTTP 500. Fixing that one call site would leave the same trap for the
next endpoint author, so the gate makes it safe by construction — one database operation at a time
per request.

The cost is negligible: these are small, indexed queries.

---

## 6. Authentication and the token lifecycle

```
  login / register
        │
        ├─► password verified (Identity PasswordHasher, PBKDF2 + salt)
        ├─► access token minted   : short-lived JWT (15 min), returned in the body
        └─► refresh token minted  : 48 random bytes
                                     · stored as a SHA-256 digest only
                                     · delivered as HttpOnly, SameSite=Strict cookie
                                     · path-scoped to /api/auth

  API request
        │
        ├─► Authorization: Bearer <access token>
        ├─► 200 ─────────────────────────────────────────────►
        │
        └─► 401
              │
              ▼
        Axios response interceptor (single in-flight refresh)
              │
              ├─► POST /api/auth/refresh  →  new access token + rotated refresh cookie
              │   · the presented token is revoked
              │   · replaying a consumed token revokes the whole family (reuse detection)
              │
              └─► retry the original request once
```

### Security properties

| Property | How |
|---|---|
| No plaintext passwords | `PasswordHasher<Customer>` (PBKDF2, per-password salt) |
| Access token unreachable from JS | Held in a module-scoped variable, never in a store or `localStorage` |
| Refresh token unreachable from JS | HttpOnly cookie |
| Session survives a hard reload | Router guard calls `refreshAccessToken()` before evaluating `meta.requiresAuth` |
| No refresh stampede | Concurrent 401s share one in-flight promise (`refreshPromise`) |
| No infinite retry loop | Each request retries at most once (`_retried` flag); auth endpoints are excluded |
| No role self-assignment | `RegisterRequest` has no role field; the service hardcodes `Roles.Customer` |
| Brute-force blunted | Per-IP rate limit on `/api/auth/*` |
| No account enumeration | Failed logins return one indistinguishable message |

### Authorization

Authorization is enforced **server-side**. Hiding admin links in the UI is a convenience, not a
control — the integration tests call the API directly with a customer token and assert 403.

The frontend route guards mirror the server policy for user experience only.

---

## 7. Cart design

The legacy cart lived in ASP.NET Session. It is replaced by two tables with explicit ownership.

```
Cart                         CartItem
─────                        ─────────
Id                           Id
CustomerId  (nullable) ◄──┐   CartId        (FK, cascade)
AnonymousId (nullable)  ◄──┤   ProductId     (FK, cascade)
CreatedAt                   ProductVariantId (FK, nullable)
UpdatedAt                   Quantity        (> 0, check constraint)
                            UnitPrice       (snapshot at add time)
                            ProductName     (snapshot at add time)
                            ImageUrl
```

A check constraint enforces **exactly one owner**:

```sql
(CustomerId IS NULL AND AnonymousId IS NOT NULL)
OR (CustomerId IS NOT NULL AND AnonymousId IS NULL)
```

Guest identity is an opaque GUID in an HttpOnly `shophub_cart` cookie, minted by the API on first
contact and cached in `HttpContext.Items` so a single request cannot emit competing `Set-Cookie`
headers.

### Identity precedence

An authenticated caller always operates on the cart owned by their customer id. The anonymous id
is only consulted for guests. A stale guest cookie therefore cannot read or mutate another
shopper's cart after login.

### Guest → user merge

`POST /api/cart/merge` runs `CartMergePlanner.Merge`, a pure function. It handles:

| Case | Behaviour |
|---|---|
| Duplicate product | Quantities are summed |
| Deleted or deactivated product | Line dropped, warning returned |
| Invalid quantity (≤ 0) | Line ignored, warning returned |
| Stock insufficient | Line clamped, warning returned |
| Stale price | Every line is re-priced from the current catalogue |
| Excess quantity | Clamped to 99 per line |

Adjustments return `200 OK` with a `warnings` array rather than failing, so the shopper keeps a
usable cart. The guest cart is deleted once consumed.

---

## 8. Checkout and orders

```
POST /api/orders
   │
   ├─ 1. Idempotency check — a replayed key returns the original order
   ├─ 2. Load the cart; empty ⇒ 409
   ├─ 3. Validate every line against live catalogue data
   │        · product still active?
   │        · enough stock?
   │        · quantity ≥ 1?
   ├─ 4. Compute subtotal, shipping, total — all server-side
   │
   └─ 5. ExecutionStrategy.ExecuteAsync:
          BEGIN TRANSACTION
            · decrement stock (guarded, so concurrent orders cannot overdraw)
            · insert Order + OrderItems
            · empty the cart
          COMMIT
```

Two details worth calling out:

**Execution strategy.** `EnableRetryOnFailure` is configured for SQL Server, and EF Core's
retrying strategy does not support user-initiated transactions. The whole placement must therefore
run inside `strategy.ExecuteAsync(...)`. Without this, order placement returns HTTP 500 on SQL
Server while passing on SQLite — a bug the integration tests could not catch and the Playwright
journey did.

**Snapshots.** `Order` stores `ProductName` and `UnitPrice` per item, and full shipping/billing
address snapshots. Renaming a product or changing its price later never rewrites history.

---

## 9. Frontend architecture

### Stores

| Store | Responsibility |
|---|---|
| `auth` | Identity, login/register/logout, session restore, guest-cart merge |
| `cart` | Cart contents; the API response is the source of truth |
| `catalog` | Categories, paged products, filters, sort, home rails |
| `orders` | Order history, detail, placement |
| `ui` | Toasts, mini-cart drawer, global busy counter |

**The access token is deliberately not in `authStore`.** It lives in the Axios module so it can
never reach a template, a devtools state dump, or a persisted store.

### The API-is-authority rule

No store computes a total. Every mutation returns the recalculated cart from the server, and the
store stores *that*. The frontend may display a preview; the backend decides.

This is why a price change between add-to-cart and checkout silently corrects itself: the cart is
re-priced from the catalogue on every mutation.

### Routing and guards

| Route | Guard |
|---|---|
| `/`, `/products`, `/products/:id`, `/cart` | Public |
| `/checkout`, `/account/**` | `requiresAuth` |
| `/admin/**` | `requiresAdmin` |
| `/login`, `/register` | `guestOnly` |

Before evaluating a guard the router awaits `auth.restore()`, because a hard reload loses the
in-memory access token. Without that, a signed-in shopper would be bounced to `/login`.

### Design preservation

The legacy storefront's visual identity — palette, type scale, layout rhythm, product card
treatment, category tree, editorial home page — is carried over into
`src/assets/styles/main.css` as design tokens. Bootstrap 5 supplies the grid and utilities; the
custom layer supplies the brand.

The implementation mechanism was replaced wholesale: no Razor partials, no jQuery, no MVC routes,
no session-dependent interactions.

---

## 10. Testing strategy

| Layer | Tool | Scope |
|---|---|---|
| Unit (backend) | xUnit | Pure domain logic, entities, password hashing |
| Integration (backend) | xUnit + `WebApplicationFactory` + SQLite | Every endpoint group, auth, authorization, cart merge, checkout, orders, admin |
| Unit/component (frontend) | Vitest + Vue Test Utils | Stores, reusable components, formatters |
| End-to-end | Playwright | Five required journeys plus authorization boundaries |

The split is deliberate: pure logic is tested directly (fast, exhaustive edge cases);
behaviour that spans the stack is tested through the real HTTP pipeline; and the handful of flows
that must be proven end-to-end are driven through a real browser.

### Known environment caveat

Integration tests use SQLite, which the assignment explicitly permits. It does not reproduce two
SQL Server behaviours, and both were found by the E2E suite rather than the unit tests:

1. `EnableRetryOnFailure` + user transactions (see §8)
2. Multiple cascade paths (see `docs/database.md`)

**Both are documented, and both are now covered by the E2E journeys.** Were more time available,
the right fix would be a second integration-test host configured for SQL Server.

---

## 11. Preserved behaviour worth knowing

| Legacy behaviour | Decision |
|---|---|
| Standard shipping ₹150, Express ₹350 | Preserved exactly (`ShippingOptions`) |
| 8 products per page | Preserved as the default; the API accepts any page size up to 60 |
| "Only N left" at ≤ 12 units | Preserved (low-stock threshold) |
| Hierarchical categories | Preserved, including recursive product counts and descendant filtering |
| Discount badges and "was" pricing | Preserved, with the double-discount bug fixed |
| "Free shipping over ₹999" | **Dropped** — it was never implemented in the legacy code |
| Multi-step checkout | Preserved as address → shipping → review, now on one route with a progress indicator |
| Payment step collecting a card number | **Dropped** — no card data is collected or stored |
