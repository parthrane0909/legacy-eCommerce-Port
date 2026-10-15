<script setup lang="ts">
/** Grid of product cards with loading, empty and error states. */
import ProductCard from './ProductCard.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { ProductSummary } from '@/types'

withDefaults(
  defineProps<{
    products: ProductSummary[]
    loading?: boolean
    error?: string | null
    busyProductId?: number | null
    emptyMessage?: string
  }>(),
  { loading: false, error: null, busyProductId: null, emptyMessage: undefined },
)

const emit = defineEmits<{ add: [product: ProductSummary] }>()
</script>

<template>
  <div>
    <LoadingSpinner v-if="loading" label="Loading products…" />

    <div v-else-if="error" class="error-state" role="alert">
      <h3>Something went wrong</h3>
      <p>{{ error }}</p>
    </div>

    <div v-else-if="products.length === 0" class="empty-state">
      <h3>No products found</h3>
      <p>{{ emptyMessage ?? 'Try a different search or category.' }}</p>
    </div>

    <div v-else class="product-grid">
      <ProductCard
        v-for="product in products"
        :key="product.id"
        :product="product"
        :busy="busyProductId === product.id"
        @add="emit('add', $event)"
      />
    </div>
  </div>
</template>