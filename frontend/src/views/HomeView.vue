<script setup lang="ts">
/**
 * Home page. Recreates the legacy storefront's editorial sections: hero, category tiles,
 * featured rail, deals, new arrivals and benefits — all fed by the API, nothing hardcoded.
 */
import { onMounted, ref, computed, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useCatalogStore } from '@/stores/catalog'
import { useCartStore } from '@/stores/cart'
import { useUiStore } from '@/stores/ui'
import { useHorizontalRail } from '@/composables/useHorizontalRail'
import { formatCurrency, formatRating } from '@/utils/format'
import type { ProductSummary } from '@/types'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import ProductCard from '@/components/catalog/ProductCard.vue'

const catalog = useCatalogStore()
const cart = useCartStore()
const ui = useUiStore()
const router = useRouter()

const busyProductId = ref<number | null>(null)
const dealOfTheDay = computed(() => catalog.deals[0] ?? null)

/*
 * Both home-page strips scroll horizontally and share the same navigation behaviour, so the
 * scroll wiring lives in one composable. The featured rail is the existing scrolling element;
 * the buttons drive it with `scrollBy` rather than introducing a carousel, so the card layout
 * and native wheel, trackpad and touch panning are unchanged.
 */
const featuredRail = useHorizontalRail({
  leftLabel: 'Scroll featured products left',
  rightLabel: 'Scroll featured products right',
})

/**
 * Stock wording for a featured card, matching ProductCard's own rules so the rail and the Shop
 * grid never disagree about the same product.
 */
function stockLabel(product: ProductSummary): string {
  if (!product.inStock) return 'Out of stock'
  if (product.stock <= 12) return `Only ${product.stock} left`
  return 'In stock'
}

function stockClass(product: ProductSummary): string {
  if (!product.inStock) return 'out'
  return product.stock <= 12 ? 'low' : 'in'
}

/**
 * One pass of the loop. The strip renders this set twice, so the automatic movement can wrap
 * back to the start without a visible jump. Only the first copy is exposed to assistive
 * technology and to the tab order, so no category is announced or reachable twice.
 */
const categoryCount = () => catalog.categories.length

const categoryRail = useHorizontalRail({
  leftLabel: 'Scroll categories left',
  rightLabel: 'Scroll categories right',
  // The category strip flows on its own so every category is discovered without clicking.
  // 34px/s keeps a card readable and clickable while it crosses the strip.
  continuous: { itemCount: categoryCount, pxPerSecond: 34 },
})

onMounted(() => void catalog.fetchHome())

// Both strips are populated by fetchHome(), so their scroll extents are only known once the
// data lands. Adding cards does not change the strip's own box, so the resize observer alone
// would not fire; re-measure explicitly.
watch(
  () => [catalog.categories.length, catalog.featured.length],
  async () => {
    await nextTick()
    categoryRail.refresh()
    featuredRail.refresh()
  },
)

async function addToCart(productId: number, productName?: string): Promise<void> {
  busyProductId.value = productId
  await cart.add(productId, 1)

  if (cart.error) {
    ui.notifyError(cart.error)
  } else {
    ui.notifySuccess(productName ? `${productName} added to cart` : 'Added to cart')
  }

  busyProductId.value = null
}

function browseCategory(id: number): void {
  void router.push({ name: 'products', query: { categoryId: id } })
}
</script>

<template>
  <LoadingSpinner v-if="catalog.loading && catalog.featured.length === 0" label="Loading the storefront…" />

  <div v-else-if="catalog.error" class="error-state" role="alert">
    <h3>We couldn't load the storefront</h3>
    <p>{{ catalog.error }}</p>
    <button class="btn-pill mt-2" type="button" @click="catalog.fetchHome()">Try again</button>
  </div>

  <template v-else>
    <!-- Hero -->
    <section class="hero">
      <div class="wrap hero-grid">
        <div>
          <p class="kicker mb-0">Online shopping · Electronics · Home · Books · More</p>
          <h1 class="hero-title">Everything<br />you need,<br />in one place.</h1>
          <p class="text-secondary" style="max-width: 420px">
            Discover products across electronics, home essentials, books, sports gear and more —
            with secure checkout and fast delivery.
          </p>
          <div class="d-flex gap-2 flex-wrap mt-3">
            <RouterLink class="btn-pill btn-accent" :to="{ name: 'products' }">
              Shop all products
            </RouterLink>
            <a class="btn-pill" href="#categories">Browse categories</a>
          </div>

          <div class="hero-stats">
            <div>
              <strong>{{ catalog.categories.length }}</strong>
              <span>Categories</span>
            </div>
            <div>
              <strong>{{ catalog.featured.length }}+</strong>
              <span>Featured</span>
            </div>
            <div>
              <strong>{{ catalog.deals.length }}</strong>
              <span>Deals live</span>
            </div>
          </div>
        </div>

        <div class="hero-right">
          <div class="hero-deal">
            <p class="kicker">Deal of the day</p>

            <template v-if="dealOfTheDay">
              <RouterLink :to="{ name: 'product-details', params: { id: dealOfTheDay.id } }">
                <img :src="dealOfTheDay.imageUrl" :alt="dealOfTheDay.name" />
              </RouterLink>
              <div class="fw-semibold mt-2">{{ dealOfTheDay.name }}</div>
              <div class="d-flex align-items-center gap-2 mt-1">
                <span class="fw-bold">{{ formatCurrency(dealOfTheDay.price) }}</span>
                <span v-if="dealOfTheDay.wasPrice" class="old-price text-secondary">
                  {{ formatCurrency(dealOfTheDay.wasPrice) }}
                </span>
                <span class="badge-sale" style="position: static">-{{ dealOfTheDay.discountPercent }}%</span>
              </div>
            </template>

            <RouterLink class="btn-pill w-100 justify-content-center mt-3" :to="{ name: 'products' }">
              Shop now →
            </RouterLink>
          </div>
        </div>
      </div>
    </section>

    <!-- Category tiles -->
    <section v-if="catalog.categories.length" id="categories" class="section">
      <div class="wrap">
        <p class="kicker">Browse</p>
        <h2 class="section-title mb-4">Shop by category</h2>

        <div class="rail">
          <button
            class="rail-btn"
            type="button"
            :disabled="!categoryRail.canScrollLeft.value"
            :aria-label="categoryRail.leftLabel"
            @click="categoryRail.scrollBy(-1)"
          >
            <span aria-hidden="true">‹</span>
          </button>

          <div
            :ref="categoryRail.setViewport"
            class="cat-tiles"
            @scroll.passive="categoryRail.update"
          >
            <div class="cat-track">
            <button
              v-for="category in catalog.categories"
              :key="category.id"
              class="cat-tile text-start border-0"
              type="button"
              @click="browseCategory(category.id)"
            >
              <span class="cat-tile-name">{{ category.name }}</span>
              <span class="cat-tile-count">{{ category.productCount }} products</span>
              <span v-if="category.children.length" class="cat-tile-sub">
                {{ category.children.slice(0, 3).map((c) => c.name).join(' · ') }}
              </span>
            </button>

            <!--
              Cloned pass for the seamless loop. Hidden from assistive technology and removed
              from the tab order, and it carries no key of its own, so it can never be
              announced, focused or clicked as a duplicate category.
            -->
            <template v-for="category in catalog.categories" :key="`clone-${category.id}`">
              <button
                class="cat-tile text-start border-0"
                type="button"
                tabindex="-1"
                aria-hidden="true"
                inert
                @click="browseCategory(category.id)"
              >
                <span class="cat-tile-name">{{ category.name }}</span>
                <span class="cat-tile-count">{{ category.productCount }} products</span>
                <span v-if="category.children.length" class="cat-tile-sub">
                  {{ category.children.slice(0, 3).map((c) => c.name).join(' · ') }}
                </span>
              </button>
            </template>
            </div>
          </div>

          <button
            class="rail-btn"
            type="button"
            :disabled="!categoryRail.canScrollRight.value"
            :aria-label="categoryRail.rightLabel"
            @click="categoryRail.scrollBy(1)"
          >
            <span aria-hidden="true">›</span>
          </button>
        </div>
      </div>
    </section>

    <!-- Featured rail -->
    <section v-if="catalog.featured.length" class="section">
      <div class="wrap">
        <p class="kicker">Handpicked</p>
        <h2 class="section-title mb-4">Featured products</h2>

        <div class="rail">
          <button
            class="rail-btn"
            type="button"
            :disabled="!featuredRail.canScrollLeft.value"
            :aria-label="featuredRail.leftLabel"
            @click="featuredRail.scrollBy(-1)"
          >
            <span aria-hidden="true">‹</span>
          </button>

          <div
            :ref="featuredRail.setViewport"
            class="featured-rail"
            @scroll.passive="featuredRail.update"
          >
            <article v-for="product in catalog.featured" :key="product.id" class="rail-item">
              <div class="thumb product-thumb">
                <RouterLink :to="{ name: 'product-details', params: { id: product.id } }">
                  <img :src="product.imageUrl" :alt="product.name" loading="lazy" />
                </RouterLink>
              </div>

              <div class="meta">
                <div>
                  <div class="cat">{{ product.categoryName }}</div>
                  <RouterLink :to="{ name: 'product-details', params: { id: product.id } }">
                    <div class="name">{{ product.name }}</div>
                  </RouterLink>
                  <div
                    v-if="product.ratingNum !== null"
                    class="rating"
                    :aria-label="`Rated ${formatRating(product.ratingNum)} out of 5`"
                  >
                    ★ {{ formatRating(product.ratingNum) }}
                    <span class="text-secondary">/ 5</span>
                  </div>
                </div>
                <div>
                  <div class="price">{{ formatCurrency(product.price) }}</div>
                  <div
                    v-if="product.discountPercent !== null && product.discountPercent > 0 && product.wasPrice"
                    class="old-price"
                  >
                    {{ formatCurrency(product.wasPrice) }}
                  </div>
                </div>
              </div>

              <div class="card-foot">
                <span class="stock" :class="stockClass(product)">{{ stockLabel(product) }}</span>
                <button
                  class="btn-pill"
                  type="button"
                  :disabled="busyProductId === product.id || !product.inStock"
                  @click="addToCart(product.id, product.name)"
                >
                  {{ product.inStock ? 'Add to cart' : 'Sold out' }}
                </button>
              </div>
            </article>
          </div>

          <button
            class="rail-btn"
            type="button"
            :disabled="!featuredRail.canScrollRight.value"
            :aria-label="featuredRail.rightLabel"
            @click="featuredRail.scrollBy(1)"
          >
            <span aria-hidden="true">›</span>
          </button>
        </div>
      </div>
    </section>

    <!-- Deals -->
    <section v-if="catalog.deals.length" id="deals" class="section">
      <div class="wrap">
        <p class="kicker">Save more</p>
        <h2 class="section-title mb-4">Deals &amp; promotions</h2>
        <div class="product-grid">
          <ProductCard
            v-for="product in catalog.deals"
            :key="product.id"
            :product="product"
            :busy="busyProductId === product.id"
            @add="addToCart($event.id, $event.name)"
          />
        </div>
      </div>
    </section>

    <!-- New arrivals -->
    <section v-if="catalog.newArrivals.length" id="new" class="section">
      <div class="wrap">
        <p class="kicker">Just landed</p>
        <h2 class="section-title mb-4">New arrivals</h2>
        <div class="product-grid">
          <ProductCard
            v-for="product in catalog.newArrivals"
            :key="product.id"
            :product="product"
            :busy="busyProductId === product.id"
            @add="addToCart($event.id, $event.name)"
          />
        </div>
      </div>
    </section>

    <!-- Benefits -->
    <section class="section">
      <div class="wrap">
        <p class="kicker">Why ShopHub</p>
        <h2 class="section-title mb-4">Shopping made simple</h2>
        <div class="row g-4">
          <div class="col-md-3">
            <strong>◈ Secure payments</strong>
            <p class="text-secondary small mb-0">Protected checkout on every order.</p>
          </div>
          <div class="col-md-3">
            <strong>◎ Fast delivery</strong>
            <p class="text-secondary small mb-0">Standard and express shipping options.</p>
          </div>
          <div class="col-md-3">
            <strong>↩ Easy returns</strong>
            <p class="text-secondary small mb-0">Simple returns within 7 days.</p>
          </div>
          <div class="col-md-3">
            <strong>★ Quality products</strong>
            <p class="text-secondary small mb-0">Reviewed picks across every category.</p>
          </div>
        </div>
      </div>
    </section>
  </template>
</template>