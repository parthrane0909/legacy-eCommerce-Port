/** Cart-key helpers for guest carts held in the browser. */

/** Key under which the guest's cart id is remembered so it can be merged after login. */
const GUEST_CART_KEY = 'shophub.guestCartId'

export function readGuestCartId(): string | null {
  try {
    return window.localStorage.getItem(GUEST_CART_KEY)
  } catch {
    // Private browsing or a blocked storage partition: fall back to no stored id.
    return null
  }
}

export function writeGuestCartId(id: string): void {
  try {
    window.localStorage.setItem(GUEST_CART_KEY, id)
  } catch {
    // Non-fatal: the API's own HttpOnly cookie remains the source of truth for the guest cart.
  }
}

export function clearGuestCartId(): void {
  try {
    window.localStorage.removeItem(GUEST_CART_KEY)
  } catch {
    // Nothing to clean up.
  }
}

/**
 * Creates a random id for a guest cart. The API also issues an anonymous cart cookie;
 * this mirrors it in localStorage so the id survives a page reload and can be sent
 * explicitly when merging after login.
 */
export function newGuestCartId(): string {
  const id =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().replace(/-/g, '')
      : Math.random().toString(36).slice(2) + Date.now().toString(36)

  writeGuestCartId(id)
  return id
}