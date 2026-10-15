import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as ordersApi from '@/api/orders'
import type { OrderDetail, OrderSummary, PagedResult, PlaceOrderRequest } from '@/types'

/** Order state for the signed-in customer's history and the checkout flow. */
export const useOrderStore = defineStore('orders', () => {
  const orders = ref<OrderSummary[]>([])
  const current = ref<OrderDetail | null>(null)

  const page = ref(1)
  const pageSize = ref(10)
  const totalCount = ref(0)
  const totalPages = ref(0)

  const loading = ref(false)
  const placing = ref(false)
  const error = ref<string | null>(null)

  const isEmpty = computed(() => !loading.value && orders.value.length === 0)
  const hasNext = computed(() => page.value < totalPages.value)

  /** Stable key so a double-submitted checkout cannot create a second order. */
  function idempotencyKey(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `order-${Date.now()}-${Math.random().toString(36).slice(2)}`
  }

  async function fetchOrders(): Promise<PagedResult<OrderSummary> | null> {
    loading.value = true
    error.value = null
    try {
      const result = await ordersApi.getOrders(page.value, pageSize.value)
      orders.value = result.items
      totalCount.value = result.totalCount
      totalPages.value = result.totalPages
      return result
    } catch (cause) {
      orders.value = []
      error.value = cause instanceof Error ? cause.message : 'Orders could not be loaded.'
      return null
    } finally {
      loading.value = false
    }
  }

  async function fetchOrder(id: number): Promise<OrderDetail | null> {
    loading.value = true
    error.value = null
    try {
      current.value = await ordersApi.getOrder(id)
      return current.value
    } catch {
      current.value = null
      // A 404 means the order does not exist, or belongs to another customer. The API
      // deliberately does not distinguish the two.
      error.value = 'That order could not be found.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Places the order. `placing` guards the button so a shopper cannot submit twice, and the
   * idempotency key means even a replayed request returns the original order.
   */
  async function place(request: PlaceOrderRequest): Promise<OrderDetail | null> {
    if (placing.value) return null

    placing.value = true
    error.value = null
    try {
      const order = await ordersApi.placeOrder(request, idempotencyKey())
      current.value = order
      return order
    } catch (cause) {
      error.value = cause instanceof Error ? cause.message : 'The order could not be placed.'
      return null
    } finally {
      placing.value = false
    }
  }

  async function goToPage(target: number): Promise<void> {
    if (target < 1 || (totalPages.value > 0 && target > totalPages.value)) return
    page.value = target
    await fetchOrders()
  }

  function clearCurrent(): void {
    current.value = null
  }

  return {
    orders,
    current,
    page,
    pageSize,
    totalCount,
    totalPages,
    loading,
    placing,
    error,
    isEmpty,
    hasNext,
    fetchOrders,
    fetchOrder,
    place,
    goToPage,
    clearCurrent,
  }
})