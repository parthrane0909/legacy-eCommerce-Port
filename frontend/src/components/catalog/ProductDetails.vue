<script setup lang="ts">
/**
 * Product detail panel: gallery, variant picker, stock state, quantity and add-to-cart.
 *
 * Extracted from `ProductDetailsView.vue` so the view owns data loading and routing only.
 * The gallery preserves the legacy detail-page layout: a large primary image with a
 * thumbnail strip beneath it.
 */
import { computed, ref, watch } from 'vue'
import { formatCurrency, formatRating } from '@/utils/format'
import type { ProductDetail } from '@/types'

const props = withDefaults(
  defineProps<{
    product: ProductDetail
    /** Variant currently chosen, or null when the product has no variants. */
    selectedVariantId?: number | null
    quantity?: number
    /** True while an add-to-cart request is in flight. */
    adding?: boolean
  }>(),
  { selectedVariantId: null, quantity: 1, adding: false },
)

const emit = defineEmits<{
  'update:selectedVariantId': [id: number | null]
  'update:quantity': [quantity: number]
  add: []
}>()

/** Which gallery image is currently shown; null means "use the product's primary image". */
const activeImage = ref<string | null>(null)

// Navigating between products must not leave the previous product's image selected.
watch(
  () => props.product.id,
  () => {
    activeImage.value = null
  },
)

const rating = computed(() => formatRating(props.product.ratingNum))

const stockLabel = computed(() => {
  if (!props.product.inStock) return 'Out of stock'
  if (props.product.stock <= 12) return `Only ${props.product.stock} left in stock`
  return 'In stock — ships in 24h'
})

const stockClass = computed(() => {
  if (!props.product.inStock) return 'out'
  return props.product.stock <= 12 ? 'low' : 'in'
})

/** Maximum selectable quantity: capped by stock and by the API's per-line limit. */
const maxQuantity = computed(() => Math.max(1, Math.min(99, props.product.stock)))
</script>

<template>
  <div class="row g-5">
    <div class="col-lg-7">
      <img
        v-if="activeImage ?? product.imageUrl"
        :src="activeImage ?? product.imageUrl"
        :alt="product.name"
        style="width: 100%; border-radius: 24px; aspect-ratio: 1; object-fit: cover"
      />


      <div v-if="product.images.length > 1" class="d-flex gap-2 mt-3 flex-wrap">
        <button
          v-for="image in product.images"
          :key="image.id"
          class="border-0 bg-transparent p-0"
          type="button"
          :aria-label="`Show ${image.altText ?? image.url}`"
          @click="activeImage = image.url"
        >
          <img
            :src="image.url"
            :alt="image.altText ?? ''"
            style="width: 84px; height: 84px; object-fit: cover; border-radius: 12px; border: 2px solid var(--line)"
          />
        </button>
      </div>
    </div>

    <div class="col-lg-5">
      <p class="kicker mb-1">
        <RouterLink :to="{ name: 'products', query: { categoryId: product.categoryId } }">
          {{ product.categoryName }}
        </RouterLink>
      </p>
      <h1 class="section-title h2">{{ product.name }}</h1>

      <div v-if="product.discountPercent" class="my-2">
        <span class="badge-sale" style="position: static">-{{ product.discountPercent }}% today</span>
      </div>

      <div class="d-flex align-items-baseline gap-3 my-3">
        <span class="fs-3 fw-bold">{{ formatCurrency(product.price) }}</span>
        <span v-if="product.wasPrice" class="text-secondary text-decoration-line-through">
          {{ formatCurrency(product.wasPrice) }}
        </span>
      </div>

      <div v-if="rating" class="rating" :aria-label="`Rated ${rating} out of 5`">
        ★ {{ rating }} <span class="text-secondary">/ 5</span>
      </div>

      <p class="mt-2"><span class="stock" :class="stockClass">{{ stockLabel }}</span></p>
      <p class="text-secondary">{{ product.description }}</p>

      <div v-if="product.variants.length" class="my-4">
        <label class="kicker mb-2 d-block">Choose an option</label>
        <div class="d-flex gap-2 flex-wrap">
          <button
            v-for="variant in product.variants"
            :key="variant.id"
            class="pill"
            :class="{ active: selectedVariantId === variant.id }"
            type="button"
            :aria-pressed="selectedVariantId === variant.id"
            @click="emit('update:selectedVariantId', variant.id)"
          >
            {{ variant.name }}
            <span v-if="variant.priceDelta" class="text-secondary ms-1">
              +{{ formatCurrency(variant.priceDelta) }}
            </span>
          </button>
        </div>
      </div>

      <div class="d-flex gap-2 align-items-end my-4">
        <div class="field mb-0">
          <label for="detail-qty">Quantity</label>
          <input
            id="detail-qty"
            :value="quantity"
            class="form-control"
            style="width: 96px"
            type="number"
            min="1"
            :max="maxQuantity"
            @input="emit('update:quantity', Number(($event.target as HTMLInputElement).value) || 1)"
          />
        </div>
      </div>

      <button
        class="btn-pill btn-accent w-100 justify-content-center"
        type="button"
        :disabled="adding || !product.inStock"
        @click="emit('add')"
      >
        <span v-if="adding" class="spinner-border spinner-border-sm" aria-hidden="true" />
        {{ !product.inStock ? 'Out of stock' : adding ? 'Adding…' : 'Add to cart' }}
      </button>
    </div>
  </div>
</template>
