# ShopHub — Vue 3 + .NET 10 Minimal API

A full-stack modernization of the legacy **ShopHub** eCommerce platform.

The legacy application was ASP.NET MVC 5 / .NET Framework 4.7.2 / EF6 with server-rendered
Razor views, jQuery, and an ASP.NET Session shopping cart. This repository contains its
**complete replacement**: a Vue 3 + TypeScript single-page application backed by a RESTful
.NET 10 Minimal API, EF Core 10, SQL Server, and JWT authentication.

The legacy source is retained **unchanged** at the repository root as a reference for domain
knowledge and image assets. It is never compiled, referenced, or invoked by the new application.

---

## Table of contents

1. [What changed and why](#1-what-changed-and-why)
2. [Prerequisites](#2-prerequisites)
3. [Repository layout](#3-repository-layout)
4. [Database setup](#4-database-setup)
5. [Running the backend](#5-running-the-backend)
6. [Running the frontend](#6-running-the-frontend)
7. [API documentation](#7-api-documentation)
8. [Default accounts](#8-default-accounts)
9. [Tests and coverage](#9-tests-and-coverage)
10. [Configuration reference](#10-configuration-reference)
11. [Further documentation](#11-further-documentation)

---

## 1. What changed and why

| Concern | Legacy | Modern | Rationale |
|---|---|---|---|
| Rendering | Razor `.cshtml` server views | Vue 3 SPA | Assignment mandates a decoupled SPA |
| Transport | MVC form posts + jQuery AJAX | JSON over REST | Assignment mandates a REST API |
| Cart storage | `HttpContextCartSession` (ASP.NET Session) | `Cart`/`CartItem` tables keyed by customer **or** anonymous id | Session is not stateless and cannot survive horizontal scale |
| Data access | EF6 EDMX + POCO | EF Core 10, code-first migrations | Assignment mandates EF Core 10 |
| Database | `LegacyEcommerceDb` | `ShopHubModernDb` (separate) | The legacy database is never modified |
| Authentication | OWIN cookie + Identity 2 | JWT access token + rotating refresh token | Assignment mandates JWT |
| Password policy | 6 characters, no complexity | 8+ chars with four character classes | The legacy policy was weak |
| Admin portal | **Did not exist** | Full `/api/admin/*` surface behind an Admin role policy | Required by the assignment |
| Tests | **None** | 202 backend + 175 frontend + 93 E2E | Required by the assignment |

Business behaviour was ported deliberately, with documented decisions where the legacy code was
defective. The most important:

- **Pricing.** `Product.Price` is the authoritative selling price and is *never* re-discounted.
  `DiscountPercent` is presentation-only and derives the struck-through "was" price. The legacy
  `CartService` applied the discount a second time at cart time, under-charging customers.
  See [docs/migration-notes.md](docs/migration-notes.md).
- **Stock is actually decremented** on order placement, inside the same transaction that creates
  the order. The legacy application never decremented stock at all.
- **Order totals are re-derived server-side** from catalogue data. No client-supplied price or
  total is ever trusted.
- **No card data is collected or stored.** The legacy checkout persisted a PAN in `Session` and
  `TempData`.

### Storefront behaviour worth knowing

- **Incremental search.** Typing in the header search (or the listing-page search box) refreshes
  results after a 300 ms pause. Matching is done by the API over the product name *and*
  description, is case-insensitive, and accepts partial terms (`lap` finds both Laptop
  products). A request in flight for an older term can never overwrite a newer one's results.
  Clearing the box restores the unfiltered listing.
- **Horizontal strips.** The home page's category tiles and featured products are single
  non-wrapping strips with left/right buttons that advance by one card. The scrollbar is
  suppressed; wheel, trackpad and touch panning still work, and neither strip introduces
  page-level horizontal scrolling.
- **Continuously flowing category strip.** The category tiles travel **steadily** from right to
  left on a linear CSS animation of an inner track — no per-card stepping and no pauses between
  cards. The loop distance is the *measured* width of one pass, gaps included, published to CSS
  as `--loop-distance` and recomputed on resize, so it stays correct as the card basis, the
  category count or the viewport change; a hardcoded distance would drift. The track renders the
  categories twice, so reaching the end is pixel-identical to the beginning and the loop has no
  visible reset or blank gap. The second pass is `aria-hidden` + `inert` and is not
  keyboard-reachable, so no category is announced, focused or selected twice. Movement pauses
  while the strip is hovered or a card has focus, and is disabled entirely under
  `prefers-reduced-motion: reduce` — where the strip instead becomes a hand-scrolled viewport and
  the arrows scroll it as before.
  - **The arrows share the animation's timeline** rather than writing `scrollLeft`, because the
    container must not scroll while the track is translating — two mechanisms moving the same
    content would fight and jump. The arrows scrub the animation's `animation-delay` instead of
    pausing and resuming it imperatively: an imperative `pause()`/`play()` detaches the animation
    from `animation-play-state`, after which the hover and focus pause silently stops working.
  - The featured strip is click-driven and unaffected.
- **Featured Products rail.** A horizontally scrolling rail of full product cards, sized to the
  same basis as the Shop grid's card minimum (280px) so the two sections read at one scale.
  Roughly five are visible on a wide desktop, fewer as the viewport narrows; the left and right
  arrows step the rail and disable themselves at each end, and everything else is reached by
  scrolling. The rail does **not** move on its own — it is manual only, so there is no animation
  to fight the arrows for the scroll position (the *category* strip below it is the one that
  flows continuously). Each card reuses the shared `.product-thumb` square frame and the same
  tokens and anatomy as `ProductCard` — category, name, rating, price, was-price, stock and an
  Add to Cart button in a footer — rather than a parallel set of rules, so the sections cannot
  drift apart.
- **Promotional collections.** Featured Products, New Arrivals and Deals each return **ten**
  products from `GET /api/products/home`. The counts were raised from eight, and each collection
  keeps its existing server-side eligibility rule (`IsFeatured`, newest `CreatedAt`, and
  `DiscountPercent > 0` respectively) — the underlying pools were always larger than the caps, so
  no collection logic was rewritten and no product was invented. Two existing catalogue entries
  were promoted to featured and two given a discount, which also enlarges the Shop page's
  on-sale set. The changes live in `SeedData.Products` for fresh databases and in the
  `ExpandPromotionalCollections` data migration for existing ones, so both end up identical.
- **Add-to-cart confirmation.** A successful add shows a toast in the **top-right** corner,
  naming the product and clear of the header at every width. It appears only after the API
  confirms the add, so a failure reports an error instead.
- **Shop category sidebar.** One top-level **All products** entry, then each real category with
  its actual subcategories and product counts. The tree component renders itself recursively, so
  the "All products" entry is emitted only at the root; previously it was repeated inside every
  category group and highlighted at each level.
- **Home hero.** Two parts: the introductory text on the left and the Deal of the Day chair card
  on the right, composed as one unit. Neither column is a flexible `1fr`. Both are bounded to
  roughly what their content needs (520px and 480px, tightening to 460/400 on smaller screens)
  and the pair is **centred** in the content container with a 72px column gap, so the leftover
  width is shared evenly at the edges instead of pooling into a hole between the two. This cut
  the gap between the text and the card from 783px to 183px and made the insets symmetric to the
  pixel at every width. Below 900px the hero stacks and the card is centred at up to 460px
  rather than stretching to the full page width. The card's kicker, price, discount badge and
  Shop Now button are unchanged.
- **Responsive header.** The header bar wraps at **900px** rather than 720px. Below that the
  search field takes its own row and the navigation tightens, which is what removes the
  horizontal overflow that previously appeared between roughly 721px and 900px, where the
  brand, search, navigation, account and cart controls needed more width than the viewport had.
  Every control stays visible and the bar never overlaps the page content from 320px upward.
- **Wide content column.** The page and the header share one `--content-width` (1720px), so they
  align at every width and the layout uses ~90% of a 1920px desktop instead of ~65%. Cards and
  section spacing scale with it; the container stops binding below that, so mobile is unchanged.
- **Post-login navigation.** A successful sign-in lands on Home. If the visitor arrived at a
  protected page first, the router records that intent in `?redirect=` and it is still honoured.

---

## 2. Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.402+** | Pinned by `backend/global.json` |
| Node.js | **20.19+** or 22+ | Vite 8 requires it |
| SQL Server | 2022 (Developer/Express) | Any edition that supports LocalDB/Express |
| Docker *(optional)* | 24+ | Only to run SQL Server in a container |

Check your toolchain:

```bash
dotnet --version     # expect 10.0.4xx
node --version       # expect v20.19+ or v22+
```

---

## 3. Repository layout

```
.
├── backend/                     # .NET 10 solution
│   ├── Ecommerce.sln
│   ├── Ecommerce.Api/           # Minimal API, endpoints, middleware, auth wiring
│   ├── Ecommerce.Core/          # Entities, DTOs, interfaces, pure domain logic
│   ├── Ecommerce.Infrastructure/ # DbContext, configurations, migrations, seed, services
│   ├── Ecommerce.Tests/         # xUnit + WebApplicationFactory (SQLite in-memory)
│   └── scripts/                 # run-api.sh, run-api-e2e.sh, report-coverage.py
│
├── frontend/                    # Vue 3 + TypeScript + Vite SPA
│   ├── src/
│   │   ├── api/                 # Axios modules, one per feature
│   │   ├── components/          # layout / catalog / cart / checkout / account / admin / common
│   │   ├── stores/              # Pinia: auth, cart, catalog, orders, ui
│   │   ├── views/               # Route-level components
│   │   ├── router/              # Route table + guards
│   │   ├── test/                # Vitest specs
│   │   └── types/               # Shared TypeScript interfaces
│   ├── e2e/                     # Playwright journeys
│   └── public/images/products/  # Migrated product imagery
│
├── docs/                        # Architecture, API, database, migration notes, reflection
│
├── Ecommerce.Core/              # ── legacy reference only ──
├── Ecommerce.Data/              # ── legacy reference only ──
├── Ecommerce.Services/          # ── legacy reference only ──
├── Ecommerce.Web/               # ── legacy reference only ──
├── LegacyEcommerce.sln          # ── legacy reference only ──
├── db/                          # ── legacy seed scripts (data source) ──
└── packages/                    # ── legacy NuGet packages ──
```

> **The four root-level legacy projects are not part of the new application.** They are not
> referenced by `backend/Ecommerce.sln`, and the new application does not load, call, or depend
> on them at runtime.

### 3.1 Required reusable components

The assignment lists 17 reusable components. All 17 exist, are driven by real state, and are unit
tested; none is a placeholder.

| Component | Path |
| --- | --- |
| `AppHeader` | `src/components/layout/AppHeader.vue` |
| `AppFooter` | `src/components/layout/AppFooter.vue` |
| `ProductCard` | `src/components/catalog/ProductCard.vue` |
| `ProductList` | `src/components/catalog/ProductList.vue` |
| `ProductDetails` | `src/components/catalog/ProductDetails.vue` |
| `CartDrawer` | `src/components/cart/CartDrawer.vue` |
| `CartItemRow` | `src/components/cart/CartItemRow.vue` |
| `OrderSummary` | `src/components/cart/OrderSummary.vue` |
| `CheckoutForm` | `src/components/checkout/CheckoutForm.vue` |
| `AddressForm` | `src/components/account/AddressForm.vue` |
| `LoginForm` | `src/components/account/LoginForm.vue` |
| `RegisterForm` | `src/components/account/RegisterForm.vue` |
| `AdminProductForm` | `src/components/admin/AdminProductForm.vue` |
| `AdminOrderTable` | `src/components/admin/AdminOrderTable.vue` |
| `Pagination` | `src/components/common/Pagination.vue` |
| `LoadingSpinner` | `src/components/common/LoadingSpinner.vue` |
| `NotificationToast` | `src/components/common/NotificationToast.vue` |

The form components are presentational: the page view owns the async call, navigation and
notifications, and passes `modelValue` / `submitting` / `error` down while listening for
`update:modelValue` and `submit`. This keeps the views thin and the components independently
testable, and it removed the order-summary markup that had been duplicated between `CartView`
and `CheckoutView`.

---

## 4. Database setup

The modern application uses its own database, **`ShopHubModernDb`**, entirely separate from the
legacy `LegacyEcommerceDb`. The legacy database is never read, written, or migrated by this project.

### 4.1 Option A — Docker (quickest)

```bash
docker run -d --name shophub-mssql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='<your-strong-password>' \
  -e MSSQL_PID=Developer \
  -p 1434:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

> Use port **1434** so the legacy `mssql` container on 1433 is left untouched.

Or use the provided Compose file:

```bash
cd backend
cp .env.example .env          # then edit the password
docker compose -f docker-compose.dev.yml up -d
```

### 4.2 Option B — an existing SQL Server instance

Point the connection string at your own instance (SQL Server Express or LocalDB). LocalDB
connection strings look like:

```
Server=(localdb)\MSSQLLocalDB;Database=ShopHubModernDb;Trusted_Connection=True;
```

### 4.3 Apply migrations and seed

Migrations run automatically on API start-up. To apply them manually:

```bash
cd backend
export ConnectionStrings__DefaultConnection='<your connection string>'

dotnet tool restore
dotnet ef database update \
  --project Ecommerce.Infrastructure \
  --startup-project Ecommerce.Api
```

### 4.4 Seed data

Seeding runs automatically on start-up and is **idempotent** — restarting never duplicates rows.

Seeded content:

| Entity | Count | Source |
|---|---|---|
| Categories | 27 | Ported from the legacy canonical seed `db/seed_generic.sql` |
| Products | 25 | 22 ported from the legacy seed + 3 added to meet the assignment minimum of 25 |
| Product images | 50 | Legacy product imagery, served from `frontend/public/images/products/` |
| Product variants | 43 | Ported from the legacy seed |
| Administrator | 1 | Seeded |
| Sample customer | 1 | Seeded |

Seeding is *content-preserving, not code-preserving*: names, slugs, descriptions, prices,
discounts, ratings, stock levels and image associations were carried over; the legacy SQL
procedures were not.

---

## 5. Running the backend

```bash
cd backend
cp .env.example .env          # edit the values
./scripts/run-api.sh
```

The API starts on **http://localhost:5099**. On start-up it applies migrations, seeds the
database, and logs the result.

Verify:

```bash
curl http://localhost:5099/health
# {"status":"Healthy","totalDurationMs":0.5}
```

Swagger UI (development only): <http://localhost:5099/swagger>

Or run it directly, supplying configuration yourself:

```bash
cd backend
export ConnectionStrings__DefaultConnection='<connection string>'
export Jwt__SigningKey='<at least 32 characters>'
export Jwt__Issuer='ShopHubApi'
export Jwt__Audience='ShopHubClient'
export Cors__AllowedOrigins__0='http://localhost:5173'

dotnet run --project Ecommerce.Api --urls http://localhost:5099
```

### Required configuration

| Setting | Required | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | **Yes** | Fails fast if missing |
| `Jwt:SigningKey` | **Yes** | ≥ 32 characters. Startup fails fast if absent/short |
| `Jwt:Issuer` / `Jwt:Audience` | Yes | Defaults provided |
| `Jwt:AccessTokenMinutes` | No | Default 15 |
| `Jwt:RefreshTokenDays` | No | Default 7 |
| `Cors:AllowedOrigins` | Yes in dev | The Vite dev server origin |
| `Seed:*` | No | Development-only demo credentials |

---

## 6. Running the frontend

```bash
cd frontend
npm install
npm run dev
```

The SPA starts on **http://localhost:5173** (the port configured in `vite.config.ts`).

> If that port is already taken — for example by another project on the same machine — Vite
> automatically uses the next free one, so the URL becomes `http://localhost:5174` (and so on).
> Check the URL Vite prints on start-up. To pin a specific port instead, pass it explicitly:
> `npm run dev -- --port 5174`.

Vite proxies `/api` and `/health` to the backend (default `http://localhost:5099`), so the
browser talks to a single origin and no CORS pre-flight is needed in development. Override the
target if your API runs elsewhere:

```bash
VITE_API_PROXY=http://localhost:5000 npm run dev
```

If you change the dev-server port, add the new origin to the API's CORS allow-list as well:

```bash
export Cors__AllowedOrigins__0='http://localhost:5174'
```

Production build:

```bash
npm run build     # type-checks (vue-tsc -b), then bundles to frontend/dist
npm run preview   # serves the production build
```

---

## 7. API documentation

| Resource | Location |
|---|---|
| OpenAPI document (live) | <http://localhost:5099/openapi/v1.json> |
| Swagger UI (development) | <http://localhost:5099/swagger> |
| Endpoint reference | [docs/api.md](docs/api.md) |

Endpoints are grouped with `MapGroup` and documented with `.WithSummary()` / `.Produces<T>()`, so
the OpenAPI document is generated from the code rather than maintained by hand.

### Endpoint summary

| Group | Prefix | Auth |
|---|---|---|
| Authentication | `/api/auth/*` | Anonymous (except `/me`, `/logout`) |
| Catalog | `/api/categories`, `/api/products/*` | Anonymous |
| Cart | `/api/cart/*` | Anonymous; `/merge` requires a customer |
| Orders | `/api/orders/*` | Authenticated |
| Account | `/api/account/*` | Authenticated |
| Administration | `/api/admin/*` | **Admin role** |
| Operations | `/health` | Anonymous |

---

## 8. Default accounts

Development-only credentials, created by the seeder. **Change or disable them for any shared
environment.**

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@shophub.local` | `Admin@12345` |
| Customer | `customer@shophub.local` | `Customer@12345` |

Override them with `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:CustomerEmail` and
`Seed:CustomerPassword`.

---

## 9. Tests and coverage

### 9.1 Backend — 202 tests

```bash
cd backend
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Unit tests cover the pure domain logic. Integration tests drive the **real HTTP pipeline**
through `WebApplicationFactory` against a SQLite in-memory database, exercising authentication,
authorization, cart, guest-cart merge, checkout, orders, account and administration.

### 9.2 Backend coverage

```bash
cd backend
dotnet test --collect:"XPlat Code Coverage"
python3 scripts/report-coverage.py
```

The report states the assignment's target directly:

```
Assignment target: >= 70% line coverage on Core / Services
  Core:                                  94.46%   (614/650)
  Services / Infrastructure:             90.24%   (1645/1823)
  Combined Core + Services:              91.35%   (2259/2473)
PASS - Core / Services line coverage 91.35% meets the 70% target.
```

Generated code (migrations, model snapshots, source generators) is excluded, since it is not
hand-written business logic.

### 9.3 Frontend — 175 tests

```bash
cd frontend
npm run test
npm run test:coverage
```

Vitest + Vue Test Utils cover the five Pinia stores, the reusable components and the shared
composables. The coverage gate is configured in `vite.config.ts` to exactly that scope;
route-level views and the presentational layout shells are covered end-to-end by Playwright
instead.

```
Statements   : 89.48%   (579/647)
Branches     : 89.85%   (487/542)
Functions    : 81.85%   (185/226)
Lines        : 90.31%   (513/568)
```

### 9.4 End-to-end — 93 tests

With the backend and frontend running:

```bash
cd backend && ./scripts/run-api-e2e.sh &
cd frontend && npm run test:e2e
```

The journeys cover all five required end-to-end flows:

1. Browse products
2. Add a product to the cart
3. Log in
4. Complete checkout
5. Create a product as an administrator

Plus guest-cart persistence, server-side search/filtering, authorization boundaries, sign-out,
account registration, weak-password rejection, the home-page category and featured strips, the
the category carousel's continuous flow (loop integrity, manual controls, hover and focus pause,
reduced-motion) and its accessibility guarantees, the responsive header at every affected width,
the add-to-cart notification, incremental search, post-login navigation, the responsive content
column, and the OpenAPI document / Swagger UI.

The suite waits for the API to report healthy before it starts (`e2e/global-setup.ts`), because
a cold API still has to apply migrations and seed the catalogue. Product selection targets the
first *purchasable* card rather than the first card, so repeated runs — which legitimately consume
stock — do not make the journeys depend on a pristine database.

**Rate limits.** The suite signs in many times from one IP, which exceeds the default
authentication budget of 10 requests per minute, so start the API with `run-api-e2e.sh` rather
than `run-api.sh`. That script exports the relaxed limits and `run-api.sh` maps them onto the
`RateLimiting:*` configuration keys:

```bash
cd backend
./scripts/run-api-e2e.sh &
```

To run the suite against a second API instance (leaving a development instance untouched), set
the listen URL **in `.env` rather than in the shell**. `run-api.sh` sources `.env` before it
starts, and a sourced assignment overwrites anything already in the environment, so
`SHOPHUB_API_URL=… ./scripts/run-api-e2e.sh` is ignored whenever a local `.env` exists:

```bash
# backend/.env
SHOPHUB_API_URL=http://localhost:5100
```

```bash
cd backend && ./scripts/run-api-e2e.sh &
cd frontend && E2E_BASE_URL=http://localhost:5175 E2E_SKIP_SERVERS=1 \
  E2E_API_URL=http://localhost:5100 npm run test:e2e
```

Throttling itself is still covered by the `RateLimitingTests` integration tests, which build
their own host at the default limits.

**Ports.** Playwright defaults to `http://localhost:5173` and to an API on `http://localhost:5099`.
If either had to move, point the suite at the real ones:

```bash
E2E_BASE_URL=http://localhost:5174 E2E_SKIP_SERVERS=1 E2E_API_URL=http://localhost:5099 \
  npm run test:e2e
```

---

## 10. Configuration reference

Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` → environment
variables → user secrets. Environment variables use `__` as the section separator, e.g.
`Jwt__SigningKey`.

A documented example with **no secrets** is in [backend/.env.example](backend/.env.example).

```bash
cd backend
cp .env.example .env
# edit .env, then:
./scripts/run-api.sh
```

### Secrets handling

- No secret is committed to the repository. `appsettings.json` contains placeholders only.
- `.env` is git-ignored; `.env.example` is committed.
- The JWT signing key must be ≥ 32 characters; the API **refuses to start** otherwise.
- Refresh tokens are stored as SHA-256 digests — a database leak yields no usable credentials.
- Passwords are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2, per-password salt).

---

## 11. Further documentation

| Document | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Layering, request pipeline, token lifecycle, design decisions |
| [docs/api.md](docs/api.md) | Every endpoint, request/response shape, status codes |
| [docs/database.md](docs/database.md) | Schema, relationships, constraints, indexes, seed data |
| [docs/migration-notes.md](docs/migration-notes.md) | Legacy → modern mapping and every behavioural decision |
| [docs/reflection.md](docs/reflection.md) | What was learned, what went wrong, what would change |
| [docs/legacy-architecture.md](docs/legacy-architecture.md) | The original application's architecture (reference) |
