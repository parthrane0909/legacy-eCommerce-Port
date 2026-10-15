<script setup lang="ts">
/** 404 page for unmatched routes. */
</script>

<template>
  <section class="page">
    <div class="wrap">
      <div class="empty-state">
        <p class="kicker">404</p>
        <h1 class="section-title h2">We couldn't find that page</h1>
        <p>The link may be out of date, or the item may no longer be available.</p>
        <RouterLink class="btn-pill btn-accent mt-2" :to="{ name: 'home' }">
          Back to the storefront
        </RouterLink>
      </div>
    </div>
  </section>
</template>