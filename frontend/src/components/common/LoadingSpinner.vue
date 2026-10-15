<script setup lang="ts">
/** Accessible loading indicator. */
withDefaults(
  defineProps<{
    label?: string
    inline?: boolean
  }>(),
  { label: 'Loading…', inline: false },
)
</script>

<template>
  <div
    class="d-inline-flex align-items-center gap-2"
    :class="inline ? '' : 'justify-content-center py-5'"
    role="status"
    aria-live="polite"
  >
    <span class="spinner-border spinner-border-sm" aria-hidden="true" />
    <span class="text-secondary">{{ label }}</span>
  </div>
</template>