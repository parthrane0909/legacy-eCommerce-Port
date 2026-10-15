<script setup lang="ts">
/** A single cart line with quantity control and remove action. */
import { computed } from 'vue'
import type { CartItem } from '@/types'
import { formatCurrency } from '@/utils/format'

const props = withDefaults(
  defineProps<{
    item: CartItem
    busy?: boolean
    compact?: boolean
  }>(),
  { busy: false, compact: false },
)

const emit = defineEmits<{
  update: [productId: number, quantity: number]
  remove: [productId: number]
}>()

/** Max selectable: the smaller of the API's line cap and what is in stock. */
const maxQuantity = computed(() => Math.max(1, Math.min(99, props.item.availableStock || 99)))

function onQuantity(event: Event): void {
  const value = Number.parseInt((event.target as HTMLInputElement).value, 10)
  if (Number.isNaN(value) || value === props.item.quantity) return
  emit('update', props.item.productId, value)
}
</script>

<template>
  <div class="cart-line" :class="compact ? 'py-2' : ''">
    <img :src="item.imageUrl" :alt="item.productName" loading="lazy" />

    <div>
      <div class="fw-semibold">{{ item.productName }}</div>
      <div v-if="item.variantName" class="text-secondary small">{{ item.variantName }}</div>
      <div class="small text-secondary">
        {{ formatCurrency(item.unitPrice) }} each
        <span v-if="!item.inStock" class="stock out ms-2">Out of stock</span>
      </div>
    </div>

    <label class="visually-hidden" :for="`qty-${item.id}`">Quantity for {{ item.productName }}</label>
    <input
      :id="`qty-${item.id}`"
      class="form-control form-control-sm"
      style="width: 84px"
      type="number"
      min="1"
      :max="maxQuantity"
      :value="item.quantity"
      :disabled="busy"
      @change="onQuantity"
    />

    <div class="fw-semibold">{{ formatCurrency(item.lineTotal) }}</div>

    <button
      class="btn-pill btn-ghost py-1 px-3"
      type="button"
      :disabled="busy"
      :aria-label="`Remove ${item.productName} from cart`"
      @click="emit('remove', item.productId)"
    >
      ✕
    </button>
  </div>
</template>