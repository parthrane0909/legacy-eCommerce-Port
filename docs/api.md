# API reference

Base URL in development: `http://localhost:5099`

- **OpenAPI document:** `/openapi/v1.json` (live, generated from the code)
- **Swagger UI:** `/swagger` (development only)

All request and response bodies are JSON. Property names are camelCase.

---

## Conventions

### Authentication

Except where noted, authenticated endpoints expect:

```
Authorization: Bearer <accessToken>
```

The access token is returned in the response body by `login`, `register` and `refresh`. The
refresh token is delivered as an `HttpOnly` cookie scoped to `/api/auth` and is never readable
from JavaScript.

### Errors

Every error is an RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Insufficient stock",
  "status": 409,
  "detail": "Only 3 unit(s) of \"Wireless Headphones Pro\" remain in stock, but 5 were requested.",
  "instance": "/api/orders",
  "traceId": "0HNP9SA11NO0T",
  "timestamp": "2026-10-14T11:24:07.881Z"
}
```

Validation failures add an `errors` object keyed by field name.

| Status | Meaning |
|---|---|
| `400` | Validation failed, or the request is semantically invalid |
| `401` | Missing, expired, or invalid credentials |
| `403` | Authenticated, but the role is insufficient |
| `404` | Not found — or not visible to this caller |
| `409` | Conflict: duplicate email, insufficient stock, empty cart, illegal status change |
| `429` | Rate limit exceeded |

> **Ownership is enforced as 404, not 403.** Requesting another customer's order or address
> returns `404`, so the API does not confirm that the resource exists.

### Pagination

Every list endpoint accepts `page` (default 1) and `pageSize` (default 8, max 60) and returns:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 8,
  "totalCount": 25,
  "totalPages": 4,
  "hasPrevious": false,
  "hasNext": true
}
```

---

## Authentication — `/api/auth`

| Method | Route | Auth |
|---|---|---|
| POST | `/api/auth/register` | Anonymous |
| POST | `/api/auth/login` | Anonymous |
| POST | `/api/auth/refresh` | Anonymous (cookie) |
| POST | `/api/auth/logout` | Authenticated |
| GET | `/api/auth/me` | Authenticated |

### `POST /api/auth/register`

```json
{
  "email": "asha@example.com",
  "password": "Str0ng!Password",
  "firstName": "Asha",
  "lastName": "Nair"
}
```

→ `201 Created`

```json
{
  "accessToken": "eyJhbGciOi...",
  "tokenType": "Bearer",
  "expiresInSeconds": 900,
  "user": {
    "id": 3,
    "email": "asha@example.com",
    "firstName": "Asha",
    "lastName": "Nair",
    "role": "Customer",
    "fullName": "Asha Nair"
  }
}
```

Also sets the `shophub_refresh` cookie.

The role is **always** `Customer`. There is no role field in the contract, so a client cannot
self-assign `Admin` even by sending one.

| Error | Cause |
|---|---|
| `400` | Invalid email, or a password failing the policy |
| `409` | Email already registered |

**Password policy** — at least 8 characters, with an uppercase letter, a lowercase letter, a
digit, and a non-alphanumeric character.

### `POST /api/auth/login`

```json
{ "email": "customer@shophub.local", "password": "Customer@12345" }
```

→ `200 OK` with the same body shape as register, and sets the refresh cookie.

`401` for any failure — unknown account and wrong password are indistinguishable.

### `POST /api/auth/refresh`

Body optional; the cookie takes precedence.

```json
{ "refreshToken": "optional-explicit-token" }
```

→ `200 OK` with a new access token, and a **rotated** refresh cookie.

The presented token is revoked. Replaying an already-consumed token is treated as theft: the
entire token family for that customer is revoked and the request is rejected with `401`.

### `POST /api/auth/logout`

→ `204 No Content`. Revokes the presented refresh token and clears the cookie.

### `GET /api/auth/me`

→ `200 OK` with the `user` object. `401` when unauthenticated.

---

## Catalog — anonymous

| Method | Route |
|---|---|
| GET | `/api/categories` |
| GET | `/api/categories/flat` |
| GET | `/api/products` |
| GET | `/api/products/{id}` |
| GET | `/api/products/home` |

### `GET /api/categories`

Returns the hierarchy as nested roots, each with a recursive product count.

```json
[
  {
    "id": 1,
    "name": "Electronics",
    "slug": "electronics",
    "description": "Gadgets, computers and audio gear",
    "isActive": true,
    "sortOrder": 1,
    "parentId": null,
    "productCount": 9,
    "children": [
      {
        "id": 10, "name": "Audio", "slug": "audio",
        "parentId": 1, "productCount": 3, "children": []
      }
    ]
  }
]
```

Counts roll up to descendants in memory over a single query, so a root shows the total for its
whole subtree without the legacy N+1 problem.

`/api/categories/flat` returns the same records unnested.

### `GET /api/products`

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `page` | int | 1 | |
| `pageSize` | int | 8 | Max 60 |
| `categoryId` | int | — | Includes descendant categories by default |
| `search` | string | — | Matches name or description |
| `sort` | string | `newest` | `newest`, `price_asc`, `price_desc`, `name_asc`, `name_desc`, `rating_desc` |
| `includeDescendantCategories` | bool | `true` | Set `false` for exact-category filtering |
| `minPrice`, `maxPrice` | decimal | — | |
| `inStockOnly` | bool | — | |

All filtering, sorting and paging happens in the database. The client never downloads the full
catalogue.

An unrecognised `sort` falls back to `newest` rather than erroring.

```json
{
  "items": [
    {
      "id": 1,
      "name": "Wireless Headphones Pro",
      "slug": "wireless-headphones-pro",
      "price": 4999.00,
      "wasPrice": 5881.18,
      "discountPercent": 15,
      "imageUrl": "/images/products/headphones.jpg",
      "categoryName": "Audio",
      "isFeatured": true,
      "inStock": true,
      "stock": 40,
      "ratingNum": 4.5
    }
  ],
  "page": 1,
  "pageSize": 8,
  "totalCount": 25,
  "totalPages": 4,
  "hasPrevious": false,
  "hasNext": true
}
```

> `price` is the authoritative selling price. `wasPrice` is derived from `discountPercent` for
> display only and is never used in money arithmetic.

### `GET /api/products/{id}`

```json
{
  "id": 1,
  "name": "Wireless Headphones Pro",
  "slug": "wireless-headphones-pro",
  "description": "Over-ear Bluetooth headphones...",
  "price": 4999.00,
  "wasPrice": 5881.18,
  "discountPercent": 15,
  "imageUrl": "/images/products/headphones.jpg",
  "stock": 40,
  "inStock": true,
  "isFeatured": true,
  "ratingNum": 4.5,
  "categoryId": 10,
  "categoryName": "Audio",
  "images": [
    { "id": 1, "url": "/images/products/headphones.jpg", "altText": "Wireless Headphones Pro",
      "isPrimary": true, "sortOrder": 0 }
  ],
  "variants": [
    { "id": 1, "name": "Black", "sku": "WIRELESS-HEADPHONES-PRO-BLACK",
      "priceDelta": 0.00, "stock": 20 }
  ]
}
```

`404` when the product does not exist or is inactive.

### `GET /api/products/home`

Four rails in one round trip — `featured`, `newArrivals`, `deals`, `categories`.

---

## Cart — `/api/cart`

Anonymous callers use an opaque cart cookie the API issues automatically. Authenticated callers
use their customer id, which always takes precedence.

| Method | Route | Auth |
|---|---|---|
| GET | `/api/cart` | Anonymous |
| POST | `/api/cart/items` | Anonymous |
| PUT | `/api/cart/items/{productId}` | Anonymous |
| DELETE | `/api/cart/items/{productId}` | Anonymous |
| DELETE | `/api/cart` | Anonymous |
| POST | `/api/cart/merge` | **Customer** |

### Response shape

```json
{
  "id": 4,
  "isGuest": false,
  "itemCount": 3,
  "subtotal": 12997.00,
  "items": [
    {
      "id": 12,
      "productId": 1,
      "productName": "Wireless Headphones Pro",
      "variantName": "Black",
      "imageUrl": "/images/products/headphones.jpg",
      "quantity": 3,
      "unitPrice": 4999.00,
      "lineTotal": 14997.00,
      "availableStock": 40,
      "inStock": true
    }
  ],
  "warnings": []
}
```

`unitPrice` is re-derived from the catalogue on every mutation, so a stale cart price cannot
survive a price change.

### `POST /api/cart/items`

```json
{ "productId": 1, "quantity": 2, "productVariantId": 3 }
```

`quantity` defaults to 1 and must be 1–99. Adding a product already in the cart increases its
quantity. Quantities are clamped to available stock rather than rejected.

`400` invalid quantity · `404` unknown product

### `PUT /api/cart/items/{productId}`

```json
{ "quantity": 5 }
```

`400` invalid quantity · `404` not in cart

### `DELETE /api/cart/items/{productId}`

→ `200 OK` with the recalculated cart. Removing an absent line is a no-op, not an error.

### `DELETE /api/cart`

→ `200 OK` with an empty cart.

### `POST /api/cart/merge`

Folds the guest cart into the authenticated customer's cart.

```json
{ "anonymousId": "optional-explicit-guest-id" }
```

The id is normally read from the cookie the API issued, so most clients send nothing.

→ `200 OK` with the merged cart. If anything had to be dropped or clamped, `warnings` explains
what:

```json
{
  "warnings": [
    "\"Classic Alarm Clock\" is out of stock and was removed from your cart.",
    "Only 3 unit(s) of \"Pro Yoga Mat 6mm\" remain in stock."
  ]
}
```

A merge that succeeds with adjustments is **not** an error — the shopper keeps a usable cart.

`401` when unauthenticated.

---

## Orders — `/api/orders`

All require authentication. A customer sees only their own orders.

| Method | Route |
|---|---|
| POST | `/api/orders` |
| GET | `/api/orders` |
| GET | `/api/orders/{id}` |
| GET | `/api/orders/shipping-options` |

### `POST /api/orders`

```json
{
  "shippingMethod": "Express",
  "shippingAddress": {
    "firstName": "Rohan", "lastName": "Mehta",
    "line1": "12 MG Road", "line2": "Flat 4",
    "city": "Bengaluru", "state": "Karnataka",
    "postalCode": "560001", "country": "India", "phone": "+91 9000000000"
  },
  "billingAddress": { },
  "billingSameAsShipping": true
}
```

Optional header:

```
Idempotency-Key: 8f14e45f-ea3f-4a1b-9c2d-1e2f3a4b5c6d
```

A repeated submission with the same key returns the original order instead of creating a second
one — a double-clicked checkout cannot ship two parcels.

→ `201 Created` with the order and a `Location` header.

| Error | Cause |
|---|---|
| `400` | Invalid shipping method or incomplete address |
| `409` | Empty cart, insufficient stock, product unavailable |

Order creation, order items, the stock decrement and cart finalisation all happen in one
transaction. Prices and totals are recomputed from the database; nothing from the request is
trusted.

**No payment is taken and no card details are collected or stored.** `paymentMethod` is a
demonstration value.

### `GET /api/orders`

Paged history for the current customer, newest first.

### `GET /api/orders/{id}`

```json
{
  "id": 7,
  "orderNumber": "ORD-20261014-000042-A7F2",
  "orderDate": "2026-10-14T11:24:07Z",
  "status": "Pending",
  "subtotal": 14997.00,
  "shippingCost": 350.00,
  "total": 15347.00,
  "shippingMethod": "Express",
  "paymentMethod": "Card",
  "shippingAddress": { "fullName": "Rohan Mehta", "line1": "12 MG Road", ... },
  "billingAddress": { ... },
  "items": [
    { "id": 19, "productId": 1, "productName": "Wireless Headphones Pro",
      "variantName": "Black", "quantity": 3, "unitPrice": 4999.00, "lineTotal": 14997.00 }
  ]
}
```

`404` if the order does not exist **or belongs to another customer**.

Order item names, unit prices and addresses are snapshots taken at purchase time. Later catalogue
changes never rewrite history.

### `GET /api/orders/shipping-options`

```json
[
  { "code": "Standard", "label": "Standard delivery — ₹150 · 5–7 days", "cost": 150, "estimate": "5–7 days" },
  { "code": "Express",  "label": "Express delivery — ₹350 · 1–2 days",  "cost": 350, "estimate": "1–2 days" }
]
```

---

## Account — `/api/account`

All require authentication. Every query is scoped to the authenticated customer.

| Method | Route |
|---|---|
| GET | `/api/account/profile` |
| PUT | `/api/account/profile` |
| GET | `/api/account/addresses` |
| POST | `/api/account/addresses` |
| PUT | `/api/account/addresses/{id}` |
| DELETE | `/api/account/addresses/{id}` |

### `GET /api/account/profile`

```json
{
  "id": 2,
  "email": "customer@shophub.local",
  "firstName": "Rohan",
  "lastName": "Mehta",
  "fullName": "Rohan Mehta",
  "role": "Customer",
  "createdAt": "2026-10-14T10:41:52Z"
}
```

### `PUT /api/account/profile`

```json
{ "firstName": "Rohit", "lastName": "Sharma", "email": "new@example.com" }
```

The role cannot be changed. `409` if the new email belongs to another account.

### Addresses

```json
{
  "firstName": "Rohan", "lastName": "Mehta",
  "line1": "12 MG Road", "line2": "Flat 4",
  "city": "Bengaluru", "state": "Karnataka",
  "postalCode": "560001", "country": "India",
  "phone": "+91 9000000000",
  "isDefault": true
}
```

- `POST` → `201` with a `Location` header.
- `PUT /{id}` → `200`; `404` if it is not the caller's address.
- `DELETE /{id}` → `204`; `404` if not the caller's address.

The first address created becomes the default automatically. Deleting the default promotes another
address, so a customer is never left without one.

---

## Administration — `/api/admin`

Every endpoint requires the **Admin** role. An anonymous caller gets `401`; an ordinary customer
gets `403`.

| Method | Route |
|---|---|
| GET | `/api/admin/products` |
| POST | `/api/admin/products` |
| PUT | `/api/admin/products/{id}` |
| DELETE | `/api/admin/products/{id}` |
| GET | `/api/admin/categories` |
| POST | `/api/admin/categories` |
| PUT | `/api/admin/categories/{id}` |
| DELETE | `/api/admin/categories/{id}` |
| GET | `/api/admin/orders` |
| PUT | `/api/admin/orders/{id}/status` |
| GET | `/api/admin/dashboard` |

### `GET /api/admin/products`

Paged, includes inactive products, optional `search`.

### `POST /api/admin/products`

```json
{
  "name": "Studio Monitor Speakers",
  "slug": null,
  "description": "Nearfield monitors for mixing.",
  "price": 12999.00,
  "stock": 15,
  "imageUrl": "/images/products/placeholder.jpg",
  "discountPercent": null,
  "ratingNum": null,
  "isFeatured": false,
  "isActive": true,
  "categoryId": 10
}
```

→ `201` with the created product. When `slug` is omitted the API derives one from the name and
appends a suffix if needed to keep it unique.

| Error | Cause |
|---|---|
| `400` | Price ≤ 0, negative stock, discount outside 0–90, rating outside 0–5 |
| `404` | Unknown `categoryId` |

### `DELETE /api/admin/products/{id}`

→ `204`.

A product referenced by an order is **archived** (`isActive = false`) rather than deleted, so
order history stays readable. A product with no order history is deleted outright, along with its
images and variants.

### Categories

```json
{
  "name": "Kitchen",
  "slug": null,
  "description": "Brewing and cooking essentials",
  "isActive": true,
  "sortOrder": 2,
  "parentId": 2
}
```

`409` when:

- a category with child categories is deleted,
- a category still containing products is deleted,
- a category is made its own parent, or moved beneath one of its own descendants.

### `GET /api/admin/orders`

Paged across all customers, optional `status` filter.

### `PUT /api/admin/orders/{id}/status`

```json
{ "status": "Paid" }
```

Transitions are validated server-side:

```
Pending → Paid → Shipped → Delivered
Pending → Cancelled
Paid    → Cancelled
Cancelled is terminal
```

`400` unknown status · `409` illegal transition · `404` unknown order.

### `GET /api/admin/dashboard`

```json
{
  "productCount": 25,
  "categoryCount": 27,
  "customerCount": 2,
  "orderCount": 1,
  "lowStockCount": 3,
  "totalRevenue": 15347.00,
  "recentOrders": [ ]
}
```

Revenue excludes cancelled orders.

---

## Operations

### `GET /health`

```json
{ "status": "Healthy", "totalDurationMs": 0.52 }
```

### `GET /openapi/v1.json`

The generated OpenAPI 3 document.
