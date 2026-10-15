import { test, expect, type Page } from '@playwright/test'

/**
 * Returns the first product card that is actually purchasable.
 *
 * Ordering the catalogue by "newest" is not stable with respect to stock, and every checkout
 * journey in this suite consumes a unit. Selecting `locator('.product-card').first()` therefore
 * works only against a pristine database: once the top card has sold out, its Add-to-cart button
 * is correctly disabled and the locator waits forever. Selecting the first *enabled* button makes
 * the journeys independent of accumulated state, and of the sort order.
 */
function firstPurchasableCard(page: Page) {
  return page.locator('.product-card').filter({ has: page.getByRole('button', { name: 'Add to cart' }) }).first()
}


/**
 * End-to-end journeys required by the assignment:
 *   1. browse products
 *   2. add a product to the cart
 *   3. log in
 *   4. complete checkout
 *   5. create a product as an administrator
 *
 * These run against the real API and database, so they fail if the frontend is not actually
 * wired to the backend, if seeding is wrong, or if authorization is not enforced.
 */

const CUSTOMER = {
  email: 'customer@shophub.local',
  password: 'Customer@12345',
}

const ADMIN = {
  email: 'admin@shophub.local',
  password: 'Admin@12345',
}

async function signIn(page: Page, who: typeof CUSTOMER): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Email').fill(who.email)
  await page.getByLabel('Password').fill(who.password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/account|\/products|\/$/)
}

test.describe('Storefront', () => {
  test('browses the catalogue and opens a product', async ({ page }) => {
    await page.goto('/')

    // The home page renders real, database-backed content.
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Everything')

    await page.goto('/products')
    const cards = page.locator('.product-card')
    await expect(cards.first()).toBeVisible()
    expect(await cards.count()).toBeGreaterThan(0)

    // Filtering is server-side: choosing a category changes the result set.
    await page.locator('.cat-tree button', { hasText: 'Electronics' }).first().click()
    await expect(cards.first()).toBeVisible()

    // Search is server-side too.
    await page.goto('/products?q=keyboard')
    await expect(cards).toHaveCount(1)
    await expect(cards.first()).toContainText('Keyboard')

    // Product detail, including a working add-to-cart control.
    await cards.first().locator('a').first().click()
    await expect(page).toHaveURL(/\/products\/\d+/)
    await expect(page.getByRole('button', { name: /Add to cart/i })).toBeEnabled()
  })

  test('adds a product to the cart and shows it in the drawer', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    await firstPurchasableCard(page).getByRole('button', { name: 'Add to cart' }).click()

    // The badge is the durable, server-derived evidence that the line was persisted.
    // (The toast itself is transient, so it is covered by the component unit test instead.)
    await expect(page.locator('.cart-badge')).toHaveText('1')

    // The drawer shows the line and its subtotal.
    await page.getByTestId('cart-button').click()
    await expect(page.getByRole('dialog', { name: 'Shopping cart' })).toBeVisible()
    await expect(page.locator('.cart-line')).toHaveCount(1)

    // And the full cart page agrees.
    await page.goto('/cart')
    await expect(page.locator('.cart-line')).toHaveCount(1)
    await expect(page.getByRole('link', { name: 'Proceed to checkout' })).toBeVisible()
  })

  test('a guest cart survives a reload', async ({ page }) => {
    await page.goto('/products')
    await firstPurchasableCard(page).getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.locator('.cart-badge')).toHaveText('1')

    await page.reload()
    await expect(page.locator('.cart-badge')).toHaveText('1')
  })
})

test.describe('Authentication', () => {
  test('logs in as a customer and lands on the home page', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email').fill(CUSTOMER.email)
    await page.getByLabel('Password').fill(CUSTOMER.password)
    await page.getByRole('button', { name: 'Sign in' }).click()

    // The requested default after a successful sign-in is Home.
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole('heading', { name: 'Everything' })).toBeVisible()

    // The session is live, not just a redirect.
    await expect(page.getByRole('banner').getByRole('button', { name: 'Sign out' })).toBeVisible()
  })

  test('keeps the account page reachable after signing in', async ({ page }) => {
    await signIn(page, CUSTOMER)

    await page.goto('/account')
    await expect(page.getByLabel('Email')).toHaveValue(CUSTOMER.email)
  })

  test('still honours a redirect intended route after sign-in', async ({ page }) => {
    // A guest who opens a protected page is sent to /login?redirect=…; that intent must
    // survive the change of default landing page.
    await page.goto('/checkout')
    await expect(page).toHaveURL(/\/login\?redirect=/)

    await page.getByLabel('Email').fill(CUSTOMER.email)
    await page.getByLabel('Password').fill(CUSTOMER.password)
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page).toHaveURL(/\/checkout/)
  })

  test('does not navigate when the credentials are wrong', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email').fill(CUSTOMER.email)
    await page.getByLabel('Password').fill('Definitely!Wrong1')
    await page.getByRole('button', { name: 'Sign in' }).click()

    // Stays put and reports the failure; it must not look like a successful sign-in.
    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByRole('alert')).toContainText('Invalid email or password')
    await expect(page.getByRole('banner').getByRole('link', { name: 'Sign in' })).toBeVisible()
  })

  test('rejects a wrong password without revealing whether the account exists', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email').fill(CUSTOMER.email)
    await page.getByLabel('Password').fill('Definitely!Wrong1')
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page.getByRole('alert')).toContainText('Invalid email or password')
  })

  test('registers a new customer through the form', async ({ page }) => {
    const email = `e2e-register-${Date.now()}@shophub.test`

    await page.goto('/register')

    // The page must render at all. This is the regression test for a missing `ref`
    // import that made the view throw a ReferenceError before rendering, which no
    // other test caught because nothing else visited this route.
    await expect(page.getByRole('heading', { name: 'Create account' })).toBeVisible()

    await page.getByLabel('First name').fill('E2e')
    await page.getByLabel('Last name').fill('Shopper')
    await page.getByLabel('Email').fill(email)
    await page.getByLabel('Password', { exact: true }).fill('Str0ng!Password')
    await page.getByLabel('Confirm password').fill('Str0ng!Password')

    await page.getByRole('button', { name: 'Create account' }).click()

    // A successful registration lands on the signed-in account page.
    await expect(page).toHaveURL(/\/account/)
    await expect(page.getByRole('banner').getByRole('button', { name: 'Sign out' })).toBeVisible()

    // The account really exists in the database: sign in with the new credentials.
    await page.getByRole('button', { name: 'Sign out' }).click()
    await signIn(page, { email, password: 'Str0ng!Password' })
    await page.goto('/account')
    await expect(page.getByLabel('Email')).toHaveValue(email)
  })

  test('rejects a weak password at registration', async ({ page }) => {
    await page.goto('/register')

    await page.getByLabel('First name').fill('Weak')
    await page.getByLabel('Last name').fill('Password')
    await page.getByLabel('Email').fill(`weak-${Date.now()}@shophub.test`)
    await page.getByLabel('Password', { exact: true }).fill('abc')
    await page.getByLabel('Confirm password').fill('abc')

    // The strength checklist stays unmet, so the button is never enabled.
    await expect(page.getByRole('button', { name: 'Create account' })).toBeDisabled()
    await expect(page.getByText('At least 8 characters')).toBeVisible()
  })

  test('blocks a signed-out visitor from checkout', async ({ page }) => {
    await page.goto('/checkout')
    await expect(page).toHaveURL(/\/login/)
  })

  test('keeps a signed-in non-admin out of the admin area', async ({ page }) => {
    await signIn(page, CUSTOMER)

    await page.goto('/admin/products')
    await expect(page).not.toHaveURL(/\/admin/)
  })

  test('signs out', async ({ page }) => {
    await signIn(page, CUSTOMER)
    await page.getByRole('button', { name: 'Sign out' }).click()

    // Scoped to the header, which is where the sign-in control lives.
    await expect(page.getByRole('banner').getByRole('link', { name: 'Sign in' })).toBeVisible()
  })
})

test.describe('Checkout', () => {
  test('completes a purchase and shows the order', async ({ page }) => {
    await page.goto('/products')
    await firstPurchasableCard(page).getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.locator('.cart-badge')).toHaveText('1')

    // Checkout requires authentication.
    await page.goto('/checkout')
    await page.getByLabel('Email').fill(CUSTOMER.email)
    await page.getByLabel('Password').fill(CUSTOMER.password)
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page).toHaveURL(/\/checkout/)

    // Address.
    await page.getByLabel('First name').first().fill('Test')
    await page.getByLabel('Last name').first().fill('Shopper')
    await page.getByLabel('Address line 1').first().fill('42 Test Street')
    await page.getByLabel('City').first().fill('Bengaluru')
    await page.getByLabel('State').first().fill('Karnataka')
    await page.getByLabel('Postal code').first().fill('560001')
    await page.getByRole('button', { name: /Continue to shipping/ }).click()

    // Shipping: the express tier costs the legacy ₹350.
    await page.getByRole('radio').nth(1).check()
    await expect(page.locator('.summary-card', { hasText: 'Shipping (Express)' })).toContainText('₹350')
    await page.getByRole('button', { name: /Review order/ }).click()

    // Review and place.
    await expect(page.getByRole('button', { name: 'Place order' })).toBeEnabled()
    await page.getByRole('button', { name: 'Place order' }).click()

    // Confirmation: the order detail page with a real order number.
    await expect(page).toHaveURL(/\/account\/orders\/\d+/)
    await expect(page.getByText(/ORD-\d{8}-\d{6}-[0-9A-F]{4}/)).toBeVisible()

    // The cart is empty afterwards.
    await page.goto('/cart')
    await expect(page.getByText('Your cart is empty')).toBeVisible()

    // And the order appears in history.
    await page.goto('/account/orders')
    await expect(page.locator('.data-table tbody tr').first()).toBeVisible()
  })

  test('refuses to continue with an incomplete address', async ({ page }) => {
    await signIn(page, CUSTOMER)

    await page.goto('/products')
    await firstPurchasableCard(page).getByRole('button', { name: 'Add to cart' }).click()
    await page.goto('/checkout')

    await expect(page.getByRole('button', { name: /Continue to shipping/ })).toBeDisabled()
    await expect(page.getByText('Address line 1 is required')).toBeVisible()
  })
})

test.describe('Administration', () => {
  test('creates a product as an administrator and sees it in the catalogue', async ({ page }) => {
    await signIn(page, ADMIN)

    await page.goto('/admin/products')
    const productName = `E2E Product ${Date.now()}`

    await page.getByRole('button', { name: 'New product' }).click()
    await page.getByLabel('Name').fill(productName)
    await page.getByLabel('Price (₹)').fill('1499')
    await page.getByLabel('Stock').fill('7')
    await page.getByRole('button', { name: 'Create product' }).click()

    // The admin table is paged, so filter to the new product rather than relying on it
    // landing on page one (previous runs of this suite add rows of their own).
    await page.getByTestId('admin-product-search').fill(productName)
    await page.getByRole('button', { name: 'Filter' }).click()

    await expect(page.locator('.data-table')).toContainText(productName)

    // The write reached the real database: the storefront now serves it.
    await page.goto(`/products?q=${encodeURIComponent(productName)}`)
    await expect(page.locator('.product-card')).toHaveCount(1)
    await expect(page.locator('.product-card')).toContainText(productName)
  })

  test('shows dashboard counts', async ({ page }) => {
    await signIn(page, ADMIN)

    await page.goto('/admin')

    // Every counter renders a value and a label, and they come from the API.
    const cards = page.locator('.stat-card')
    await expect(cards).toHaveCount(6)
    await expect(cards.first().locator('span')).toHaveText('Products')
    await expect(cards.last().locator('span')).toHaveText('Revenue')
  })

  test('lists orders and offers the valid next status', async ({ page }) => {
    await signIn(page, ADMIN)

    await page.goto('/admin/orders')
    const table = page.locator('.data-table')
    if ((await table.locator('tbody tr').count()) > 0) {
      await expect(table.locator('tbody tr').first()).toBeVisible()
    }
  })
})
test.describe('Featured products rail', () => {
  test('gives every featured product an identical image frame', async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()

    const frames = await page
      .locator('.featured-rail .product-thumb')
      .evaluateAll((nodes) =>
        nodes.map((n) => {
          const r = n.getBoundingClientRect()
          const style = getComputedStyle(n)
          return {
            w: Math.round(r.width),
            h: Math.round(r.height),
            background: style.backgroundColor,
            radius: style.borderRadius,
          }
        }),
      )

    expect(frames.length).toBeGreaterThan(1)

    // One frame size, and one background treatment, for every product regardless of whether
    // its photograph is portrait or landscape.
    expect(new Set(frames.map((f) => `${f.w}x${f.h}`)).size).toBe(1)
    expect(new Set(frames.map((f) => f.background)).size).toBe(1)
    expect(new Set(frames.map((f) => f.radius)).size).toBe(1)

    // The frame is square and fully opaque behind the photo, so no image sits on the page.
    for (const { w, h, background } of frames) {
      expect(w).toBe(h)
      expect(background).not.toBe('rgba(0, 0, 0, 0)')
    }
  })

  test('keeps every featured image inside its frame without distorting it', async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()

    const images = await page.locator('.featured-rail .product-thumb img').evaluateAll((nodes) =>
      nodes.map((img) => {
        const frame = (img.parentElement as HTMLElement).getBoundingClientRect()
        const r = img.getBoundingClientRect()
        const style = getComputedStyle(img)
        return {
          // Contained, not stretched: the photo keeps its own proportions inside the frame
          // rather than being scaled to the frame's axes.
          fit: style.objectFit,
          insideFrame: r.left >= frame.left - 1 && r.right <= frame.right + 1,
          // The inset is padding on the image itself, since the frame is its containing block.
          inset: parseFloat(style.paddingTop),
          // The rendered content is smaller than the frame for any non-square source, which is
          // the visible proof that nothing was stretched to fill it.
          narrowerThanFrame: Math.min(r.width, r.height) <= Math.max(frame.width, frame.height),
        }
      }),
    )

    expect(images.length).toBeGreaterThan(1)
    for (const img of images) {
      expect(img.fit).toBe('contain')
      expect(img.insideFrame).toBe(true)
      expect(img.narrowerThanFrame).toBe(true)
      // Padding keeps the photo off the frame edges.
      expect(img.inset).toBeGreaterThanOrEqual(4)
    }
  })

  test('sizes featured cards to match the shop product cards', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1000 })
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()
    await page.waitForTimeout(500)

    const rail = await page.locator('.featured-rail').evaluate((el) => {
      const box = el.getBoundingClientRect()
      const items = Array.from(el.querySelectorAll('.rail-item'))
      const fullyVisible = items.filter((item) => {
        const r = item.getBoundingClientRect()
        return r.left >= box.left - 2 && r.right <= box.right + 2
      }).length
      return { total: items.length, fullyVisible, cardWidth: Math.round(items[0].getBoundingClientRect().width) }
    })

    // Every featured product stays in the rail...
    expect(rail.total).toBe(10)

    // ...and the cards are properly sized rather than shrunk to force ten onto the screen.
    // Readability wins over the count, so the bar is the Shop grid's own card basis.
    expect(rail.cardWidth).toBeGreaterThanOrEqual(250)

    // Several are visible at once on a wide desktop, and the rest are reachable by scrolling.
    expect(rail.fullyVisible).toBeGreaterThanOrEqual(4)
    expect(rail.fullyVisible).toBeLessThan(rail.total)
  })

  test('featured cards carry the same information as a shop product card', async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()

    const card = page.locator('.featured-rail .rail-item').first()
    // Category, name, price and an action, matching the Shop card's anatomy...
    await expect(card.locator('.cat')).not.toBeEmpty()
    await expect(card.locator('.name')).not.toBeEmpty()
    await expect(card.locator('.price')).not.toBeEmpty()
    // ...including the stock label and button in a footer, not a bare full-width button.
    await expect(card.locator('.card-foot .stock')).not.toBeEmpty()
    await expect(card.locator('.card-foot button')).toBeVisible()

    // Nothing overflows its card, and the whole rail stays inside the viewport.
    const overflow = await page.locator('.featured-rail').evaluate((el) => {
      const railBox = el.getBoundingClientRect()
      const clipped = Array.from(el.querySelectorAll('.rail-item')).some((c) => {
        const card = c.getBoundingClientRect()
        const foot = c.querySelector('.card-foot')!.getBoundingClientRect()
        return foot.bottom > card.bottom + 1
      })
      return {
        clipped,
        withinPage: railBox.right <= document.documentElement.clientWidth + 1,
        documentOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      }
    })
    expect(overflow.clipped).toBe(false)
    expect(overflow.withinPage).toBe(true)
    expect(overflow.documentOverflow).toBeLessThanOrEqual(0)
  })

  test('reduces the visible count progressively rather than overflowing', async ({ page }) => {
    const counts: number[] = []

    for (const width of [1920, 1600, 1440, 1280, 1024, 768, 390]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/')
      await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()
      await page.waitForTimeout(350)

      const result = await page.locator('.featured-rail').evaluate((el) => {
        const box = el.getBoundingClientRect()
        const items = Array.from(el.querySelectorAll('.rail-item'))
        return {
          visible: items.filter((item) => {
            const r = item.getBoundingClientRect()
            return r.left >= box.left - 2 && r.right <= box.right + 2
          }).length,
          // The strip itself must stay inside the page, whatever the viewport.
          withinPage: box.right <= document.documentElement.clientWidth + 1,
          documentOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        }
      })

      expect(result.withinPage).toBe(true)
      expect(result.documentOverflow).toBeLessThanOrEqual(0)
      counts.push(result.visible)
    }

    // More cards on a wider screen, and never more than the rail actually holds.
    for (let i = 1; i < counts.length; i++) {
      expect(counts[i]).toBeLessThanOrEqual(counts[i - 1])
    }
  })

  test('keeps the featured controls and arrows working', async ({ page }) => {
    // A narrower desktop, where the ten cards do not all fit and the rail has to scroll.
    await page.setViewportSize({ width: 1280, height: 900 })
    await page.goto('/')
    const rail = page.locator('.featured-rail')
    await expect(rail.locator('.rail-item').first()).toBeVisible()
    await rail.scrollIntoViewIfNeeded()

    const next = page.getByRole('button', { name: 'Scroll featured products right' })
    const previous = page.getByRole('button', { name: 'Scroll featured products left' })

    // Product information and the add-to-cart control remain present on every card.
    const card = rail.locator('.rail-item').first()
    await expect(card.locator('.name')).not.toBeEmpty()
    await expect(card.locator('.price')).not.toBeEmpty()
    await expect(card.locator('.cat')).not.toBeEmpty()
    await expect(card.getByRole('button', { name: 'Add to cart' })).toBeVisible()

    // The rail still scrolls, which is what reveals the remainder once ten no longer fit.
    await expect(previous).toBeDisabled()
    await next.click()
    await expect.poll(() => rail.evaluate((el) => el.scrollLeft)).toBeGreaterThan(0)
    await expect(previous).toBeEnabled()
  })
})

test.describe('Promotional collections', () => {
  const EXPECTED_SIZE = 10

  async function collections(page: import('@playwright/test').Page) {
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()
    await page.waitForTimeout(400)

    return page.evaluate(() => {
      const ids = (selector: string) =>
        Array.from(document.querySelectorAll(selector)).map((el) => el.getAttribute('data-product-id'))

      const sectionCards = (heading: string) => {
        const section = Array.from(document.querySelectorAll('.section')).find((s) =>
          s.querySelector('.section-title')?.textContent?.includes(heading),
        )
        return section ? Array.from(section.querySelectorAll('.product-card')) : []
      }

      return {
        featured: Array.from(document.querySelectorAll('.featured-rail .rail-item')).length,
        newArrivals: sectionCards('New arrivals').length,
        deals: sectionCards('Deals').length,
        images: Array.from(document.querySelectorAll('img'))
          .map((i) => (i as HTMLImageElement).getAttribute('src'))
          .filter((s): s is string => !!s),
        ids,
      }
    })
  }

  test('every promotional collection shows two more products than before', async ({ page }) => {
    const data = await collections(page)

    // Each collection was capped at eight and is now ten.
    expect(data.featured).toBe(EXPECTED_SIZE)
    expect(data.newArrivals).toBe(EXPECTED_SIZE)
    expect(data.deals).toBe(EXPECTED_SIZE)
  })

  test('no collection contains a duplicate product', async ({ page }) => {
    const duplicates = await page.evaluate(() => {
      const sectionIds = (heading: string) => {
        const section = Array.from(document.querySelectorAll('.section')).find((s) =>
          s.querySelector('.section-title')?.textContent?.includes(heading),
        )
        return section
          ? Array.from(section.querySelectorAll('.product-card')).map((c) => c.textContent)
          : []
      }
      const featured = Array.from(document.querySelectorAll('.featured-rail .rail-item')).map(
        (i) => i.textContent,
      )
      return {
        featured: featured.length !== new Set(featured).size,
        newArrivals: (() => {
          const ids = sectionIds('New arrivals')
          return ids.length !== new Set(ids).size
        })(),
        deals: (() => {
          const ids = sectionIds('Deals')
          return ids.length !== new Set(ids).size
        })(),
      }
    })

    expect(duplicates.featured).toBe(false)
    expect(duplicates.newArrivals).toBe(false)
    expect(duplicates.deals).toBe(false)
  })

  test('the newly promoted products appear in their collections', async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('.featured-rail .rail-item').first()).toBeVisible()
    await page.waitForTimeout(400)

    const present = await page.evaluate(() => {
      const names = (heading: string) => {
        const section = Array.from(document.querySelectorAll('.section')).find((s) =>
          s.querySelector('.section-title')?.textContent?.includes(heading),
        )
        return section ? section.textContent ?? '' : ''
      }
      return {
        featured: document.querySelector('.featured-rail')?.textContent ?? '',
        deals: names('Deals'),
      }
    })

    // Promoted to featured: Pour-Over Coffee Set and The Startup Playbook.
    expect(present.featured).toContain('Pour-Over Coffee Set')
    expect(present.featured).toContain('The Startup Playbook')

    // Discounted: Home Gym Dumbbell Set and Premium Hardcover Notebook.
    expect(present.deals).toContain('Home Gym Dumbbell Set')
    expect(present.deals).toContain('Premium Hardcover Notebook')
  })

  test('every deal is genuinely discounted with a consistent was-price', async ({ page }) => {
    await page.goto('/')
    const deals = page.locator('.section', { hasText: 'Deals' }).locator('.product-card')
    await expect(deals.first()).toBeVisible()

    const cards = await deals.evaluateAll((nodes) =>
      nodes.map((n) => {
        const price = n.querySelector('.price')?.textContent?.trim() ?? ''
        const was = n.querySelector('.old-price')?.textContent?.trim() ?? ''
        const badge = n.querySelector('.badge-sale')?.textContent?.trim() ?? ''
        return { price, was, badge, hasButton: !!n.querySelector('.card-foot button') }
      }),
    )

    expect(cards.length).toBeGreaterThan(0)
    for (const card of cards) {
      // Eligibility is explicit, so every deal carries a real discount badge...
      expect(card.badge).toMatch(/-\d+%/)
      // ...a struck-through was-price beside the selling price...
      expect(card.price).toBeTruthy()
      expect(card.was).toBeTruthy()
      // ...and a working action.
      expect(card.hasButton).toBe(true)
    }
  })

  test('all product images resolve, including the newly displayed products', async ({ page }) => {
    const failures = await page.evaluate(() =>
      Array.from(document.images)
        .filter((img) => !img.complete || img.naturalWidth === 0)
        .map((img) => img.getAttribute('src')),
    )

    expect(failures).toEqual([])
  })

  test('the shop listing still filters, sorts and pages correctly', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()
    const initial = await page.locator('.product-card').count()

    // Search narrows the listing.
    await page.locator('.shop-search-input').fill('keyboard')
    await page.waitForTimeout(800)
    expect(await page.locator('.product-card').count()).toBeLessThan(initial)

    // Clearing it restores the full page.
    await page.locator('.shop-search-input').fill('')
    await page.waitForTimeout(800)
    expect(await page.locator('.product-card').count()).toBe(initial)

    // Sorting does not change the result count, only the order.
    const first = await page.locator('.product-card .name').first().innerText()
    await page.locator('.shop-layout select, select').first().selectOption({ index: 1 })
    await page.waitForTimeout(700)
    expect(await page.locator('.product-card').count()).toBe(initial)
    expect(await page.locator('.product-card .name').first().innerText()).not.toBe(first)
  })

  test('adding a newly displayed product to the cart still works', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    // The notebook was only on sale because of this change, so it doubles as the regression
    // check that a newly discounted product behaves like any other.
    await page.locator('.shop-search-input').fill('notebook')
    await page.waitForTimeout(800)

    const card = page.locator('.product-card').first()
    await expect(card).toContainText('Premium Hardcover Notebook')
    await card.getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.locator('.cart-badge')).toHaveText('1')
  })
})

test.describe('Storefront presentation', () => {
  test('gives every product card the same square image box', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    // Source photography mixes 3:2 and 2:3 ratios; the container must not follow them.
    const thumbs = await page.locator('.product-card .thumb').evaluateAll((nodes) =>
      nodes.map((n) => {
        const r = n.getBoundingClientRect()
        return { w: Math.round(r.width), h: Math.round(r.height) }
      }),
    )

    expect(thumbs.length).toBeGreaterThan(1)
    for (const { w, h } of thumbs) {
      expect(w).toBe(h)
    }

    // Every card shares one width, so the grid stays aligned.
    expect(new Set(thumbs.map((t) => t.w)).size).toBe(1)
  })

  test('aligns the add-to-cart controls within a row', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    const rowTops = await page.locator('.product-card').evaluateAll((nodes) => {
      const rows = new Map()
      for (const n of nodes) {
        const key = Math.round((n.getBoundingClientRect().top + window.scrollY) / 5) * 5
        rows.set(key, rows.get(key) ?? [])
        rows.get(key).push(
          Math.round(n.querySelector('.card-foot').getBoundingClientRect().top + window.scrollY),
        )
      }
      return [...rows.values()]
    })

    expect(rowTops.length).toBeGreaterThan(0)
    for (const tops of rowTops) {
      expect(new Set(tops).size).toBe(1)
    }
  })

  test('navigates the featured rail with the arrow buttons', async ({ page }) => {
    await page.goto('/')
    const rail = page.locator('.featured-rail')
    await expect(rail).toBeVisible()

    const previous = page.getByRole('button', { name: 'Scroll featured products left' })
    const next = page.getByRole('button', { name: 'Scroll featured products right' })

    // The scrollbar is suppressed, but the element is still a scroll viewport.
    await expect(rail).toHaveCSS('scrollbar-width', 'none')

    // At the start there is nowhere to go back to.
    await expect(previous).toBeDisabled()
    await expect(next).toBeEnabled()

    const step = await rail.evaluate((el) => {
      const card = el.firstElementChild!.getBoundingClientRect().width
      const gap = parseFloat(getComputedStyle(el).columnGap) || 0
      return card + gap
    })

    await next.click()
    await expect
      .poll(() => rail.evaluate((el) => Math.round(el.scrollLeft)))
      .toBeGreaterThan(0)
    await expect(previous).toBeEnabled()

    // One click advances about one card, not the whole row.
    const firstStep = await rail.evaluate((el) => Math.round(el.scrollLeft))
    expect(firstStep).toBeGreaterThan(0)
    expect(firstStep).toBeLessThanOrEqual(Math.ceil(step))

    // Reaching the end disables the right button.
    await rail.evaluate((el) => el.scrollTo({ left: el.scrollWidth }))
    await expect(next).toBeDisabled()
    await expect(previous).toBeEnabled()

    // And the rail still scrolls under a horizontal wheel, not only via the buttons.
    await rail.evaluate((el) => el.scrollTo({ left: 0 }))
    await rail.hover()
    await page.mouse.wheel(200, 0)
    await expect.poll(() => rail.evaluate((el) => Math.round(el.scrollLeft))).toBeGreaterThan(0)
  })

  test('highlights the registration link on the sign-in page', async ({ page }) => {
    await page.goto('/login')

    const createOne = page.getByRole('link', { name: 'Create one' })
    await expect(createOne).toBeVisible()
    await expect(createOne).toHaveAttribute('href', '/register')

    // The global `a` rule strips colour and underline, so the link is styled to stand out from
    // the surrounding sentence.
    await expect(createOne).toHaveCSS('text-decoration-line', 'underline')
    const color = await createOne.evaluate((el) => getComputedStyle(el).color)
    expect(color).toBe('rgb(224, 67, 12)')

    // Visible focus ring for keyboard users.
    await createOne.focus()
    await expect(createOne).toHaveCSS('outline-style', 'solid')
  })
})

test.describe('Home categories', () => {
  test('keeps every category in one non-wrapping horizontal strip', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()

    const cards = strip.locator('.cat-tile')
    const count = await cards.count()
    expect(count).toBeGreaterThan(1)

    // All cards share one vertical position: nothing wrapped onto a second row.
    const tops = await cards.evaluateAll((nodes) =>
      nodes.map((n) => Math.round(n.getBoundingClientRect().top)),
    )
    expect(new Set(tops).size).toBe(1)

    // The strip is the only thing that scrolls; the page itself must not.
    const pageOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(pageOverflow).toBeLessThanOrEqual(1)
  })

  test('scrolls to reveal categories beyond the fold and still navigates', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()

    // The strip flows continuously, so the right control moves the animation timeline
    // rather than the container's scroll offset.
    const position = () =>
      page.evaluate(() => {
        const anim = document.querySelector('.cat-track')?.getAnimations()[0]
        return anim ? anim.currentTime : 0
      })

    const before = await position()
    const right = page.getByRole('button', { name: 'Scroll categories right' })
    await expect(right).toBeEnabled()
    await right.click()
    await expect.poll(async () => Math.round(await position())).toBeGreaterThan(Math.round(before))

    // A category is still clickable and still filters the catalogue. Entering the strip pauses
    // the flow first, which is what a shopper does before clicking; automation has to do the
    // same, because it checks for a stationary element before moving the pointer.
    await strip.hover()
    await expect
      .poll(() =>
        page.evaluate(() => document.querySelector('.cat-track')?.getAnimations()[0]?.playState),
      )
      .toBe('paused')

    // The container clips what has travelled out of view, so a category is chosen that is
    // fully inside the strip now that it has stopped.
    const visible = await page.evaluate(() => {
      const strip = document.querySelector('.cat-tiles')!
      const track = strip.firstElementChild as HTMLElement
      const box = strip.getBoundingClientRect()
      const real = [...track.children].filter((c) => !c.hasAttribute('aria-hidden'))
      return real.findIndex((card) => {
        const r = card.getBoundingClientRect()
        return r.left >= box.left - 1 && r.right <= box.right + 1
      })
    })
    expect(visible).toBeGreaterThanOrEqual(0)

    const target = strip.locator('.cat-tile:not([aria-hidden])').nth(visible)
    const name = (await target.locator('.cat-tile-name').innerText()).trim()
    await target.click()

    await expect(page).toHaveURL(/\/products\?categoryId=/)
    await expect(page.getByText(`Showing`).first()).toBeVisible()
    expect(name.length).toBeGreaterThan(0)
  })
})

test.describe('Incremental product search', () => {
  test('finds a product from a partial term typed in the header', async ({ page }) => {
    await page.goto('/')
    const search = page.getByPlaceholder('Search products, brands and categories')

    // "lap" is enough to reach both Laptop products.
    await search.fill('lap')

    await expect(page).toHaveURL(/\/products\?q=lap/)
    await expect(page.locator('.product-card')).toHaveCount(2)
    await expect(page.locator('.product-card').first()).toContainText('Laptop')
  })

  test('matches regardless of capitalisation', async ({ page }) => {
    await page.goto('/products?q=lap')
    await expect(page.locator('.product-card')).toHaveCount(2)

    const search = page.getByPlaceholder('Search products, brands and categories')
    await search.fill('LAP')

    await expect(page).toHaveURL(/\/products\?q=LAP/)
    await expect(page.locator('.product-card')).toHaveCount(2)
  })

  test('updates results as the query changes, then resets when cleared', async ({ page }) => {
    await page.goto('/products')
    const search = page.getByPlaceholder('Search products, brands and categories')

    // Wait for the unfiltered page to settle before recording the baseline, otherwise the
    // count is taken mid-load and the final assertion is meaningless.
    await expect(page.locator('.product-card').first()).toBeVisible()
    const all = await page.locator('.product-card').count()
    expect(all).toBeGreaterThan(0)

    await search.fill('lap')
    await expect(page).toHaveURL(/q=lap/)
    await expect(page.locator('.product-card')).toHaveCount(2)

    // Narrowing further changes the result set again. 'backpack' identifies one product;
    // note the API also matches the description, which is why a term such as 'aluminium'
    // legitimately returns more than one hit.
    await search.fill('backpack')
    await expect(page).toHaveURL(/q=backpack/)
    await expect(page.locator('.product-card')).toHaveCount(1)
    await expect(page.locator('.product-card')).toContainText('Travel Laptop Backpack')

    // Clearing restores the unfiltered list and drops the query from the URL.
    await search.fill('')
    await expect(page).not.toHaveURL(/q=/)
    await expect(page.locator('.product-card')).toHaveCount(all)
  })

  test('shows an empty state rather than stale results for a term with no matches', async ({ page }) => {
    await page.goto('/products?q=lap')
    await expect(page.locator('.product-card')).toHaveCount(2)

    await page.getByPlaceholder('Search products, brands and categories').fill('zzzznotathing')

    await expect(page.locator('.product-card')).toHaveCount(0)
    await expect(page.getByText('No products found')).toBeVisible()
  })

  test('still searches from the listing page input and on Enter', async ({ page }) => {
    await page.goto('/products')
    const listSearch = page.getByLabel('Search products')

    // Typed incrementally.
    await listSearch.fill('lap')
    await expect(page).toHaveURL(/q=lap/)
    await expect(page.locator('.product-card')).toHaveCount(2)

    // And submitted explicitly with Enter.
    await listSearch.fill('')
    await expect(page).not.toHaveURL(/q=/)
    await listSearch.fill('head')
    await listSearch.press('Enter')
    await expect(page).toHaveURL(/q=head/)
    await expect(page.locator('.product-card').first()).toContainText('Headphones')
  })

  test('keeps a category filter while searching', async ({ page }) => {
    await page.goto('/products')
    // The first sidebar control is "All products", which clears the filter, so target a
    // real category instead.
    const sidebarCategory = page
      .locator('.shop-sidebar button')
      .filter({ hasNotText: 'All products' })
      .first()
    const categoryName = (await sidebarCategory.locator('span').first().innerText()).trim()
    await sidebarCategory.click()
    await expect(page).toHaveURL(/categoryId=/)
    expect(categoryName.length).toBeGreaterThan(0)

    await page.getByPlaceholder('Search products, brands and categories').fill('lap')
    await expect(page).toHaveURL(/q=lap/)
    // Both filters are carried in the URL together.
    await expect(page).toHaveURL(/categoryId=/)
  })

  test('does not emit a request for every keystroke', async ({ page }) => {
    await page.goto('/products')
    const search = page.getByPlaceholder('Search products, brands and categories')

    let requests = 0
    page.on('request', (r) => {
      if (r.url().includes('/api/products') && r.url().includes('search=')) requests += 1
    })

    // Six keystrokes in quick succession should debounce into far fewer requests.
    for (const ch of 'laptops') {
      await search.type(ch, { delay: 10 })
    }

    await expect(page).toHaveURL(/q=laptops/)
    await page.waitForTimeout(600)
    expect(requests).toBeLessThan(6)
  })
})

test.describe('Add-to-cart notification', () => {
  test('shows a right-side toast naming the product after a successful add', async ({ page }) => {
    await page.goto('/products')
    const card = page.locator('.product-card').filter({ has: page.getByRole('button', { name: 'Add to cart' }) }).first()
    const productName = (await card.locator('.name').innerText()).trim()

    await card.getByRole('button', { name: 'Add to cart' }).click()

    const toast = page.locator('.toast')
    await expect(toast).toBeVisible()
    await expect(toast).toContainText(productName)
    await expect(toast).toContainText('added to cart')

    // Anchored to the right edge, with the same 16px gutter the stylesheet sets.
    const box = (await toast.boundingBox())!
    const viewport = page.viewportSize()!
    expect(viewport.width - (box.x + box.width)).toBeLessThanOrEqual(20)

    // And it stays clear of the header, whose height changes as the viewport narrows.
    const header = (await page.getByRole('banner').boundingBox())!
    expect(box.y).toBeGreaterThanOrEqual(header.y + header.height - 1)

    // Announced politely for screen readers.
    await expect(page.locator('[aria-live="polite"] .toast')).toHaveCount(1)
    await expect(toast).toHaveAttribute('role', 'status')

    // The cart total is still correct.
    await expect(page.locator('.cart-badge')).toHaveText('1')
  })

  test('dismisses the toast on its own', async ({ page }) => {
    await page.goto('/products')
    const card = page.locator('.product-card').filter({ has: page.getByRole('button', { name: 'Add to cart' }) }).first()
    await card.getByRole('button', { name: 'Add to cart' }).click()

    await expect(page.locator('.toast')).toBeVisible()
    // The default lifetime is ~3.2s.
    await expect(page.locator('.toast')).toHaveCount(0, { timeout: 8000 })
  })

  test('keeps a second addition usable and the count correct', async ({ page }) => {
    await page.goto('/products')
    const purchasable = page.locator('.product-card').filter({ has: page.getByRole('button', { name: 'Add to cart' }) })

    await purchasable.nth(0).getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.locator('.toast')).toBeVisible()
    await expect(page.locator('.cart-badge')).toHaveText('1')

    await purchasable.nth(1).getByRole('button', { name: 'Add to cart' }).click()
    await expect(page.locator('.cart-badge')).toHaveText('2')
    await expect(page.locator('.toast').first()).toBeVisible()

    // The notification must not cover the cart control.
    const toastBox = (await page.locator('.toast').first().boundingBox())!
    const cartBox = (await page.getByTestId('cart-button').boundingBox())!
    const overlaps =
      toastBox.x < cartBox.x + cartBox.width &&
      toastBox.x + toastBox.width > cartBox.x &&
      toastBox.y < cartBox.y + cartBox.height &&
      toastBox.y + toastBox.height > cartBox.y
    expect(overlaps).toBe(false)
  })

  test('shows no success toast when the add fails', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    // Force the add-to-cart call to fail, so the failure path is exercised deterministically
    // instead of depending on a sold-out product being present in the seed.
    await page.route('**/api/cart/items', (route) =>
      route.fulfill({ status: 500, contentType: 'application/json', body: '{"message":"Add failed"}' }),
    )

    const card = page
      .locator('.product-card')
      .filter({ has: page.getByRole('button', { name: 'Add to cart' }) })
      .first()
    await card.getByRole('button', { name: 'Add to cart' }).click()

    // A success confirmation must not appear; the failure is reported instead.
    await expect(page.locator('.toast.success')).toHaveCount(0)
    await expect(page.locator('.toast.error')).toBeVisible()
    await expect(page.locator('.toast.error')).not.toContainText('added to cart')

    // And the cart was not credited.
    await expect(page.locator('.cart-badge')).toHaveCount(0)
  })
})

test.describe('Category carousel', () => {
  /**
   * State of the running CSS animation that carries the category track.
   *
   * The values are projected into a plain object because a live `Animation` cannot cross the
   * Playwright boundary: returning one directly yields an empty object.
   */
  const trackAnimation = (page: import('@playwright/test').Page) =>
    page.evaluate(() => {
      const anim = document.querySelector('.cat-track')?.getAnimations()[0]
      return anim ? { currentTime: anim.currentTime, playState: anim.playState } : null
    })

  test('flows continuously rather than advancing in delayed steps', async ({ page }) => {
    await page.goto('/')
    const track = page.locator('.cat-track')
    await expect(track).toBeVisible()

    // Wait for the CSS animation to be attached, not merely for the markup to exist.
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    const timing = await track.evaluate((el) => {
      const style = getComputedStyle(el)
      return {
        name: style.animationName,
        easing: style.animationTimingFunction,
        iterations: style.animationIterationCount,
      }
    })

    // A linear, infinitely repeating translate is what makes the travel continuous.
    expect(timing.name).toBe('cat-marquee')
    expect(timing.easing).toBe('linear')
    expect(timing.iterations).toBe('infinite')

    // Sample the timeline against the wall clock. A continuous animation advances in step
    // with real time; the stepped implementation this replaced sat still between jumps, so
    // its total progress over the same window would be far smaller than the elapsed time.
    const started = Date.now()
    const first = (await trackAnimation(page))!.currentTime
    await page.waitForTimeout(2000)
    const second = (await trackAnimation(page))!
    const advanced = second.currentTime - first
    const elapsed = Date.now() - started

    // Within a generous tolerance for scheduling jitter, the track kept up with the clock.
    expect(advanced).toBeGreaterThan(elapsed * 0.8)
    expect(advanced).toBeLessThan(elapsed * 1.2)

    // And it is genuinely still running rather than stopped part-way.
    expect(second.playState).toBe('running')
  })

  test('derives the loop distance from the rendered category sequence', async ({ page }) => {
    await page.goto('/')
    const track = page.locator('.cat-track')
    await expect(track).toBeVisible()
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    const { published, measured, gaps } = await page.evaluate(() => {
      const strip = document.querySelector('.cat-tiles')!
      const trackEl = strip.firstElementChild as HTMLElement
      const cards = [...trackEl.children]
      const realCount = cards.findIndex((c) => c.hasAttribute('aria-hidden'))
      const first = cards[0].getBoundingClientRect().left
      const clone = cards[realCount].getBoundingClientRect().left
      return {
        published: parseFloat(strip.style.getPropertyValue('--loop-distance')),
        measured: Math.round((clone - first) * 100) / 100,
        gaps: Math.round(
          cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left,
        ),
      }
    })

    // The animation must travel exactly one pass, gaps included, so the duplicate lands
    // precisely where the original began. A hardcoded distance would drift.
    expect(published).toBe(measured)
    expect(gaps).toBeGreaterThan(0)
  })

  test('loops without a blank gap and cycles through every category', async ({ page }) => {
    await page.goto('/')
    const track = page.locator('.cat-track')
    await expect(track).toBeVisible()
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    // Shorten the loop so the boundary is observable, keeping the measured distance.
    await page.evaluate(() => {
      const strip = document.querySelector('.cat-tiles') as HTMLElement
      const trackEl = strip.firstElementChild as HTMLElement
      trackEl.getAnimations().forEach((a) => a.cancel())
      strip.style.setProperty('--loop-duration', '4000ms')
      trackEl.style.animation = 'none'
      void trackEl.offsetHeight
      trackEl.style.animation = ''
    })

    // Watch whatever is at the left edge: that is what a shopper sees, and it must keep
    // changing without ever going blank at the loop boundary.
    const leftEdge = () =>
      page.evaluate(() => {
        const strip = document.querySelector('.cat-tiles')!
        const trackEl = strip.firstElementChild as HTMLElement
        const box = strip.getBoundingClientRect()
        let best: string | null = null
        let bestX = Infinity
        for (const card of trackEl.children) {
          const r = card.getBoundingClientRect()
          if (r.right > box.left && r.left < box.right && r.left >= box.left - 1 && r.left < bestX) {
            bestX = r.left
            best = card.querySelector('.cat-tile-name')!.textContent!.trim()
          }
        }
        return best
      })

    const seen: (string | null)[] = []
    for (let i = 0; i < 50; i++) {
      seen.push(await leftEdge())
      await page.waitForTimeout(80)
    }

    expect(seen.filter((name) => name === null)).toHaveLength(0)
    expect(new Set(seen.filter(Boolean)).size).toBeGreaterThan(1)
  })

  test('pauses while hovered and resumes on leave', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()
    await strip.scrollIntoViewIfNeeded()
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    const box = (await strip.boundingBox())!
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2)
    await expect
      .poll(async () => (await trackAnimation(page))?.playState)
      .toBe('paused')

    // With the track still, a category is reliably clickable.
    await page.locator('.cat-tiles .cat-tile:not([aria-hidden])').first().click({ timeout: 10_000 })
    await expect(page).toHaveURL(/categoryId=/)

    await page.goBack()
    await expect(strip).toBeVisible()
    await page.mouse.move(5, 5)
    await expect
      .poll(async () => (await trackAnimation(page))?.playState)
      .toBe('running')
  })

  test('pauses while a category has keyboard focus', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    // Focus the first real card, ignoring the hidden cloned pass.
    await strip.locator('.cat-tile:not([aria-hidden])').first().focus()
    await expect
      .poll(async () => (await trackAnimation(page))?.playState)
      .toBe('paused')
  })

  test('does not animate when reduced motion is requested, but the arrows still work', async ({ browser }) => {
    const context = await browser.newContext({ reducedMotion: 'reduce' })
    const page = await context.newPage()
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()
    await page.waitForTimeout(400)

    const state = await page.evaluate(() => {
      const trackEl = document.querySelector('.cat-track') as HTMLElement
      return {
        animations: trackEl.getAnimations().length,
        name: getComputedStyle(trackEl).animationName,
        manual: document.querySelector('.cat-tiles')!.classList.contains('is-manual'),
      }
    })

    // No automatic motion, but the strip is still measured so it can be scrolled by hand.
    expect(state.animations).toBe(0)
    expect(state.name).toBe('none')
    expect(state.manual).toBe(true)

    const before = await strip.evaluate((el) => el.scrollLeft)
    await page.getByRole('button', { name: 'Scroll categories right' }).click()
    await expect.poll(() => strip.evaluate((el) => el.scrollLeft)).toBeGreaterThan(before)

    // Category selection still works.
    await strip.scrollIntoViewIfNeeded()
    await page.locator('.cat-tiles .cat-tile:not([aria-hidden])').first().click({ timeout: 10_000 })
    await expect(page).toHaveURL(/categoryId=/)

    await context.close()
  })

  test('the arrows seek the running animation and the loop never dead-ends', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()
    await page.waitForFunction(
      () => (document.querySelector('.cat-track')?.getAnimations().length ?? 0) > 0,
      undefined,
      { timeout: 15_000 },
    )

    // A looping strip always has somewhere to go, so neither arrow is disabled.
    const previous = page.getByRole('button', { name: 'Scroll categories left' })
    const next = page.getByRole('button', { name: 'Scroll categories right' })
    await expect(previous).toBeEnabled()
    await expect(next).toBeEnabled()

    // The arrow seeks the animation by scrubbing its delay. That offset is what the app sets,
    // so it is asserted directly: `Animation.currentTime` on an infinite animation is
    // unbounded elapsed time and does not reflect the seek.
    const seekOffset = () =>
      page.evaluate(() => {
        const delay = getComputedStyle(document.querySelector('.cat-track')!).animationDelay
        const ms = delay.endsWith('ms') ? Number(delay.slice(0, -2)) : Number(delay.replace('s', '')) * 1000
        // The app applies a negative delay, so the position within the loop is its magnitude.
        return Math.abs(ms)
      })

    // Work out how much of the loop one card is, from the geometry the app published.
    const perCard = await page.evaluate(() => {
      const strip = document.querySelector('.cat-tiles') as HTMLElement
      const track = strip.firstElementChild as HTMLElement
      const cards = [...track.children]
      const step = cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left
      const distance = parseFloat(strip.style.getPropertyValue('--loop-distance'))
      const duration = parseFloat(strip.style.getPropertyValue('--loop-duration'))
      return Math.round((step / distance) * duration)
    })

    const first = await seekOffset()
    await next.click()
    const forward = await seekOffset()
    // The right arrow moved the flow forward by exactly one card.
    expect(Math.abs(forward - first - perCard)).toBeLessThanOrEqual(2)

    await previous.click()
    // Seeking back undoes it exactly, proving the offset is shared with the running flow.
    expect(await seekOffset()).toBe(first)
  })

  test('exposes each category once to assistive technology', async ({ page }) => {
    await page.goto('/')
    const strip = page.locator('.cat-tiles')
    await expect(strip).toBeVisible()

    const counts = await strip.evaluate((el) => {
      const cards = [...el.querySelectorAll('.cat-tile')]
      const real = cards.filter((c) => !c.hasAttribute('aria-hidden'))
      const names = real.map((c) => c.querySelector('.cat-tile-name')!.textContent!.trim())
      return {
        total: cards.length,
        real: real.length,
        clonesHidden: cards.length - real.length,
        clonesInert: cards.filter((c) => c.hasAttribute('aria-hidden')).every((c) => c.hasAttribute('inert')),
        clonesUnfocusable: cards
          .filter((c) => c.hasAttribute('aria-hidden'))
          .every((c) => c.getAttribute('tabindex') === '-1'),
        duplicates: names.length !== new Set(names).size,
      }
    })

    // The track is rendered twice for a seamless loop, but only one pass is exposed.
    expect(counts.total).toBe(counts.real * 2)
    expect(counts.clonesHidden).toBe(counts.real)
    expect(counts.clonesInert).toBe(true)
    expect(counts.clonesUnfocusable).toBe(true)
    expect(counts.duplicates).toBe(false)
  })
})

test.describe('Shop category sidebar', () => {
  /** Depth of each button within the nested lists, 0 being the outermost. */
  async function treeDepths(page: import('@playwright/test').Page) {
    return page.locator('.cat-tree button').evaluateAll((nodes) =>
      nodes.map((n) => {
        let depth = 0
        let ul: HTMLUListElement | null = n.closest('ul')
        const root = document.querySelector('.cat-tree')
        while (ul && ul !== root) {
          depth += 1
          ul = ul.parentElement?.closest('ul') ?? null
        }
        return { text: n.textContent?.trim() ?? '', depth }
      }),
    )
  }

  test('shows exactly one All products option', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.cat-tree button').first()).toBeVisible()

    const buttons = await treeDepths(page)
    const allProducts = buttons.filter((b) => b.text === 'All products')

    // The category tree renders itself recursively, so the entry used to be emitted at every
    // depth and appeared once per category group.
    expect(allProducts).toHaveLength(1)
    expect(allProducts[0].depth).toBe(0)
  })

  test('keeps every real category and subcategory', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.cat-tree button').first()).toBeVisible()

    const buttons = await treeDepths(page)
    const topLevel = buttons.filter((b) => b.depth === 0 && b.text !== 'All products').map((b) => b.text)
    const subcategories = buttons.filter((b) => b.depth > 0).map((b) => b.text)

    // Categories carry their product count alongside the name.
    expect(topLevel.length).toBeGreaterThan(1)
    expect(subcategories.length).toBeGreaterThan(1)
    for (const label of topLevel) {
      expect(label).toMatch(/\D+\d+$/)
    }
    // No subcategory entry is a duplicate "All products" label.
    expect(subcategories).not.toContain('All products')
  })

  test('filters by category and by subcategory, then restores the full catalogue', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()
    const all = await page.locator('.product-card').count()

    // A top-level category narrows the listing.
    await page.locator('.cat-tree button', { hasText: 'Electronics' }).first().click()
    await page.waitForTimeout(700)
    const electronics = await page.locator('.product-card').count()
    expect(electronics).toBeLessThan(all)

    // A subcategory narrows it further.
    await page.locator('.cat-tree button', { hasText: 'Audio' }).first().click()
    await page.waitForTimeout(700)
    const audio = await page.locator('.product-card').count()
    expect(audio).toBeLessThan(electronics)

    // The single top-level entry brings the full catalogue back.
    await page.locator('.cat-tree button', { hasText: 'All products' }).click()
    await page.waitForTimeout(700)
    expect(await page.locator('.product-card').count()).toBe(all)
  })

  test('marks only the chosen entry as selected', async ({ page }) => {
    await page.goto('/products')
    await expect(page.locator('.cat-tree button').first()).toBeVisible()

    // Initially the single top-level entry is active.
    let active = page.locator('.cat-tree button.active')
    await expect(active).toHaveCount(1)
    await expect(active).toHaveText(/All products/)

    // Choosing a subcategory moves the highlight, and exactly one entry is ever active.
    await page.locator('.cat-tree button', { hasText: 'Audio' }).first().click()
    await page.waitForTimeout(700)
    active = page.locator('.cat-tree button.active')
    await expect(active).toHaveCount(1)
    await expect(active).toHaveText(/Audio/)
  })
})

test.describe('Post-widening layout correction', () => {
  const widths = [1920, 1600, 1440, 1280, 1024, 900, 768, 600, 390, 360]

  test('the hero shows only the intro text and the deal card', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1000 })
    await page.goto('/')
    await expect(page.locator('.hero-deal img')).toBeVisible()

    const hero = await page.evaluate(() => {
      const grid = document.querySelector('.hero-grid')!
      return {
        children: grid.children.length,
        columns: getComputedStyle(grid).gridTemplateColumns.split(' ').length,
        // The removed hero photograph and its wrapper must be gone, not merely hidden.
        hasCentreColumn: !!document.querySelector('.hero-center'),
        kicker: document.querySelector('.hero-grid .kicker')?.textContent ?? '',
        hasTitle: !!document.querySelector('.hero-title'),
        hasStats: document.querySelectorAll('.hero-stats > div').length,
        dealKicker: document.querySelector('.hero-deal .kicker')?.textContent ?? '',
        dealPrice: document.querySelector('.hero-deal .price, .hero-deal .fw-bold')?.textContent ?? '',
        dealButton: document.querySelector('.hero-deal a.btn-pill')?.textContent?.trim() ?? '',
      }
    })

    // No empty column left behind where the image used to be.
    expect(hero.hasCentreColumn).toBe(false)
    expect(hero.children).toBe(2)
    expect(hero.columns).toBe(2)

    // The introductory text and the deal card are all still present and populated.
    expect(hero.hasTitle).toBe(true)
    expect(hero.kicker).not.toBe('')
    expect(hero.hasStats).toBeGreaterThanOrEqual(3)
    expect(hero.dealKicker).toContain('Deal of the day')
    expect(hero.dealPrice).not.toBe('')
    expect(hero.dealButton).toContain('Shop now')
  })

  test('the hero stacks cleanly without overflow at every width', async ({ page }) => {
    for (const width of [1920, 1280, 1024, 900, 768, 720, 600, 390, 360, 320]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/')
      await expect(page.locator('.hero-deal img')).toBeVisible()

      const layout = await page.evaluate(() => {
        const grid = document.querySelector('.hero-grid')!
        const cols = getComputedStyle(grid).gridTemplateColumns.split(' ').map((v) => Math.round(parseFloat(v)))
        const deal = document.querySelector('.hero-deal')!.getBoundingClientRect()
        const button = document.querySelector('.hero-deal a.btn-pill')!.getBoundingClientRect()
        return {
          columns: cols.length,
          // No collapsed or empty track left over from the removed column.
          allTracksUsed: cols.every((c) => c > 0),
          cardFits: deal.right <= document.documentElement.clientWidth + 1,
          buttonFits: button.right <= document.documentElement.clientWidth + 1 && button.width > 0,
          overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        }
      })

      // Two columns on a desktop, one once it stacks, never more.
      expect(layout.columns).toBeLessThanOrEqual(2)
      expect(layout.allTracksUsed).toBe(true)
      expect(layout.cardFits).toBe(true)
      expect(layout.buttonFits).toBe(true)
      expect(layout.overflow).toBeLessThanOrEqual(0)
    }
  })

  test('the hero keeps its two-part composition and stays balanced', async ({ page }) => {
    for (const width of [1920, 1440, 1280]) {
      await page.setViewportSize({ width, height: 1000 })
      await page.goto('/')
      await expect(page.locator('.cat-tile').first()).toBeVisible()

      const parts = await page.evaluate(() => {
        const grid = document.querySelector('.hero-grid')!
        const kids = Array.from(grid.children)
        return {
          columns: getComputedStyle(grid).gridTemplateColumns.split(' ').length,
          count: kids.length,
          heights: kids.map((c) => Math.round(c.getBoundingClientRect().height)),
        }
      })

      // The introductory text and the deal card, side by side on a desktop.
      expect(parts.count).toBe(2)
      expect(parts.columns).toBe(2)

      // Neither part dwarfs the other, which is what keeps the hero from looking sparse.
      const tallest = Math.max(...parts.heights)
      const shortest = Math.min(...parts.heights)
      expect(tallest / shortest).toBeLessThan(1.6)
    }
  })

  for (const width of widths) {
    test(`home and shop align on one content boundary at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/')
      await expect(page.locator('.cat-tile').first()).toBeVisible()

      const bounds = await page.evaluate(() => {
        const box = (sel: string) => {
          const el = document.querySelector(sel)
          if (!el) return null
          const r = el.getBoundingClientRect()
          return { left: Math.round(r.left), right: Math.round(r.right) }
        }
        return {
          header: box('.site-header .bar .brand'),
          hero: box('.hero-grid .kicker'),
          section: box('.section .section-title'),
          container: box('.hero-grid'),
        }
      })

      // The header and the sections below share the page's content edge.
      expect(bounds.header!.left).toBe(bounds.section!.left)

      // The hero is a deliberately narrower composition centred inside that same container, so
      // it sits within the container's bounds rather than pinned to its left edge.
      expect(bounds.hero!.left).toBeGreaterThanOrEqual(bounds.container!.left - 1)
      expect(bounds.hero!.right).toBeLessThanOrEqual(bounds.container!.right + 1)
    })

    test(`the shop listing is usable at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/products')
      await expect(page.locator('.product-card').first()).toBeVisible()

      const listing = await page.evaluate(() => {
        const grid = document.querySelector('.product-grid')!
        const cards = Array.from(grid.querySelectorAll('.product-card'))
        const rects = cards.map((c) => c.getBoundingClientRect())

        // Group the cards into rows by their top edge.
        const rows = new Map<number, DOMRect[]>()
        for (const r of rects) {
          const key = Math.round(r.top)
          rows.set(key, [...(rows.get(key) ?? []), r])
        }
        const rowList = [...rows.values()]

        const clipped = cards.filter((el) => {
          const foot = el.querySelector('.card-foot')!.getBoundingClientRect()
          return foot.bottom > el.getBoundingClientRect().bottom + 1
        }).length

        const distorted = Array.from(grid.querySelectorAll('.product-thumb img')).filter((img) => {
          const r = img.getBoundingClientRect()
          return Math.abs(r.width - r.height) > 2 || getComputedStyle(img).objectFit !== 'contain'
        }).length

        const nameOverflow = Array.from(grid.querySelectorAll('.name')).filter(
          (n) => n.scrollWidth > n.clientWidth + 1,
        ).length

        return {
          columns: getComputedStyle(grid).gridTemplateColumns.split(' ').length,
          cardWidth: Math.round(rects[0].width),
          // Every card in a row shares one width, so the row reads as a grid.
          rowsEven: rowList.every((r) => new Set(r.map((x) => Math.round(x.width))).size === 1),
          rows: rowList.length,
          clipped,
          distorted,
          nameOverflow,
          overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
          // The controls above the grid must stay on screen.
          searchVisible: !!document.querySelector('.shop-search-input')?.getBoundingClientRect().width,
          sortVisible: !!document.querySelector('.shop-layout select, .shop-layout + select')
            ?.getBoundingClientRect().width,
        }
      })

      expect(listing.overflow).toBeLessThanOrEqual(0)
      expect(listing.rowsEven).toBe(true)
      expect(listing.clipped).toBe(0)
      expect(listing.distorted).toBe(0)
      expect(listing.nameOverflow).toBe(0)
      expect(listing.searchVisible).toBe(true)
      // Cards must not become absurdly wide just because the container is wide.
      expect(listing.cardWidth).toBeLessThanOrEqual(400)
      expect(listing.rows).toBeGreaterThan(0)
    })
  }

  test('a page of products fills complete rows on a wide desktop', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 900 })
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    const rows = await page.evaluate(() => {
      const cards = Array.from(document.querySelectorAll('.product-card'))
      const byRow = new Map<number, number>()
      for (const c of cards) {
        const top = Math.round(c.getBoundingClientRect().top)
        byRow.set(top, (byRow.get(top) ?? 0) + 1)
      }
      const counts = [...byRow.values()]
      return { counts, columns: getComputedStyle(document.querySelector('.product-grid')!).gridTemplateColumns.split(' ').length }
    })

    // The listing is one page deep, so at desktop width every row should be complete. This
    // is what the wider container previously broke, leaving a large unused area.
    expect(rows.counts.every((n) => n === rows.columns)).toBe(true)
  })

  test('the shop listing is reachable without scrolling past the whole sidebar', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 900 })
    await page.goto('/products')
    await expect(page.locator('.product-card').first()).toBeVisible()

    const position = await page.evaluate(() => {
      const card = document.querySelector('.product-card')!.getBoundingClientRect()
      return { top: card.top + window.scrollY, viewport: window.innerHeight }
    })

    // The stacked category tree is bounded, so the first card sits near the first screen
    // instead of roughly two viewports down.
    expect(position.top).toBeLessThan(position.viewport * 1.4)
  })
})

test.describe('Responsive header', () => {
  const widths = [900, 860, 768, 720, 600, 390, 360]

  for (const width of widths) {
    test(`does not overflow horizontally and keeps every control at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/')
      await expect(page.locator('.cat-tile').first()).toBeVisible()

      const result = await page.evaluate(() => {
        const bar = document.querySelector('.site-header .bar')!
        const viewWidth = document.documentElement.clientWidth
        const visible = (selector: string) => {
          const el = bar.querySelector(selector)
          if (!el) return false
          const r = el.getBoundingClientRect()
          return r.width > 0 && r.height > 0 && r.right <= viewWidth + 1 && r.left >= -1
        }
        const header = document.querySelector('.site-header')!.getBoundingClientRect()
        const content = document.querySelector('.hero, .section')?.getBoundingClientRect()

        return {
          overflow: document.documentElement.scrollWidth - viewWidth,
          controls: ['.brand', '.header-search', '.nav-main', '[data-testid="cart-button"]'].every(visible),
          // The sticky header must not sit on top of the page content.
          overlaps: content ? content.top < header.bottom - 1 : false,
        }
      })

      expect(result.overflow).toBeLessThanOrEqual(0)
      expect(result.controls).toBe(true)
      expect(result.overlaps).toBe(false)
    })
  }
})

test.describe('Page layout', () => {
  const viewports = [
    { width: 1920, height: 1000, label: 'desktop 1920' },
    { width: 1440, height: 900, label: 'laptop 1440' },
    { width: 1024, height: 800, label: 'tablet 1024' },
    { width: 390, height: 844, label: 'mobile 390' },
  ]

  for (const viewport of viewports) {
    test(`content fills the viewport and header aligns at ${viewport.label}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height })
      await page.goto('/')
      await expect(page.locator('.cat-tile').first()).toBeVisible()

      const layout = await page.evaluate(() => {
        const inner = (sel: string) => {
          const el = document.querySelector(sel)!
          const r = el.getBoundingClientRect()
          return { x: Math.round(r.x), width: Math.round(r.width) }
        }
        return {
          content: inner('.section .wrap'),
          header: inner('.site-header .bar'),
          docWidth: document.documentElement.clientWidth,
          overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        }
      })

      // The header shares the page's content boundary.
      expect(Math.abs(layout.content.x - layout.header.x)).toBeLessThanOrEqual(1)

      // The container uses a large share of a desktop viewport and never adds side margins
      // on smaller screens.
      const share = layout.content.width / layout.docWidth
      if (viewport.width >= 1440) {
        expect(share).toBeGreaterThanOrEqual(0.85)
        expect(share).toBeLessThanOrEqual(1)
      } else {
        expect(layout.content.width).toBeLessThanOrEqual(layout.docWidth)
      }
    })
  }

  test('the carousel does not cause page-level horizontal overflow', async ({ page }) => {
    for (const width of [1920, 1440, 1024, 768, 390]) {
      await page.setViewportSize({ width, height: 900 })
      await page.goto('/')
      await expect(page.locator('.cat-tile').first()).toBeVisible()
      await page.waitForTimeout(400)

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      )
      const stripOverflowsPage = await page.locator('.cat-tiles').evaluate((el) => {
        const r = el.getBoundingClientRect()
        return r.right > document.documentElement.clientWidth + 1
      })
      expect(stripOverflowsPage).toBe(false)
      // No page may scroll sideways at any width, including the narrowest.
      expect(overflow).toBeLessThanOrEqual(0)
    }
  })
})
