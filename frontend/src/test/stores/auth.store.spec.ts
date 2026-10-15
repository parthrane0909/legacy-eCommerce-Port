import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

vi.mock('@/api/auth', () => ({
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  me: vi.fn(),
}))

vi.mock('@/api/client', () => ({
  setAccessToken: vi.fn(),
  setAuthFailureHandler: vi.fn(),
  refreshAccessToken: vi.fn(),
  toErrorMessage: (e: unknown) => (e instanceof Error ? e.message : 'failed'),
}))

vi.mock('@/api/cart', () => ({
  getCart: vi.fn(),
  addCartItem: vi.fn(),
  updateCartItem: vi.fn(),
  removeCartItem: vi.fn(),
  clearCart: vi.fn(),
  mergeCart: vi.fn(),
}))

import * as authApi from '@/api/auth'
import * as cartApi from '@/api/cart'
import { useAuthStore } from '@/stores/auth'
import { useCartStore } from '@/stores/cart'
import type { Cart, User } from '@/types'

const customer: User = {
  id: 2,
  email: 'customer@shophub.local',
  firstName: 'Rohan',
  lastName: 'Mehta',
  role: 'Customer',
  fullName: 'Rohan Mehta',
}

const admin: User = { ...customer, id: 1, email: 'admin@shophub.local', role: 'Admin', fullName: 'Aditi Rao' }

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    window.localStorage.clear()
  })

  it('starts unauthenticated', () => {
    const store = useAuthStore()

    expect(store.user).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(store.isAdmin).toBe(false)
  })

  it('marks an ordinary customer as authenticated but not admin', async () => {
    vi.mocked(authApi.login).mockResolvedValue(customer)
    const store = useAuthStore()

    await store.login(customer.email, 'Customer@12345')

    expect(store.isAuthenticated).toBe(true)
    expect(store.isAdmin).toBe(false)
  })

  it('recognises an administrator', async () => {
    vi.mocked(authApi.login).mockResolvedValue(admin)
    const store = useAuthStore()

    await store.login(admin.email, 'Admin@12345')

    expect(store.isAdmin).toBe(true)
  })

  it('merges the guest cart after login', async () => {
    window.localStorage.setItem('shophub.guestCartId', 'guest-abc')
    vi.mocked(authApi.login).mockResolvedValue(customer)

    const cart = useCartStore()
    vi.mocked(cartApi.mergeCart).mockResolvedValue({
      id: 5,
      isGuest: false,
      itemCount: 1,
      subtotal: 100,
      items: [],
    } as Cart)

    const store = useAuthStore()
    await store.login(customer.email, 'Customer@12345')

    expect(cartApi.mergeCart).toHaveBeenCalledWith('guest-abc')
    expect(window.localStorage.getItem('shophub.guestCartId')).toBeNull()
    expect(cart.isGuest).toBe(false)
  })

  it('still signs in when the cart merge fails', async () => {
    window.localStorage.setItem('shophub.guestCartId', 'guest-abc')
    vi.mocked(authApi.login).mockResolvedValue(customer)
    vi.mocked(cartApi.mergeCart).mockRejectedValue(new Error('merge failed'))

    const store = useAuthStore()
    await store.login(customer.email, 'Customer@12345')

    expect(store.isAuthenticated).toBe(true)
  })

  it('merges after login even with no locally stored guest cart id', async () => {
    // Regression: nothing in the app writes `shophub.guestCartId` — the API's own HttpOnly
    // cookie is the real source of the guest cart id. Gating the merge on that localStorage
    // key meant it never ran, and every guest silently lost their cart on sign-in.
    expect(window.localStorage.getItem('shophub.guestCartId')).toBeNull()

    vi.mocked(authApi.login).mockResolvedValue(customer)
    vi.mocked(cartApi.mergeCart).mockResolvedValue({
      id: 5,
      isGuest: false,
      itemCount: 2,
      subtotal: 1000,
      items: [],
    })

    const store = useAuthStore()
    await store.login(customer.email, 'Customer@12345')

    expect(cartApi.mergeCart).toHaveBeenCalledTimes(1)
    expect(cartApi.mergeCart).toHaveBeenCalledWith('')

    // And the merged cart is what the UI now shows.
    expect(useCartStore().itemCount).toBe(2)
  })

  it('merges after registration too', async () => {
    vi.mocked(authApi.register).mockResolvedValue(customer)
    vi.mocked(cartApi.mergeCart).mockResolvedValue({
      id: 5,
      isGuest: false,
      itemCount: 1,
      subtotal: 500,
      items: [],
    })

    const store = useAuthStore()
    await store.register({
      email: customer.email,
      password: 'Customer@12345',
      firstName: 'Rohan',
      lastName: 'Mehta',
    })

    expect(cartApi.mergeCart).toHaveBeenCalledTimes(1)
  })

  it('clears identity and cart state on logout', async () => {
    vi.mocked(authApi.login).mockResolvedValue(customer)
    vi.mocked(authApi.logout).mockResolvedValue(undefined)

    const store = useAuthStore()
    await store.login(customer.email, 'Customer@12345')

    const cart = useCartStore()
    await store.logout()

    expect(store.user).toBeNull()
    expect(authApi.logout).toHaveBeenCalled()
    expect(cart.isEmpty).toBe(true)
  })

  it('restores a session on reload via the refresh cookie', async () => {
    const { refreshAccessToken } = await import('@/api/client')
    vi.mocked(refreshAccessToken).mockResolvedValue('new-token')
    vi.mocked(authApi.me).mockResolvedValue(customer)

    const store = useAuthStore()
    await store.restore()

    expect(refreshAccessToken).toHaveBeenCalled()
    expect(authApi.me).toHaveBeenCalled()
    expect(store.user).toEqual(customer)
  })

  it('stays signed out when restore fails', async () => {
    const { refreshAccessToken } = await import('@/api/client')
    vi.mocked(refreshAccessToken).mockRejectedValue(new Error('no cookie'))
    vi.mocked(authApi.me).mockResolvedValue(null)

    const store = useAuthStore()
    await store.restore()

    expect(store.user).toBeNull()
    expect(store.initialising).toBe(false)
  })
})