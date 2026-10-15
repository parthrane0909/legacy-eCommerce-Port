<script setup lang="ts">
/** Toast notifications, exposed to assistive technology via aria-live. */
import { useUiStore } from '@/stores/ui'

const ui = useUiStore()
</script>

<template>
  <div class="toast-stack" aria-live="polite" aria-atomic="false">
    <div
      v-for="toast in ui.toasts"
      :key="toast.id"
      class="toast"
      :class="toast.tone"
      role="status"
      @click="ui.dismiss(toast.id)"
    >
      {{ toast.message }}
    </div>
  </div>
</template>