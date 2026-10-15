<script setup lang="ts">
/**
 * Product detail page. Owns loading the product and adding to the cart; the presentation is
 * `ProductDetails`.
 */
import { onMounted, ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import ProductDetails from '@/components/catalog/ProductDetails.vue'
import { useCatalogStore } from '@/stores/catalog'
import { useCartStore } from '@/stores/cart'
import { useUiStore } from '@/stores/ui'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'

const catalog = useCatalogStore()
const cart = useCartStore()
const ui = useUiStore()
const route = useRoute()

const selectedVariantId = ref<number | null>(null)
const quantity = ref(1)
const adding = ref(false)

onMounted(async () => {
  const id = Number(route.params.id)
  if (Number.isNaN(id)) return

  const loaded = await catalog.fetchProduct(id)
  if (loaded && loaded.variants.length > 0) {
    selectedVariantId.value = loaded.variants[0].id
  }
})

const detail = computed(() => catalog.currentProduct)

async function addToCart(): Promise<void> {
  const product = detail.value
  if (!product) return

  adding.value = true
  await cart.add(product.id, quantity.value, selectedVariantId.value)

  if (cart.error) ui.notifyError(cart.error)
  else ui.notifySuccess(`${product.name} added to cart`)

  adding.value = false
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <LoadingSpinner v-if="catalog.loading" label="Loading product…" />

      <div v-else-if="catalog.error" class="error-state" role="alert">
        <h3>Product not found</h3>
        <p>{{ catalog.error }}</p>
        <RouterLink class="btn-pill mt-2" :to="{ name: 'products' }">Back to catalogue</RouterLink>
      </div>

      <ProductDetails
        v-else-if="detail"
        :product="detail"
        :selected-variant-id="selectedVariantId"
        :quantity="quantity"
        :adding="adding"
        @update:selected-variant-id="selectedVariantId = $event"
        @update:quantity="quantity = $event"
        @add="addToCart"
      />
    </div>
  </section>
</template>
