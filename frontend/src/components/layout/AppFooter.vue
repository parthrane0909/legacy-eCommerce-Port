<script setup lang="ts">
/** Site footer, carried over from the legacy storefront's link structure. */
</script>

<template>
  <footer class="site-footer">
    <div class="wrap">
      <div class="footer-grid">
        <div>
          <RouterLink :to="{ name: 'home' }" class="brand" style="color: #fff">
            SHOP<span>HUB</span>
          </RouterLink>
          <p style="color: #8a877f; max-width: 320px; margin-top: 14px; font-size: 13px; line-height: 1.7">
            Everything you need, in one place. Electronics, home essentials, books, sports gear and
            more — with secure checkout and fast delivery.
          </p>
        </div>

        <div>
          <h5>Shop</h5>
          <div class="footer-col">
            <RouterLink :to="{ name: 'products' }">All products</RouterLink>
            <RouterLink :to="{ name: 'products', query: { categoryId: 1 } }">Categories</RouterLink>
            <RouterLink :to="{ name: 'home', hash: '#deals' }">Deals</RouterLink>
            <RouterLink :to="{ name: 'home', hash: '#new' }">New arrivals</RouterLink>
          </div>
        </div>

        <div>
          <h5>Account</h5>
          <div class="footer-col">
            <RouterLink :to="{ name: 'login' }">Sign in</RouterLink>
            <RouterLink :to="{ name: 'register' }">Create account</RouterLink>
            <RouterLink :to="{ name: 'order-history' }">Order history</RouterLink>
          </div>
        </div>

        <div>
          <h5>Customer service</h5>
          <div class="footer-col">
            <RouterLink :to="{ name: 'cart' }">Shopping cart</RouterLink>
            <RouterLink :to="{ name: 'checkout' }">Checkout</RouterLink>
            <RouterLink :to="{ name: 'order-history' }">Track orders</RouterLink>
          </div>
        </div>
      </div>

      <div class="footer-bottom">
        <span>© {{ new Date().getFullYear() }} ShopHub</span>
        <span>Vue 3 · .NET 10 Minimal API</span>
      </div>
    </div>
  </footer>
</template>