<script setup lang="ts">
/**
 * Admin shell. Nested routes render into the content slot, with the admin nav alongside.
 * The route guard and the API's role policy both protect these screens.
 */
import AdminDashboardMetrics from './AdminDashboardMetrics.vue'
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">Administration</p>
      <h1 class="section-title mb-4">Admin</h1>

      <div class="admin-shell">
        <nav class="admin-nav" aria-label="Admin sections">
          <RouterLink :to="{ name: 'admin-dashboard' }" exact-active-class="router-link-active">
            Dashboard
          </RouterLink>
          <RouterLink :to="{ name: 'admin-products' }">Products</RouterLink>
          <RouterLink :to="{ name: 'admin-categories' }">Categories</RouterLink>
          <RouterLink :to="{ name: 'admin-orders' }">Orders</RouterLink>
        </nav>

        <div style="min-width: 0">
          <!-- The index route renders the metrics panel; child routes replace it. -->
          <RouterView v-slot="{ Component }">
            <component :is="Component ?? AdminDashboardMetrics" />
          </RouterView>
        </div>
      </div>
    </div>
  </section>
</template>