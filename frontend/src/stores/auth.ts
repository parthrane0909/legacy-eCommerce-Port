import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as authApi from '@/api/auth'
import { setAccessToken, setAuthFailureHandler, refreshAccessToken } from '@/api/client'
import { clearGuestCartId, readGuestCartId } from '@/utils/guestCart'
import { useCartStore } from '@/stores/cart'
import type { User } from '@/types'

/**
 * Authentication state.
 *
 * The access token is intentionally not part of this store's state: it is held in the Axios
 * module so it can never leak into a template, devtools state dump, or persisted store.
 */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isAuthenticated = computed(() => user.value !== null)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const initialising = ref(false)

  /**
   * Merge the guest cart into the newly authenticated customer's cart.
   *
   * The merge request is sent unconditionally. The API resolves the guest cart from its own
   * HttpOnly `shophub_cart` cookie and treats any id in the body as a *fallback* for clients
   * that hold one, so calling this with an empty body is sufficient — and necessary. Gating the
   * call on a locally stored id silently dropped the guest's cart, because nothing in the app
   * ever writes that id; the merge endpoint existed, worked, and was simply never reached.
   */
  async function mergeGuestCart(): Promise<void> {
    const cart = useCartStore()

    try {
      const merged = await cart.merge(readGuestCartId() ?? '')
      if (merged.warnings?.length) {
        cart.setWarnings(merged.warnings)
      }
      clearGuestCartId()
    } catch {
      // A failed merge must never block login; the shopper keeps a working session.
    }
  }

  async function login(email: string, password: string): Promise<User> {
    const signedIn = await authApi.login({ email, password })
    user.value = signedIn
    await mergeGuestCart()
    return signedIn
  }

  async function register(input: {
    email: string
    password: string
    firstName: string
    lastName: string
  }): Promise<User> {
    const created = await authApi.register(input)
    user.value = created
    await mergeGuestCart()
    return created
  }

  async function logout(): Promise<void> {
    await authApi.logout()
    user.value = null
    clearGuestCartId()

    // Drop any privileged UI state left over from the previous session.
    const cart = useCartStore()
    cart.reset()
  }

  /**
   * Restores the session on a hard reload. The access token is gone (it lived in memory),
   * so a refresh cookie exchange is the only way to recover it.
   */
  async function restore(): Promise<void> {
    if (initialising.value || user.value) return

    initialising.value = true
    try {
      await refreshAccessToken()
      user.value = await authApi.me()
    } catch {
      user.value = null
    } finally {
      initialising.value = false
    }
  }

  /** Applies a freshly-issued token after any client-side refresh. */
  function adoptToken(token: string | null): void {
    setAccessToken(token)
  }

  return {
    user,
    initialising,
    isAuthenticated,
    isAdmin,
    login,
    register,
    logout,
    restore,
    adoptToken,
  }
})

/** Wires the client's refresh-failure signal to the store exactly once. */
export function registerAuthFailureHandler(): void {
  const store = useAuthStore()
  setAuthFailureHandler(() => {
    store.user = null
  })
}