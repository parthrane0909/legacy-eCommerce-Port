import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

vi.mock('@/api/catalog', () => ({
  getCategories: vi.fn(),
  getProducts: vi.fn(),
  getProduct: vi.fn(),
  getHomeProducts: vi.fn(),
}))

import * as catalogApi from '@/api/catalog'
import { useCatalogStore } from '@/stores/catalog'
import type { PagedResult, ProductSummary } from '@/types'

function product(name: string, id: number): ProductSummary {
  return {
    id,
    name,
    slug: name.toLowerCase().replace(/\s+/g, '-'),
    price: 1000,
    wasPrice: null,
    discountPercent: null,
    imageUrl: '/a.jpg',
    categoryName: 'Audio',
    isFeatured: false,
    inStock: true,
    stock: 10,
    ratingNum: 4.5,
  }
}

function page(items: ProductSummary[], totalCount = items.length): PagedResult<ProductSummary> {
  return {
    items,
    page: 1,
    pageSize: 8,
    totalCount,
    totalPages: 1,
    hasPrevious: false,
    hasNext: false,
  }
}

/** Resolves with the products whose name contains the term, mirroring the API's partial match. */
function serverSearch(term: string) {
  const all = [
    product('Aluminium Laptop Stand', 1),
    product('Travel Laptop Backpack', 2),
    product('Wireless Headphones Pro', 3),
  ]
  const needle = term.trim().toLowerCase()
  return page(needle ? all.filter((p) => p.name.toLowerCase().includes(needle)) : all)
}

describe('catalog store — incremental search', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.mocked(catalogApi.getProducts).mockImplementation(async ({ search = '' } = {}) =>
      serverSearch(search),
    )
  })

  it('finds products from a partial term', async () => {
    const store = useCatalogStore()

    await store.setSearch('lap')

    expect(store.products.map((p) => p.name)).toEqual([
      'Aluminium Laptop Stand',
      'Travel Laptop Backpack',
    ])
    expect(catalogApi.getProducts).toHaveBeenCalledWith(expect.objectContaining({ search: 'lap' }))
  })

  it('is case-insensitive', async () => {
    const store = useCatalogStore()

    await store.setSearch('LAP')
    const upper = store.products.map((p) => p.name)

    await store.setSearch('lap')

    expect(upper).toEqual(store.products.map((p) => p.name))
    expect(upper.length).toBe(2)
  })

  it('trims surrounding whitespace', async () => {
    const store = useCatalogStore()

    await store.setSearch('   lap   ')

    expect(catalogApi.getProducts).toHaveBeenCalledWith(expect.objectContaining({ search: 'lap' }))
    expect(store.products).toHaveLength(2)
  })

  it('restores the full list when the query is cleared', async () => {
    const store = useCatalogStore()

    await store.setSearch('lap')
    expect(store.products).toHaveLength(2)

    await store.setSearch('')

    expect(store.products).toHaveLength(3)
    expect(store.totalCount).toBe(3)
  })

  it('returns an empty result set for a term with no matches', async () => {
    const store = useCatalogStore()

    await store.setSearch('zzzz')

    expect(store.products).toEqual([])
    expect(store.totalCount).toBe(0)
    expect(store.error).toBeNull()
  })

  it('keeps the newest query when a slower earlier response resolves later', async () => {
    // Reproduces the overlap that debounced typing creates: the "lapt" request is issued
    // first but resolves last, and must not overwrite the "lap" results.
    const resolvers: (() => void)[] = []
    vi.mocked(catalogApi.getProducts).mockImplementation(
      ({ search = '' } = {}) =>
        new Promise((resolve) => {
          resolvers.push(() => resolve(serverSearch(search)))
        }),
    )

    const store = useCatalogStore()
    const first = store.setSearch('lapt')
    const second = store.setSearch('lap')

    // Resolve the newer query first, then let the stale one land.
    resolvers[1]()
    await second
    resolvers[0]()
    await first

    expect(store.products.map((p) => p.name)).toEqual([
      'Aluminium Laptop Stand',
      'Travel Laptop Backpack',
    ])
    expect(store.loading).toBe(false)
  })

  it('surfaces an error for the newest query only', async () => {
    vi.mocked(catalogApi.getProducts).mockImplementation(async ({ search = '' } = {}) => {
      if (search === 'boom') throw new Error('Search failed')
      return serverSearch(search)
    })

    const store = useCatalogStore()
    await store.setSearch('boom')

    expect(store.error).toBe('Search failed')
    expect(store.products).toEqual([])
  })

  it('combines the search term with an active category filter', async () => {
    const store = useCatalogStore()
    store.categoryId = 3

    await store.setSearch('lap')

    expect(catalogApi.getProducts).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'lap', categoryId: 3 }),
    )
  })
})
