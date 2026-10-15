import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as cartApi from '@/api/cart'
import type { Cart, CartItem } from '@/types'

/** An empty cart, used as the initial state and as the shape after a clear. */
function emptyCart(): Cart {
  return { id: 0, isGuest: true, itemCount: 0, subtotal: 0, items: [] }
}

/**
 * Cart state. The source of truth is the API, not this store: every mutation returns the
 * recalculated cart (prices and stock re-derived server-side) and that response is what the
 * store holds. Nothing here trusts a client-computed total.
 */
export const useCartStore = defineStore('cart', () => {
  const cart = ref<Cart>(emptyCart())
  const loading = ref(false)
  const error = ref<string | null>(null)
  const warnings = ref<string[]>([])

  const items = computed<CartItem[]>(() => cart.value.items)
  const itemCount = computed(() => cart.value.itemCount)
  const subtotal = computed(() => cart.value.subtotal)
  const isEmpty = computed(() => cart.value.itemCount === 0)
  const isGuest = computed(() => cart.value.isGuest)

  function apply(next: Cart): Cart {
    cart.value = next
    warnings.value = next.warnings ?? []
    error.value = null
    return next
  }

  function setWarnings(next: string[]): void {
    warnings.value = next
  }

  function dismissWarnings(): void {
    warnings.value = []
  }

  /** Wraps an async cart call with consistent loading and error state. */
  async function run<T>(operation: () => Promise<T>): Promise<T | null> {
    loading.value = true
    error.value = null
    try {
      return await operation()
    } catch (cause) {
      error.value = message(cause)
      return null
    } finally {
      loading.value = false
    }
  }

  function message(cause: unknown): string {
    if (cause instanceof Error && cause.message) return cause.message
    return 'The cart could not be updated. Please try again.'
  }

  async function fetch(): Promise<void> {
    await run(async () => apply(await cartApi.getCart()))
  }

  async function add(productId: number, quantity = 1, variantId?: number | null): Promise<void> {
    await run(async () =>
      apply(await cartApi.addCartItem({ productId, quantity, productVariantId: variantId ?? null })),
    )
  }

  async function update(productId: number, quantity: number): Promise<void> {
    await run(async () => apply(await cartApi.updateCartItem(productId, quantity)))
  }

  async function remove(productId: number): Promise<void> {
    await run(async () => apply(await cartApi.removeCartItem(productId)))
  }

  async function clear(): Promise<void> {
    await run(async () => apply(await cartApi.clearCart()))
  }

  async function merge(anonymousId: string): Promise<Cart> {
    let result: Cart | null = null
    await run(async () => {
      result = await cartApi.mergeCart(anonymousId)
      apply(result)
    })
    return result ?? cart.value
  }

  /** Local reset on logout, so no previous customer's cart lingers in the UI. */
  function reset(): void {
    cart.value = emptyCart()
    warnings.value = []
    error.value = null
  }

  return {
    cart,
    items,
    itemCount,
    subtotal,
    isEmpty,
    isGuest,
    loading,
    error,
    warnings,
    fetch,
    add,
    update,
    remove,
    clear,
    merge,
    reset,
    setWarnings,
    dismissWarnings,
  }
})