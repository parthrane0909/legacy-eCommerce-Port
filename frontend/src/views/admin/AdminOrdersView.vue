<script setup lang="ts">
/** Admin order management: list every customer's orders and advance their status. */
import { onMounted, ref } from 'vue'
import * as adminApi from '@/api/admin'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'
import AdminOrderTable from '@/components/admin/AdminOrderTable.vue'
import Pagination from '@/components/common/Pagination.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import { ORDER_STATUSES, type AdminOrder } from '@/types'

const ui = useUiStore()

const orders = ref<AdminOrder[]>([])
const page = ref(1)
const totalPages = ref(0)
const totalCount = ref(0)
const status = ref('')

const loading = ref(true)
const busyId = ref<number | null>(null)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const result = await adminApi.getAdminOrders(page.value, 20, status.value)
    orders.value = result.items
    totalPages.value = result.totalPages
    totalCount.value = result.totalCount
  } catch (cause) {
    error.value = toErrorMessage(cause, 'Orders could not be loaded.')
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function onStatus(id: number, next: string): Promise<void> {
  busyId.value = id
  try {
    await adminApi.updateOrderStatus(id, next)
    ui.notifySuccess('Order status updated.')
    await load()
  } catch (cause) {
    ui.notifyError(toErrorMessage(cause, 'The status could not be updated.'))
    await load() // Resync: the select optimistically moved, so restore the server value.
  } finally {
    busyId.value = null
  }
}

async function goToPage(target: number): Promise<void> {
  page.value = target
  await load()
}
</script>

<template>
  <div>
    <div class="d-flex flex-wrap gap-2 justify-content-between align-items-center mb-3">
      <h2 class="h5 mb-0">Orders ({{ totalCount }})</h2>

      <div class="d-flex gap-2 align-items-center">
        <label class="visually-hidden" for="status-filter">Filter by status</label>
        <select id="status-filter" v-model="status" class="form-select form-select-sm" style="width: auto" @change="load">
          <option value="">All statuses</option>
          <option v-for="s in ORDER_STATUSES" :key="s" :value="s">{{ s }}</option>
        </select>
      </div>
    </div>

    <LoadingSpinner v-if="loading" label="Loading orders…" />

    <div v-else-if="error" class="error-state" role="alert">
      <h3>Something went wrong</h3>
      <p>{{ error }}</p>
      <button class="btn-pill mt-2" type="button" @click="load">Try again</button>
    </div>

    <div v-else-if="orders.length === 0" class="empty-state">
      <h3>No orders found</h3>
      <p>Orders appear here as customers check out.</p>
    </div>

    <template v-else>
      <AdminOrderTable :orders="orders" :busy-id="busyId" @status="onStatus" />
      <Pagination :page="page" :total-pages="totalPages" :disabled="loading" @change="goToPage" />
    </template>
  </div>
</template>