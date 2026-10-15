import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

vi.mock('@/api/orders', () => ({
  placeOrder: vi.fn(),
  getOrders: vi.fn(),
  getOrder: vi.fn(),
  getShippingOptions: vi.fn(),
}))

import * as ordersApi from '@/api/orders'
import { useOrderStore } from '@/stores/orders'
import type { OrderDetail, OrderSummary, PagedResult } from '@/types'

function summary(overrides: Partial<OrderSummary> = {}): OrderSummary {
  return {
    id: 1,
    orderNumber: 'ORD-20261014-000001-A1B2',
    orderDate: '2026-10-14T10:00:00Z',
    status: 'Pending',
    subtotal: 2400,
    shippingCost: 150,
    total: 2550,
    shippingMethod: 'Standard',
    itemCount: 2,
    ...overrides,
  }
}

function detail(overrides: Partial<OrderDetail> = {}): OrderDetail {
  return {
    id: 1,
    orderNumber: 'ORD-20261014-000001-A1B2',
    orderDate: '2026-10-14T10:00:00Z',
    status: 'Pending',
    subtotal: 2400,
    shippingCost: 150,
    total: 2550,
    shippingMethod: 'Standard',
    paymentMethod: 'Card',
    shippingAddress: {
      fullName: 'Rohan Mehta',
      line1: '12 MG Road',
      line2: null,
      city: 'Bengaluru',
      state: 'KA',
      postalCode: '560001',
      country: 'India',
      phone: null,
    },
    billingAddress: {
      fullName: 'Rohan Mehta',
      line1: '12 MG Road',
      line2: null,
      city: 'Bengaluru',
      state: 'KA',
      postalCode: '560001',
      country: 'India',
      phone: null,
    },
    items: [
      {
        id: 1,
        productId: 5,
        productName: 'Desk Lamp',
        variantName: null,
        quantity: 2,
        unitPrice: 1200,
        lineTotal: 2400,
      },
    ],
    ...overrides,
  }
}

function page(items: OrderSummary[], overrides: Partial<PagedResult<OrderSummary>> = {}) {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: 1,
    hasPrevious: false,
    hasNext: false,
    ...overrides,
  } as PagedResult<OrderSummary>
}

const request = {
  shippingMethod: 'Standard',
  shippingAddress: {
    firstName: 'Rohan',
    lastName: 'Mehta',
    line1: '12 MG Road',
    line2: null,
    city: 'Bengaluru',
    state: 'KA',
    postalCode: '560001',
    country: 'India',
    phone: null,
  },
  billingAddress: {
    firstName: 'Rohan',
    lastName: 'Mehta',
    line1: '12 MG Road',
    line2: null,
    city: 'Bengaluru',
    state: 'KA',
    postalCode: '560001',
    country: 'India',
    phone: null,
  },
  billingSameAsShipping: true,
}

describe('order store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads order history with pagination metadata', async () => {
    vi.mocked(ordersApi.getOrders).mockResolvedValue(page([summary()], { totalCount: 3, totalPages: 2 }))

    const store = useOrderStore()
    const result = await store.fetchOrders()

    expect(result?.totalCount).toBe(3)
    expect(store.totalPages).toBe(2)
    expect(store.hasNext).toBe(true)
    expect(store.isEmpty).toBe(false)
  })

  it('reports an empty history', async () => {
    vi.mocked(ordersApi.getOrders).mockResolvedValue(page([]))

    const store = useOrderStore()
    await store.fetchOrders()

    expect(store.isEmpty).toBe(true)
  })

  it('records an error when history cannot be loaded', async () => {
    vi.mocked(ordersApi.getOrders).mockRejectedValue(new Error('boom'))

    const store = useOrderStore()
    await store.fetchOrders()

    expect(store.error).toBe('boom')
    expect(store.orders).toEqual([])
  })

  it('loads a single order into current', async () => {
    vi.mocked(ordersApi.getOrder).mockResolvedValue(detail())

    const store = useOrderStore()
    const result = await store.fetchOrder(1)

    expect(result?.orderNumber).toContain('ORD-')
    expect(store.current?.items).toHaveLength(1)
  })

  /**
   * A 404 covers both "does not exist" and "belongs to someone else" — the API does not
   * distinguish them, so neither does the store.
   */
  it('reports a missing order without leaking the reason', async () => {
    vi.mocked(ordersApi.getOrder).mockRejectedValue(new Error('Request failed with status code 404'))

    const store = useOrderStore()
    const result = await store.fetchOrder(999)

    expect(result).toBeNull()
    expect(store.current).toBeNull()
    expect(store.error).toBe('That order could not be found.')
  })

  it('sends an idempotency key so a double submit cannot duplicate an order', async () => {
    vi.mocked(ordersApi.placeOrder).mockResolvedValue(detail())

    const store = useOrderStore()
    await store.place(request)

    expect(ordersApi.placeOrder).toHaveBeenCalledWith(request, expect.any(String))
    expect(store.current?.id).toBe(1)
  })

  it('surfaces a placement failure', async () => {
    vi.mocked(ordersApi.placeOrder).mockRejectedValue(new Error('Cart is empty.'))

    const store = useOrderStore()
    const result = await store.place(request)

    expect(result).toBeNull()
    expect(store.error).toBe('Cart is empty.')
  })

  /**
   * A second click while the first request is in flight must not create a second order;
   * `placing` is the guard, with the idempotency key as the server-side backstop.
   */
  it('ignores a concurrent placement while one is in flight', async () => {
    let resolve: (value: OrderDetail) => void = () => {}
    vi.mocked(ordersApi.placeOrder).mockReturnValue(new Promise<OrderDetail>((r) => (resolve = r)))

    const store = useOrderStore()
    const first = store.place(request)
    const second = await store.place(request)

    expect(second).toBeNull()
    expect(ordersApi.placeOrder).toHaveBeenCalledTimes(1)

    resolve(detail())
    expect(await first).not.toBeNull()
  })

  it('refuses to page outside the valid range', async () => {
    vi.mocked(ordersApi.getOrders).mockResolvedValue(page([summary()], { totalPages: 3 }))

    const store = useOrderStore()
    await store.goToPage(0)
    await store.goToPage(99)

    expect(ordersApi.getOrders).toHaveBeenCalledTimes(1)
  })

  it('clears the current order', async () => {
    vi.mocked(ordersApi.getOrder).mockResolvedValue(detail())

    const store = useOrderStore()
    await store.fetchOrder(1)
    store.clearCurrent()

    expect(store.current).toBeNull()
  })
})