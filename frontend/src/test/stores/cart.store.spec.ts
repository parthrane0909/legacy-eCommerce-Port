import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

vi.mock('@/api/cart', () => ({
  getCart: vi.fn(),
  addCartItem: vi.fn(),
  updateCartItem: vi.fn(),
  removeCartItem: vi.fn(),
  clearCart: vi.fn(),
  mergeCart: vi.fn(),
}))

import * as cartApi from '@/api/cart'
import { useCartStore } from '@/stores/cart'
import type { Cart } from '@/types'

function cart(overrides: Partial<Cart> = {}): Cart {
  return {
    id: 1,
    isGuest: true,
    itemCount: 2,
    subtotal: 2000,
    items: [
      {
        id: 1,
        productId: 10,
        productName: 'Headphones',
        variantName: null,
        imageUrl: '/a.jpg',
        quantity: 2,
        unitPrice: 1000,
        lineTotal: 2000,
        availableStock: 5,
        inStock: true,
      },
    ],
    ...overrides,
  }
}

describe('cart store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('exposes derived totals from the API response', () => {
    const store = useCartStore()
    vi.mocked(cartApi.getCart).mockResolvedValue(cart())

    return store.fetch().then(() => {
      expect(store.itemCount).toBe(2)
      expect(store.subtotal).toBe(2000)
      expect(store.isEmpty).toBe(false)
      expect(store.isGuest).toBe(true)
    })
  })

  it('treats a zero-item cart as empty', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.getCart).mockResolvedValue(cart({ itemCount: 0, subtotal: 0, items: [] }))

    await store.fetch()

    expect(store.isEmpty).toBe(true)
  })

  it('applies the recalculated cart returned by add', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.addCartItem).mockResolvedValue(cart({ itemCount: 3, subtotal: 3000 }))

    await store.add(10, 3)

    expect(cartApi.addCartItem).toHaveBeenCalledWith({
      productId: 10,
      quantity: 3,
      productVariantId: null,
    })
    expect(store.itemCount).toBe(3)
    expect(store.error).toBeNull()
  })

  it('passes a variant id through when supplied', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.addCartItem).mockResolvedValue(cart())

    await store.add(10, 1, 77)

    expect(cartApi.addCartItem).toHaveBeenCalledWith({
      productId: 10,
      quantity: 1,
      productVariantId: 77,
    })
  })

  it('records an error and leaves state untouched when a request fails', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.addCartItem).mockRejectedValue(new Error('Product 10 was not found'))

    await store.add(10, 1)

    expect(store.error).toBe('Product 10 was not found')
    expect(store.isEmpty).toBe(true)
  })

  it('surfaces merge warnings returned by the API', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.mergeCart).mockResolvedValue(
      cart({ isGuest: false, warnings: ['Only 3 left in stock'] }),
    )

    const result = await store.merge('guest-123')

    expect(cartApi.mergeCart).toHaveBeenCalledWith('guest-123')
    expect(result.warnings).toEqual(['Only 3 left in stock'])
    expect(store.warnings).toEqual(['Only 3 left in stock'])
    expect(store.isGuest).toBe(false)
  })

  it('clears warnings on demand', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.getCart).mockResolvedValue(cart({ warnings: ['something'] }))

    await store.fetch()
    expect(store.warnings).toHaveLength(1)

    store.dismissWarnings()
    expect(store.warnings).toHaveLength(0)
  })

  it('resets to a blank cart on logout', async () => {
    const store = useCartStore()
    vi.mocked(cartApi.getCart).mockResolvedValue(cart())

    await store.fetch()
    store.reset()

    expect(store.isEmpty).toBe(true)
    expect(store.itemCount).toBe(0)
    expect(store.subtotal).toBe(0)
  })

  it('tracks the loading flag around an operation', async () => {
    const store = useCartStore()
    let resolve: (value: Cart) => void = () => {}
    vi.mocked(cartApi.addCartItem).mockReturnValue(new Promise<Cart>((r) => (resolve = r)))

    const pending = store.add(10, 1)
    expect(store.loading).toBe(true)

    resolve(cart())
    await pending
    expect(store.loading).toBe(false)
  })
})