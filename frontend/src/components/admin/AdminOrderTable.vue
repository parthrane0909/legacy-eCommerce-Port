<script setup lang="ts">
/** Admin order table with the status control. */
import { ORDER_STATUSES, type AdminOrder } from '@/types'
import { formatCurrency, formatDate } from '@/utils/format'

defineProps<{
  orders: AdminOrder[]
  busyId?: number | null
}>()

const emit = defineEmits<{ status: [id: number, status: string] }>()

/** Mirrors OrderStatus.CanTransition on the API, for a clear affordance. */
function nextStatuses(current: string): string[] {
  const forward: Record<string, string[]> = {
    Pending: ['Paid', 'Cancelled'],
    Paid: ['Shipped', 'Cancelled'],
    Shipped: ['Delivered'],
    Delivered: [],
    Cancelled: [],
  }
  return forward[current] ?? ORDER_STATUSES
}
</script>

<template>
  <table class="data-table">
    <thead>
      <tr>
        <th>Order</th>
        <th>Customer</th>
        <th>Placed</th>
        <th class="text-end">Items</th>
        <th class="text-end">Total</th>
        <th>Status</th>
        <th>Change status</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="order in orders" :key="order.id">
        <td class="fw-semibold">{{ order.orderNumber }}</td>
        <td>
          {{ order.customerName || 'Guest' }}
          <br />
          <span class="small text-secondary">{{ order.customerEmail }}</span>
        </td>
        <td>{{ formatDate(order.orderDate) }}</td>
        <td class="text-end">{{ order.itemCount }}</td>
        <td class="text-end">{{ formatCurrency(order.total) }}</td>
        <td><span class="badge-feat" style="position: static">{{ order.status }}</span></td>
        <td>
          <label class="visually-hidden" :for="`status-${order.id}`">
            Status for {{ order.orderNumber }}
          </label>
          <select
            :id="`status-${order.id}`"
            class="form-select form-select-sm"
            style="width: auto"
            :value="order.status"
            :disabled="busyId === order.id"
            @change="emit('status', order.id, ($event.target as HTMLSelectElement).value)"
          >
            <option :value="order.status" disabled>{{ order.status }}</option>
            <option v-for="status in nextStatuses(order.status)" :key="status" :value="status">
              {{ status }}
            </option>
          </select>
        </td>
      </tr>
    </tbody>
  </table>
</template>