import client from './client'
import type { OrderDetail, OrderSummary, PagedResult, PlaceOrderRequest, ShippingOption } from '@/types'

/**
 * Places an order from the current cart.
 *
 * `idempotencyKey` collapses a repeated submission (for example a double-click) onto a single
 * order, so the shopper is never charged twice or ships two parcels.
 */
export async function placeOrder(
  request: PlaceOrderRequest,
  idempotencyKey?: string,
): Promise<OrderDetail> {
  const { data } = await client.post<OrderDetail>('/orders', request, {
    headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
  })
  return data
}

export async function getOrders(page = 1, pageSize = 10): Promise<PagedResult<OrderSummary>> {
  const { data } = await client.get<PagedResult<OrderSummary>>('/orders', {
    params: { page, pageSize },
  })
  return data
}

export async function getOrder(id: number): Promise<OrderDetail> {
  const { data } = await client.get<OrderDetail>(`/orders/${id}`)
  return data
}

export async function getShippingOptions(): Promise<ShippingOption[]> {
  const { data } = await client.get<ShippingOption[]>('/orders/shipping-options')
  return data
}