import client from './client'
import type { Cart } from '@/types'

export interface AddCartItemRequest {
  productId: number
  quantity: number
  productVariantId?: number | null
}

/**
 * The cart for the current caller. Works for guests (the API issues an anonymous cart
 * cookie) and for signed-in customers.
 */
export async function getCart(): Promise<Cart> {
  const { data } = await client.get<Cart>('/cart')
  return data
}

export async function addCartItem(request: AddCartItemRequest): Promise<Cart> {
  const { data } = await client.post<Cart>('/cart/items', request)
  return data
}

export async function updateCartItem(productId: number, quantity: number): Promise<Cart> {
  const { data } = await client.put<Cart>(`/cart/items/${productId}`, { quantity })
  return data
}

export async function removeCartItem(productId: number): Promise<Cart> {
  const { data } = await client.delete<Cart>(`/cart/items/${productId}`)
  return data
}

export async function clearCart(): Promise<Cart> {
  const { data } = await client.delete<Cart>('/cart')
  return data
}

/**
 * Folds the guest cart into the signed-in customer's cart. Called once after login.
 * The API returns any dropped or clamped lines as `warnings`.
 */
export async function mergeCart(anonymousId?: string | null): Promise<Cart> {
  const { data } = await client.post<Cart>('/cart/merge', { anonymousId: anonymousId ?? null })
  return data
}