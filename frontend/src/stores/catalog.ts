import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as catalogApi from '@/api/catalog'
import type { Category, ProductDetail, ProductSummary } from '@/types'

export type SortOption = 'newest' | 'price_asc' | 'price_desc' | 'name_asc' | 'name_desc' | 'rating_desc'

/** Sort options offered in the UI, matching the values the API accepts. */
export const SORT_OPTIONS: { value: SortOption; label: string }[] = [
  { value: 'newest', label: 'Newest' },
  { value: 'price_asc', label: 'Price: low to high' },
  { value: 'price_desc', label: 'Price: high to low' },
  { value: 'name_asc', label: 'Name: A–Z' },
  { value: 'name_desc', label: 'Name: Z–A' },
  { value: 'rating_desc', label: 'Top rated' },
]

/** Page size, matching the legacy storefront's eight-per-page grid. */
export const PAGE_SIZE = 8

/**
 * Catalogue state. Products are always fetched as a page from the API; the store never
 * holds the full catalogue, so filtering and sorting are genuinely server-side.
 */
export const useCatalogStore = defineStore('catalog', () => {
  const categories = ref<Category[]>([])
  const products = ref<ProductSummary[]>([])
  const featured = ref<ProductSummary[]>([])
  const newArrivals = ref<ProductSummary[]>([])
  const deals = ref<ProductSummary[]>([])

  const page = ref(1)
  const pageSize = ref(PAGE_SIZE)
  const totalCount = ref(0)
  const totalPages = ref(0)

  const categoryId = ref<number | null>(null)
  const search = ref('')
  const sort = ref<SortOption>('newest')

  const loading = ref(false)
  const error = ref<string | null>(null)

  /** The product currently being viewed on the detail page. */
  const currentProduct = ref<ProductDetail | null>(null)

  const hasPrevious = computed(() => page.value > 1)
  const hasNext = computed(() => page.value < totalPages.value)
  const isEmpty = computed(() => !loading.value && products.value.length === 0)
  /** Flattens the nested tree so a child category id resolves too. */
  function flatten(nodes: Category[]): Category[] {
    return nodes.flatMap((node) => [node, ...flatten(node.children)])
  }

  const activeCategory = computed(() => {
    if (categoryId.value === null) return null
    return flatten(categories.value).find((c) => c.id === categoryId.value) ?? null
  })

  async function fetchCategories(): Promise<void> {
    try {
      categories.value = await catalogApi.getCategories()
    } catch {
      categories.value = []
    }
  }

  /**
   * Guards against a slow response for an older query landing after a newer one. Incremental
   * search fires several overlapping requests, so without this the results could be replaced
   * by the previous query's payload.
   */
  let productsRequestId = 0

  async function fetchProducts(): Promise<void> {
    const requestId = ++productsRequestId
    loading.value = true
    error.value = null
    try {
      const result = await catalogApi.getProducts({
        page: page.value,
        pageSize: pageSize.value,
        categoryId: categoryId.value,
        search: search.value,
        sort: sort.value,
      })

      // A newer query superseded this one; discard the stale payload.
      if (requestId !== productsRequestId) return

      products.value = result.items
      totalCount.value = result.totalCount
      totalPages.value = result.totalPages
    } catch (cause) {
      if (requestId !== productsRequestId) return

      products.value = []
      error.value = cause instanceof Error ? cause.message : 'Products could not be loaded.'
    } finally {
      if (requestId === productsRequestId) loading.value = false
    }
  }

  async function fetchHome(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const home = await catalogApi.getHomeProducts()
      featured.value = home.featured
      newArrivals.value = home.newArrivals
      deals.value = home.deals
      categories.value = home.categories
    } catch (cause) {
      error.value = cause instanceof Error ? cause.message : 'The storefront could not be loaded.'
    } finally {
      loading.value = false
    }
  }

  async function fetchProduct(id: number): Promise<ProductDetail | null> {
    loading.value = true
    error.value = null
    try {
      const product = await catalogApi.getProduct(id)
      currentProduct.value = product
      return product
    } catch {
      currentProduct.value = null
      error.value = 'That product could not be found.'
      return null
    } finally {
      loading.value = false
    }
  }

  /** Applies a filter and returns to page 1, which is what a shopper expects. */
  async function setCategory(id: number | null): Promise<void> {
    categoryId.value = id
    page.value = 1
    await fetchProducts()
  }

  async function setSearch(term: string): Promise<void> {
    search.value = term.trim()
    page.value = 1
    await fetchProducts()
  }

  async function setSort(option: SortOption): Promise<void> {
    sort.value = option
    page.value = 1
    await fetchProducts()
  }

  async function goToPage(target: number): Promise<void> {
    if (target < 1 || (totalPages.value > 0 && target > totalPages.value)) return
    page.value = target
    await fetchProducts()
  }

  function resetFilters(): void {
    categoryId.value = null
    search.value = ''
    sort.value = 'newest'
    page.value = 1
  }

  return {
    categories,
    products,
    currentProduct,
    featured,
    newArrivals,
    deals,
    page,
    pageSize,
    totalCount,
    totalPages,
    categoryId,
    search,
    sort,
    loading,
    error,
    hasPrevious,
    hasNext,
    isEmpty,
    activeCategory,
    fetchCategories,
    fetchProducts,
    fetchHome,
    fetchProduct,
    setCategory,
    setSearch,
    setSort,
    goToPage,
    resetFilters,
  }
})