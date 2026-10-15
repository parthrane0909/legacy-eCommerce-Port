<script setup lang="ts">
/** Dashboard counters and recent orders. */
import { onMounted, ref } from 'vue'
import * as adminApi from '@/api/admin'
import { toErrorMessage } from '@/api/client'
import { formatCurrency, formatDate } from '@/utils/format'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { AdminDashboard } from '@/types'

const dashboard = ref<AdminDashboard | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    dashboard.value = await adminApi.getDashboard()
  } catch (cause) {
    error.value = toErrorMessage(cause, 'The dashboard could not be loaded.')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <LoadingSpinner v-if="loading" label="Loading dashboard…" />

  <div v-else-if="error" class="error-state" role="alert">
    <h3>Something went wrong</h3>
    <p>{{ error }}</p>
    <button class="btn-pill mt-2" type="button" @click="load">Try again</button>
  </div>

  <div v-else-if="dashboard">
    <div class="stat-grid mb-4">
      <div class="stat-card">
        <strong>{{ dashboard.productCount }}</strong>
        <span>Products</span>
      </div>
      <div class="stat-card">
        <strong>{{ dashboard.categoryCount }}</strong>
        <span>Categories</span>
      </div>
      <div class="stat-card">
        <strong>{{ dashboard.customerCount }}</strong>
        <span>Customers</span>
      </div>
      <div class="stat-card">
        <strong>{{ dashboard.orderCount }}</strong>
        <span>Orders</span>
      </div>
      <div class="stat-card">
        <strong>{{ dashboard.lowStockCount }}</strong>
        <span>Low stock</span>
      </div>
      <div class="stat-card">
        <strong>{{ formatCurrency(dashboard.totalRevenue) }}</strong>
        <span>Revenue</span>
      </div>
    </div>

    <div class="summary-card" style="position: static">
      <h2 class="h6 mb-3">Recent orders</h2>

      <p v-if="dashboard.recentOrders.length === 0" class="text-secondary small mb-0">
        No orders have been placed yet.
      </p>

      <table v-else class="data-table">
        <thead>
          <tr>
            <th>Order</th>
            <th>Customer</th>
            <th>Placed</th>
            <th>Status</th>
            <th class="text-end">Total</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="order in dashboard.recentOrders" :key="order.id">
            <td class="fw-semibold">{{ order.orderNumber }}</td>
            <td>{{ order.customerName }}<br /><span class="small text-secondary">{{ order.customerEmail }}</span></td>
            <td>{{ formatDate(order.orderDate) }}</td>
            <td><span class="badge-feat" style="position: static">{{ order.status }}</span></td>
            <td class="text-end">{{ formatCurrency(order.total) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>