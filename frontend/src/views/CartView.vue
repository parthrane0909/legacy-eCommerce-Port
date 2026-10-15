<script setup lang="ts">
/** Full cart page: line items, totals and the route into checkout. */
import { onMounted } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useUiStore } from '@/stores/ui'
import CartItemRow from '@/components/cart/CartItemRow.vue'
import OrderSummary from '@/components/cart/OrderSummary.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'

const cart = useCartStore()
const ui = useUiStore()

onMounted(() => void cart.fetch())

async function onUpdate(productId: number, quantity: number): Promise<void> {
  await cart.update(productId, quantity)
  if (cart.error) ui.notifyError(cart.error)
}

async function onRemove(productId: number): Promise<void> {
  await cart.remove(productId)
  if (cart.error) ui.notifyError(cart.error)
  else ui.notifySuccess('Item removed')
}

async function onClear(): Promise<void> {
  await cart.clear()
  if (cart.error) ui.notifyError(cart.error)
  else ui.notifySuccess('Cart cleared')
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
          <p class="kicker">Cart</p>
          <h1 class="section-title mb-0">Shopping cart</h1>
        </div>
        <button
          v-if="!cart.isEmpty"
          class="btn-pill btn-ghost"
          type="button"
          :disabled="cart.loading"
          @click="onClear"
        >
          Clear cart
        </button>
      </div>

      <LoadingSpinner v-if="cart.loading && cart.isEmpty" label="Loading your cart…" />

      <div v-else-if="cart.error && cart.isEmpty" class="error-state" role="alert">
        <h3>Something went wrong</h3>
        <p>{{ cart.error }}</p>
      </div>

      <div v-else-if="cart.isEmpty" class="empty-state">
        <h3>Your cart is empty</h3>
        <p>Browse the catalogue and add something you like.</p>
        <RouterLink class="btn-pill btn-accent mt-2" :to="{ name: 'products' }">
          Browse products →
        </RouterLink>
      </div>

      <div v-else class="cart-layout">
        <div>
          <div v-for="item in cart.items" :key="item.id">
            <CartItemRow :item="item" :busy="cart.loading" @update="onUpdate" @remove="onRemove" />
          </div>

          <div v-if="cart.warnings.length" class="alert alert-warning mt-3" role="status">
            <ul class="mb-0 ps-3">
              <li v-for="(warning, index) in cart.warnings" :key="index">{{ warning }}</li>
            </ul>
          </div>

          <div v-if="cart.error" class="alert alert-danger mt-3" role="alert">{{ cart.error }}</div>
        </div>

        <OrderSummary
          :item-count="cart.itemCount"
          :subtotal="cart.subtotal"
          total-label="Subtotal"
        >
          <template #actions>
            <RouterLink
              class="btn-pill btn-accent w-100 justify-content-center mt-3"
              :to="{ name: 'checkout' }"
            >
              Proceed to checkout
            </RouterLink>
            <RouterLink
              class="btn-pill w-100 justify-content-center mt-2"
              :to="{ name: 'products' }"
            >
              Continue shopping
            </RouterLink>
          </template>
        </OrderSummary>
      </div>
    </div>
  </section>
</template>
