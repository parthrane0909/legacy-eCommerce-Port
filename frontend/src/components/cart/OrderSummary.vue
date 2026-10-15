<script setup lang="ts">
/**
 * Order summary card: item count, subtotal, optional shipping and total.
 *
 * Extracted from the duplicated markup that previously lived in `CartView.vue` and
 * `CheckoutView.vue`. The `sticky` prop controls whether the card floats beside the page
 * content (cart, checkout) or sits inline (order detail), which is the only difference
 * between the two original copies.
 */
import { computed } from 'vue'
import { formatCurrency } from '@/utils/format'

const props = withDefaults(
  defineProps<{
    itemCount: number
    subtotal: number
    /** When provided, a shipping row is rendered. */
    shippingCost?: number | null
    /** Label for the shipping row, e.g. "Shipping (Express)". */
    shippingLabel?: string
    /** Text for the total row's caption. */
    totalLabel?: string
    sticky?: boolean
    /** Optional footnote, e.g. "Totals are re-verified by the server." */
    note?: string
  }>(),
  {
    shippingCost: null,
    shippingLabel: 'Shipping',
    totalLabel: 'Total',
    sticky: false,
    note: undefined,
  },
)


const hasShipping = computed(() => props.shippingCost !== null && props.shippingCost !== undefined)

const total = computed(() => props.subtotal + (props.shippingCost ?? 0))
</script>

<template>
  <aside class="summary-card" :style="sticky ? undefined : 'position: static'">
    <h2 class="h6 mb-3">Order summary</h2>

    <div class="summary-row">
      <span>Items</span>
      <span>{{ itemCount }}</span>
    </div>

    <div class="summary-row">
      <span>Subtotal</span>
      <span>{{ formatCurrency(subtotal) }}</span>
    </div>

    <div v-if="hasShipping" class="summary-row">
      <span>{{ shippingLabel }}</span>
      <span>{{ formatCurrency(shippingCost ?? 0) }}</span>
    </div>
    <div v-else class="summary-row">
      <span>Shipping</span>
      <span class="text-secondary">Calculated at checkout</span>
    </div>

    <div class="summary-row summary-total">
      <span>{{ totalLabel }}</span>
      <span>{{ formatCurrency(total) }}</span>
    </div>

    <slot name="actions" />

    <p v-if="note" class="small text-secondary mt-3 mb-0">{{ note }}</p>
  </aside>
</template>
