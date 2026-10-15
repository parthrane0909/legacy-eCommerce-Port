<script setup lang="ts">
/** Order detail, including the address and item snapshots taken at purchase time. */
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useOrderStore } from '@/stores/orders'
import { formatCurrency, formatDate } from '@/utils/format'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'

const orders = useOrderStore()
const route = useRoute()

onMounted(async () => {
  const id = Number(route.params.id)
  if (!Number.isNaN(id)) await orders.fetchOrder(id)
})
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">
        <RouterLink :to="{ name: 'order-history' }">← Order history</RouterLink>
      </p>

      <LoadingSpinner v-if="orders.loading" label="Loading order…" />

      <div v-else-if="orders.error || !orders.current" class="error-state" role="alert">
        <h3>Order not found</h3>
        <p>{{ orders.error ?? 'That order could not be found.' }}</p>
      </div>

      <div v-else class="cart-layout">
        <div class="summary-card" style="position: static">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h1 class="h4 mb-0">{{ orders.current.orderNumber }}</h1>
            <span class="badge-feat" style="position: static">{{ orders.current.status }}</span>
          </div>

          <p class="text-secondary small mb-4">
            Placed {{ formatDate(orders.current.orderDate) }} ·
            {{ orders.current.shippingMethod }} shipping
          </p>

          <table class="table table-sm align-middle">
            <thead>
              <tr>
                <th>Product</th>
                <th class="text-end">Qty</th>
                <th class="text-end">Unit price</th>
                <th class="text-end">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in orders.current.items" :key="item.id">
                <td>
                  {{ item.productName }}
                  <span v-if="item.variantName" class="text-secondary">({{ item.variantName }})</span>
                </td>
                <td class="text-end">{{ item.quantity }}</td>
                <td class="text-end">{{ formatCurrency(item.unitPrice) }}</td>
                <td class="text-end">{{ formatCurrency(item.lineTotal) }}</td>
              </tr>
            </tbody>
          </table>

          <div class="summary-row"><span>Subtotal</span><span>{{ formatCurrency(orders.current.subtotal) }}</span></div>
          <div class="summary-row">
            <span>Shipping</span>
            <span>{{ formatCurrency(orders.current.shippingCost) }}</span>
          </div>
          <div class="summary-row summary-total">
            <span>Total</span>
            <span>{{ formatCurrency(orders.current.total) }}</span>
          </div>

          <p class="small text-secondary mt-3 mb-0">
            Payment method: {{ orders.current.paymentMethod }} (demonstration — no payment taken).
          </p>
        </div>

        <aside class="summary-card">
          <h2 class="h6 mb-3">Shipping to</h2>
          <p class="small mb-4">
            {{ orders.current.shippingAddress.fullName }}<br />
            {{ orders.current.shippingAddress.line1 }}<br />
            <span v-if="orders.current.shippingAddress.line2">
              {{ orders.current.shippingAddress.line2 }}<br />
            </span>
            {{ orders.current.shippingAddress.city }},
            {{ orders.current.shippingAddress.state }}
            {{ orders.current.shippingAddress.postalCode }}<br />
            {{ orders.current.shippingAddress.country }}
          </p>

          <h2 class="h6 mb-2">Billing to</h2>
          <p class="small mb-0">
            {{ orders.current.billingAddress.fullName }}<br />
            {{ orders.current.billingAddress.line1 }},
            {{ orders.current.billingAddress.city }},
            {{ orders.current.billingAddress.country }}
          </p>
        </aside>
      </div>
    </div>
  </section>
</template>