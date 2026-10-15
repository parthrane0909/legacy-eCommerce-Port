import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

vi.mock('@/api/catalog', () => ({
  getCategories: vi.fn(),
  getProducts: vi.fn(),
  getProduct: vi.fn(),
  getHomeProducts: vi.fn(),
}))

import * as catalogApi from '@/api/catalog'
import { useCatalogStore, PAGE_SIZE } from '@/stores/catalog'
import type { Category, PagedResult, ProductSummary } from '@/types'

function product(overrides: Partial<ProductSummary> = {}): ProductSummary {
  return {
    id: 1,
    name: 'Headphones',
    slug: 'headphones',
    price: 4999,
    wasPrice: null,
    discountPercent: null,
    imageUrl: '/a.jpg',
    categoryName: 'Audio',
    isFeatured: false,
    inStock: true,
    stock: 10,
    ratingNum: 4.5,
    ...overrides,
  }
}

function page(items: ProductSummary[], overrides: Partial<PagedResult<ProductSummary>> = {}) {
  return {
    items,
    page: 1,
    pageSize: PAGE_SIZE,
    totalCount: items.length,
    totalPages: 1,
    hasPrevious: false,
    hasNext: false,
    ...overrides,
  } as PagedResult<ProductSummary>
}

const tree: Category[] = [
  {
    id: 1,
    name: 'Electronics',
    slug: 'electronics',
    description: null,
    isActive: true,
    sortOrder: 1,
    parentId: null,
    productCount: 12,
    children: [
      {
        id: 10,
        name: 'Audio',
        slug: 'audio',
        description: null,
        isActive: true,
        sortOrder: 1,
        parentId: 1,
        productCount: 5,
        children: [],
      },
    ],
  },
]

describe('catalog store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('defaults to eight products per page, matching the legacy grid', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([], { totalCount: 0 }))

    const store = useCatalogStore()
    await store.fetchProducts()

    expect(store.pageSize).toBe(PAGE_SIZE)
    expect(catalogApi.getProducts).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 8 }))
  })

  it('sends filters to the API rather than filtering client-side', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([product()]))

    const store = useCatalogStore()
    await store.setCategory(10)
    await store.setSearch('headphones')
    await store.setSort('price_asc')

    expect(catalogApi.getProducts).toHaveBeenLastCalledWith(
      expect.objectContaining({ categoryId: 10, search: 'headphones', sort: 'price_asc', page: 1 }),
    )
  })

  it('resets to page 1 when a filter changes', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([product()]))

    const store = useCatalogStore()
    store.page = 4

    await store.setCategory(10)

    expect(store.page).toBe(1)
  })

  it('exposes pagination metadata from the response', async () => {
    vi.mocked(
      catalogApi.getProducts,
    ).mockResolvedValue(page([product()], { totalCount: 30, totalPages: 4, hasNext: true }))

    const store = useCatalogStore()
    await store.fetchProducts()

    expect(store.totalCount).toBe(30)
    expect(store.totalPages).toBe(4)
    expect(store.hasNext).toBe(true)
    expect(store.hasPrevious).toBe(false)
  })

  it('refuses to page outside the valid range', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([product()], { totalPages: 3 }))

    const store = useCatalogStore()
    await store.goToPage(0)
    await store.goToPage(99)

    expect(catalogApi.getProducts).toHaveBeenCalledTimes(1)
  })

  it('reports an empty result set', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([], { totalCount: 0 }))

    const store = useCatalogStore()
    await store.fetchProducts()

    expect(store.isEmpty).toBe(true)
    expect(store.products).toEqual([])
  })

  it('records an error and clears products on failure', async () => {
    vi.mocked(catalogApi.getProducts).mockRejectedValue(new Error('network down'))

    const store = useCatalogStore()
    await store.fetchProducts()

    expect(store.error).toBe('network down')
    expect(store.products).toEqual([])
  })

  it('loads the home rails and category tree together', async () => {
    vi.mocked(catalogApi.getHomeProducts).mockResolvedValue({
      featured: [product()],
      newArrivals: [product({ id: 2 })],
      deals: [product({ id: 3, discountPercent: 10 })],
      categories: tree,
    })

    const store = useCatalogStore()
    await store.fetchHome()

    expect(store.featured).toHaveLength(1)
    expect(store.newArrivals).toHaveLength(1)
    expect(store.deals).toHaveLength(1)
    expect(store.categories).toHaveLength(1)
  })

  it('resolves the active category for the filter heading', async () => {
    vi.mocked(catalogApi.getCategories).mockResolvedValue(tree)

    const store = useCatalogStore()
    await store.fetchCategories()
    store.categoryId = 10

    expect(store.activeCategory?.name).toBe('Audio')
  })

  it('clears all filters on reset', async () => {
    vi.mocked(catalogApi.getProducts).mockResolvedValue(page([product()]))

    const store = useCatalogStore()
    store.categoryId = 10
    store.search = 'x'
    store.sort = 'price_desc'
    store.page = 3

    store.resetFilters()

    expect(store.categoryId).toBeNull()
    expect(store.search).toBe('')
    expect(store.sort).toBe('newest')
    expect(store.page).toBe(1)
  })

  it('stores the product loaded for the detail view', async () => {
    const detail = {
      ...product(),
      description: 'Over-ear headphones.',   // required by ProductDetail
      categoryId: 10,
      images: [{ id: 1, url: '/a.jpg', altText: null, isPrimary: true, sortOrder: 0 }],
      variants: [{ id: 1, name: 'Black', sku: null, priceDelta: 0, stock: 3 }],
    }
    vi.mocked(catalogApi.getProduct).mockResolvedValue(detail)

    const store = useCatalogStore()
    const result = await store.fetchProduct(1)

    expect(result?.id).toBe(1)
    expect(store.currentProduct?.variants).toHaveLength(1)
  })
})