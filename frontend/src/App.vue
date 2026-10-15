<script setup lang="ts">
/** Application shell: header, routed view, footer, mini-cart drawer and toasts. */
import { onMounted } from 'vue'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
import CartDrawer from '@/components/cart/CartDrawer.vue'
import NotificationToast from '@/components/common/NotificationToast.vue'
import { useCartStore } from '@/stores/cart'

const cart = useCartStore()

onMounted(() => {
  // Load the cart once so the header badge is correct on first paint.
  void cart.fetch()
})
</script>

<template>
  <div class="d-flex flex-column min-vh-100">
    <AppHeader />

    <main class="flex-grow-1">
      <RouterView v-slot="{ Component }">
        <component :is="Component" />
      </RouterView>
    </main>

    <AppFooter />
    <CartDrawer />
    <NotificationToast />
  </div>
</template>