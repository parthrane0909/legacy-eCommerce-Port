<script setup lang="ts">
/** Recursive category navigation for the shop sidebar. */
import type { Category } from '@/types'

const props = withDefaults(
  defineProps<{
    categories: Category[]
    activeId: number | null
    /**
     * True only for the outermost list. This component renders itself recursively for each
     * category's children, so without the flag the "All products" entry would be emitted at
     * every depth and repeated inside every category group. Only the root shows it.
     */
    root?: boolean
  }>(),
  { root: true },
)

const emit = defineEmits<{ select: [id: number | null] }>()
</script>

<template>
  <ul class="cat-tree">
    <li v-if="props.root">
      <button type="button" :class="{ active: activeId === null }" @click="emit('select', null)">
        <span>All products</span>
      </button>
    </li>

    <li v-for="category in categories" :key="category.id">
      <button
        type="button"
        :class="{ active: activeId === category.id }"
        :aria-pressed="activeId === category.id"
        @click="emit('select', category.id)"
      >
        <span>{{ category.name }}</span>
        <span class="count">{{ category.productCount }}</span>
      </button>

      <CategoryTree
        v-if="category.children.length > 0"
        :root="false"
        :categories="category.children"
        :active-id="activeId"
        @select="emit('select', $event)"
      />
    </li>
  </ul>
</template>

