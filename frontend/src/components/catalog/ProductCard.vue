<script setup lang="ts">
/**
 * Product card, matching the legacy grid tile: image, discount badge, category, rating,
 * price with a struck-through "was" price, stock label and an add-to-cart control.
 */
import { computed } from 'vue'
import type { ProductSummary } from '@/types'
import { formatCurrency, formatRating } from '@/utils/format'

const props = withDefaults(
  defineProps<{
    product: ProductSummary
    busy?: boolean
  }>(),
  { busy: false },
)

const emit = defineEmits<{ add: [product: ProductSummary] }>()

const rating = computed(() => formatRating(props.product.ratingNum))
const onSale = computed(() => props.product.discountPercent !== null && props.product.discountPercent > 0)
const stockLabel = computed(() => {
  if (!props.product.inStock) return 'Out of stock'
  if (props.product.stock <= 12) return `Only ${props.product.stock} left`
  return 'In stock'
})
const stockClass = computed(() => {
  if (!props.product.inStock) return 'out'
  return props.product.stock <= 12 ? 'low' : 'in'
})
</script>

<template>
  <article class="product-card">
    <div class="thumb product-thumb">
      <span v-if="onSale" class="badge-sale">-{{ product.discountPercent }}%</span>
      <span v-else-if="product.isFeatured" class="badge-feat">Featured</span>
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
        <div v-if="rating" class="rating" :aria-label="`Rated ${rating} out of 5`">
          ★ {{ rating }} <span class="text-secondary">/ 5</span>
        </div>
      </div>

      <div>
        <div class="price">{{ formatCurrency(product.price) }}</div>
        <div v-if="onSale && product.wasPrice" class="old-price">
          {{ formatCurrency(product.wasPrice) }}
        </div>
      </div>
    </div>

    <div class="card-foot">
      <span class="stock" :class="stockClass">{{ stockLabel }}</span>
      <button
        class="btn-pill"
        type="button"
        :disabled="busy || !product.inStock"
        @click="emit('add', product)"
      >
        {{ product.inStock ? 'Add to cart' : 'Sold out' }}
      </button>
    </div>
  </article>
</template>