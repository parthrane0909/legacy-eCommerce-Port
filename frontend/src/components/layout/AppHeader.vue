<script setup lang="ts">
/** Site header: brand, search, primary navigation, account link and cart button. */
import { onBeforeUnmount, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { useUiStore } from '@/stores/ui'

const router = useRouter()
const cart = useCartStore()
const auth = useAuthStore()
const ui = useUiStore()
const catalog = useCatalogStore()

/**
 * Incremental search. Results refresh shortly after typing stops rather than only on submit,
 * so a shopper can find "Laptop" by typing "lap". The delay keeps this to one request per
 * pause instead of one per keystroke.
 */
const SEARCH_DEBOUNCE_MS = 300

const term = ref(catalog.search)
let debounceTimer: number | undefined

// Keep the box in step when the store's query changes from elsewhere (e.g. the URL).
watch(
  () => catalog.search,
  (value) => {
    if (value !== term.value) {
      term.value = value
      cancelPendingSearch()
    }
  },
)

function cancelPendingSearch(): void {
  if (debounceTimer !== undefined) {
    window.clearTimeout(debounceTimer)
    debounceTimer = undefined
  }
}

/**
 * Navigates to the results, replacing the history entry so typing does not flood it.
 *
 * Any category filter and sort already in effect are carried across. Searching is meant to
 * narrow the current listing, so silently dropping the category a shopper had just picked
 * would be surprising; the listing page's own search box behaves the same way.
 */
function runSearch(replace: boolean): void {
  const q = term.value.trim()
  const query: Record<string, string> = {}

  if (q) query.q = q
  if (catalog.categoryId !== null) query.categoryId = String(catalog.categoryId)
  if (catalog.sort !== 'newest') query.sort = catalog.sort

  const target = { name: 'products' as const, query }
  const onResults = router.currentRoute.value.name === 'products'

  if (onResults || !replace) {
    void router.replace(target)
  } else {
    void router.push(target)
  }
}

function onSearch(): void {
  cancelPendingSearch()
  runSearch(true)
}

function onSearchInput(): void {
  cancelPendingSearch()
  debounceTimer = window.setTimeout(() => {
    debounceTimer = undefined
    runSearch(false)
  }, SEARCH_DEBOUNCE_MS)
}

onBeforeUnmount(cancelPendingSearch)

async function openCart(): Promise<void> {
  await cart.fetch()
  ui.openMiniCart()
}
</script>

<template>
  <header class="site-header">
    <div class="bar">
      <RouterLink :to="{ name: 'home' }" class="brand">SHOP<span>HUB</span></RouterLink>

      <form class="header-search" role="search" @submit.prevent="onSearch">
        <label class="visually-hidden" for="site-search">Search the store</label>
        <input
          id="site-search"
          v-model="term"
          type="search"
          placeholder="Search products, brands and categories…"
          autocomplete="off"
          @input="onSearchInput"
        />
        <button class="btn-pill btn-accent" type="submit">Search</button>
      </form>

      <nav class="nav-main" aria-label="Primary">
        <RouterLink :to="{ name: 'home' }">Home</RouterLink>
        <RouterLink :to="{ name: 'products' }">Shop</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" :to="{ name: 'account' }">Account</RouterLink>
        <RouterLink v-if="auth.isAdmin" :to="{ name: 'admin-dashboard' }">Admin</RouterLink>
      </nav>

      <button
        v-if="auth.isAuthenticated"
        class="btn-pill btn-ghost"
        type="button"
        @click="auth.logout()"
      >
        Sign out
      </button>
      <RouterLink v-else class="btn-pill btn-ghost" :to="{ name: 'login' }">Sign in</RouterLink>

      <button
        class="btn-pill"
        type="button"
        data-testid="cart-button"
        style="position: relative"
        :aria-label="`Cart, ${cart.itemCount} items`"
        @click="openCart"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <path d="M6 2L3 6v14a2 2 0 002 2h14a2 2 0 002-2V6l-3-4z" />
          <line x1="3" y1="6" x2="21" y2="6" />
          <path d="M16 10a4 4 0 01-8 0" />
        </svg>
        <span v-if="cart.itemCount > 0" class="cart-badge">{{ cart.itemCount }}</span>
      </button>
    </div>
  </header>
</template>