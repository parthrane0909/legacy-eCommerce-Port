<script setup lang="ts">
/** Paged order history for the signed-in customer. */
import { onMounted } from 'vue'
import { useOrderStore } from '@/stores/orders'
import { formatCurrency, formatDate } from '@/utils/format'
import Pagination from '@/components/common/Pagination.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'

const orders = useOrderStore()

onMounted(() => void orders.fetchOrders())

async function goToPage(page: number): Promise<void> {
  await orders.goToPage(page)
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">Account</p>
      <h1 class="section-title mb-4">Order history</h1>

      <LoadingSpinner v-if="orders.loading" label="Loading your orders…" />

      <div v-else-if="orders.error" class="error-state" role="alert">
        <h3>Something went wrong</h3>
        <p>{{ orders.error }}</p>
      </div>

      <div v-else-if="orders.isEmpty" class="empty-state">
        <h3>No orders yet</h3>
        <p>When you place an order it will appear here.</p>
        <RouterLink class="btn-pill btn-accent mt-2" :to="{ name: 'products' }">
          Start shopping →
        </RouterLink>
      </div>

      <template v-else>
        <table class="data-table">
          <thead>
            <tr>
              <th>Order</th>
              <th>Placed</th>
              <th>Status</th>
              <th class="text-end">Items</th>
              <th class="text-end">Total</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr v-for="order in orders.orders" :key="order.id">
              <td class="fw-semibold">{{ order.orderNumber }}</td>
              <td>{{ formatDate(order.orderDate) }}</td>
              <td><span class="badge-feat" style="position: static">{{ order.status }}</span></td>
              <td class="text-end">{{ order.itemCount }}</td>
              <td class="text-end">{{ formatCurrency(order.total) }}</td>
              <td class="text-end">
                <RouterLink
                  class="btn-pill btn-ghost py-1 px-3"
                  :to="{ name: 'order-details', params: { id: order.id } }"
                >
                  View
                </RouterLink>
              </td>
            </tr>
          </tbody>
        </table>

        <Pagination
          :page="orders.page"
          :total-pages="orders.totalPages"
          :disabled="orders.loading"
          @change="goToPage"
        />
      </template>
    </div>
  </section>
</template>