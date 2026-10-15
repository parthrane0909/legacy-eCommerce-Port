<script setup lang="ts">
/** Slide-in mini-cart drawer, opened from the header cart button. */
import { onMounted } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useUiStore } from '@/stores/ui'
import CartItemRow from './CartItemRow.vue'

const cart = useCartStore()
const ui = useUiStore()

onMounted(() => {
  if (cart.isEmpty) void cart.fetch()
})

async function onUpdate(productId: number, quantity: number): Promise<void> {
  await cart.update(productId, quantity)
}

async function onRemove(productId: number): Promise<void> {
  await cart.remove(productId)
}
</script>

<template>
  <Teleport to="body">
    <div v-if="ui.miniCartOpen">
      <div class="drawer-backdrop" @click="ui.closeMiniCart()" />

      <aside class="drawer" role="dialog" aria-modal="true" aria-label="Shopping cart">
        <div class="drawer-head d-flex justify-content-between align-items-center">
          <h2 class="h5 mb-0">Your cart</h2>
          <button class="btn-pill btn-ghost py-1 px-3" type="button" @click="ui.closeMiniCart()">
            ✕
          </button>
        </div>

        <div class="drawer-body">
          <p v-if="cart.isEmpty" class="text-secondary text-center py-5">
            Your cart is empty.
          </p>

          <CartItemRow
            v-for="item in cart.items"
            :key="item.id"
            :item="item"
            :busy="cart.loading"
            compact
            @update="onUpdate"
            @remove="onRemove"
          />
        </div>

        <div v-if="!cart.isEmpty" class="drawer-foot">
          <div class="d-flex justify-content-between fw-semibold mb-3">
            <span>Subtotal</span>
            <span>{{ new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(cart.subtotal) }}</span>
          </div>
          <div class="d-flex gap-2">
            <RouterLink
              class="btn-pill flex-fill justify-content-center"
              :to="{ name: 'cart' }"
              @click="ui.closeMiniCart()"
            >
              View cart
            </RouterLink>
            <RouterLink
              class="btn-pill btn-accent flex-fill justify-content-center"
              :to="{ name: 'checkout' }"
              @click="ui.closeMiniCart()"
            >
              Checkout
            </RouterLink>
          </div>
        </div>
      </aside>
    </div>
  </Teleport>
</template>