<script setup lang="ts">
/** Page navigation control, driven by the API's pagination metadata. */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    page: number
    totalPages: number
    disabled?: boolean
  }>(),
  { disabled: false },
)

const emit = defineEmits<{ change: [page: number] }>()

/** A compact window of page numbers around the current page. */
const visiblePages = computed<(number | 'gap')[]>(() => {
  const total = props.totalPages
  const current = props.page
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)

  const pages: (number | 'gap')[] = [1]
  const start = Math.max(2, current - 1)
  const end = Math.min(total - 1, current + 1)

  if (start > 2) pages.push('gap')
  for (let i = start; i <= end; i++) pages.push(i)
  if (end < total - 1) pages.push('gap')

  pages.push(total)
  return pages
})

function go(target: number): void {
  if (props.disabled || target < 1 || target > props.totalPages || target === props.page) return
  emit('change', target)
}
</script>

<template>
  <nav v-if="totalPages > 1" class="d-flex flex-wrap gap-2 justify-content-center mt-4" aria-label="Pagination">
    <button
      class="pill"
      type="button"
      :disabled="disabled || page <= 1"
      aria-label="Previous page"
      @click="go(page - 1)"
    >
      ←
    </button>

    <template v-for="(item, index) in visiblePages" :key="`${item}-${index}`">
      <span v-if="item === 'gap'" class="pill border-0 bg-transparent" aria-hidden="true">…</span>
      <button
        v-else
        class="pill"
        type="button"
        :class="{ active: item === page }"
        :disabled="disabled"
        :aria-current="item === page ? 'page' : undefined"
        @click="go(item)"
      >
        {{ item }}
      </button>
    </template>

    <button
      class="pill"
      type="button"
      :disabled="disabled || page >= totalPages"
      aria-label="Next page"
      @click="go(page + 1)"
    >
      →
    </button>
  </nav>
</template>