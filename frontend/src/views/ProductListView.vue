<script setup lang="ts">
/** Product listing: category sidebar, sort, paging and grid, all server-driven. */
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCatalogStore, SORT_OPTIONS, type SortOption } from '@/stores/catalog'
import { useCartStore } from '@/stores/cart'
import { useUiStore } from '@/stores/ui'
import ProductList from '@/components/catalog/ProductList.vue'
import CategoryTree from '@/components/catalog/CategoryTree.vue'
import Pagination from '@/components/common/Pagination.vue'

const catalog = useCatalogStore()
const cart = useCartStore()
const ui = useUiStore()
const route = useRoute()
const router = useRouter()

const busyProductId = ref<number | null>(null)

/** Delay before an incremental search issues its request, so one pause means one request. */
const SEARCH_DEBOUNCE_MS = 300
let searchTimer: number | undefined

function cancelPendingSearch(): void {
  if (searchTimer !== undefined) {
    window.clearTimeout(searchTimer)
    searchTimer = undefined
  }
}

/**
 * Applies the current URL query to the store, then loads the matching page.
 *
 * The query is applied once on mount, and again whenever the URL changes. A navigation we
 * triggered ourselves (via `syncUrl`) would otherwise cause a duplicate fetch, because the
 * store already holds those exact values.
 */
let queryApplied = false

async function applyQuery(): Promise<void> {
  const q = route.query

  const nextCategory = q.categoryId ? Number(q.categoryId) : null
  const nextSearch = typeof q.q === 'string' ? q.q : ''
  const nextSort = (typeof q.sort === 'string' ? q.sort : 'newest') as SortOption
  const nextPage = q.page ? Math.max(1, Number(q.page)) : 1

  const alreadyApplied =
    queryApplied &&
    nextCategory === catalog.categoryId &&
    nextSearch === catalog.search &&
    nextSort === catalog.sort &&
    nextPage === catalog.page

  catalog.categoryId = nextCategory
  catalog.search = nextSearch
  catalog.sort = nextSort
  catalog.page = nextPage
  queryApplied = true

  if (alreadyApplied) return

  await Promise.all([catalog.fetchCategories(), catalog.fetchProducts()])
}

/** Writes the store's filters back to the URL so the view is shareable and back/forward works. */
function syncUrl(): void {
  const query: Record<string, string> = {}
  if (catalog.categoryId) query.categoryId = String(catalog.categoryId)
  if (catalog.search) query.q = catalog.search
  if (catalog.sort !== 'newest') query.sort = catalog.sort
  if (catalog.page > 1) query.page = String(catalog.page)

  void router.replace({ name: 'products', query })
}

onMounted(applyQuery)
watch(() => route.query, applyQuery)
onBeforeUnmount(cancelPendingSearch)

async function selectCategory(id: number | null): Promise<void> {
  cancelPendingSearch()
  await catalog.setCategory(id)
  syncUrl()
}

async function onSearch(): Promise<void> {
  cancelPendingSearch()
  await catalog.setSearch(catalog.search)
  syncUrl()
}

/** Re-queries after typing stops, so results update as the shopper types. */
function onSearchInput(): void {
  cancelPendingSearch()
  searchTimer = window.setTimeout(() => {
    searchTimer = undefined
    void onSearch()
  }, SEARCH_DEBOUNCE_MS)
}

async function onSort(event: Event): Promise<void> {
  await catalog.setSort((event.target as HTMLSelectElement).value as SortOption)
  syncUrl()
}

async function onPage(target: number): Promise<void> {
  await catalog.goToPage(target)
  syncUrl()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function addToCart(productId: number, productName?: string): Promise<void> {
  busyProductId.value = productId
  await cart.add(productId, 1)

  if (cart.error) ui.notifyError(cart.error)
  else ui.notifySuccess(productName ? `${productName} added to cart` : 'Added to cart')

  busyProductId.value = null
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">Catalogue</p>
      <h1 class="section-title mb-4">Shop all products</h1>

      <div class="shop-layout">
        <aside class="shop-sidebar">
          <h2 class="h6 mb-3">Categories</h2>
          <CategoryTree
            :categories="catalog.categories"
            :active-id="catalog.categoryId"
            @select="selectCategory"
          />
        </aside>

        <div>
          <div class="d-flex flex-wrap gap-3 justify-content-between align-items-center mb-4">
            <form class="d-flex gap-2" role="search" @submit.prevent="onSearch">
              <label class="visually-hidden" for="list-search">Search products</label>
              <input
                id="list-search"
                v-model="catalog.search"
                class="form-control shop-search-input"
                type="search"
                placeholder="Search products…"
                autocomplete="off"
                @input="onSearchInput"
              />
              <button class="btn-pill" type="submit">Search</button>
            </form>

            <div class="d-flex align-items-center gap-2">
              <label class="small text-secondary" for="sort">Sort</label>
              <select
                id="sort"
                class="form-select"
                style="width: auto"
                :value="catalog.sort"
                @change="onSort"
              >
                <option v-for="option in SORT_OPTIONS" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>
          </div>

          <p v-if="catalog.activeCategory" class="text-secondary small">
            Showing {{ catalog.totalCount }} product(s) in {{ catalog.activeCategory.name }}.
          </p>

          <ProductList
            :products="catalog.products"
            :loading="catalog.loading"
            :error="catalog.error"
            :busy-product-id="busyProductId"
            empty-message="Try a different search term or category."
            @add="addToCart($event.id, $event.name)"
          />

          <Pagination
            :page="catalog.page"
            :total-pages="catalog.totalPages"
            :disabled="catalog.loading"
            @change="onPage"
          />
        </div>
      </div>
    </div>
  </section>
</template>