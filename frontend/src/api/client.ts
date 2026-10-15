import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import type { AuthResponse, ProblemDetails } from '@/types'

/**
 * Shared Axios instance.
 *
 * Token lifecycle:
 * - The access token is held in memory only (`setAccessToken`). It is never written to
 *   localStorage or sessionStorage, so an XSS payload cannot read it at rest.
 * - The refresh token lives in an HttpOnly cookie set by the API, which JavaScript cannot read.
 * - On a 401 the instance attempts one silent refresh, then retries the original request.
 *   Concurrent 401s share a single in-flight refresh (see `refreshPromise`), which prevents a
 *   refresh storm and the token-revocation race it would cause.
 */
const client: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  withCredentials: true, // required for the HttpOnly refresh cookie
  headers: { 'Content-Type': 'application/json' },
})

let accessToken: string | null = null
let refreshPromise: Promise<string | null> | null = null

/** Notified when refreshing fails, so the app can drop to the guest state. */
let onAuthFailure: (() => void) | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function getAccessToken(): string | null {
  return accessToken
}

export function setAuthFailureHandler(handler: (() => void) | null): void {
  onAuthFailure = handler
}

client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

/** Endpoints that must never trigger a refresh loop. */
const NO_RETRY = ['/auth/login', '/auth/register', '/auth/refresh']

client.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined
    const status = error.response?.status

    if (status !== 401 || !original || original._retried) {
      return Promise.reject(error)
    }

    const url = original.url ?? ''
    if (NO_RETRY.some((path) => url.includes(path))) {
      return Promise.reject(error)
    }

    original._retried = true

    const token = await refreshAccessToken()
    if (!token) {
      return Promise.reject(error)
    }

    original.headers.Authorization = `Bearer ${token}`
    return client(original)
  },
)

/**
 * Refreshes the access token. Concurrent callers await the same request.
 */
export async function refreshAccessToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const { data } = await client.post<AuthResponse>('/auth/refresh', {})
        accessToken = data.accessToken
        return data.accessToken
      } catch {
        accessToken = null
        onAuthFailure?.()
        return null
      } finally {
        // Cleared on the next tick so simultaneous callers all observe the same attempt.
        refreshPromise = null
      }
    })()
  }

  return refreshPromise
}

/** Turns a rejected call into a readable message, preferring the API's ProblemDetails detail. */
export function toErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (axios.isAxiosError<ProblemDetails>(error)) {
    const status = error.response?.status
    const payload = error.response?.data

    if (payload?.errors) {
      const messages = Object.values(payload.errors).flat()
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }

    if (payload?.detail) return payload.detail
    if (payload?.title) return payload.title

    // Rate-limit rejections carry no body, so check the status before falling back — otherwise
    // a throttled sign-in is reported to the shopper as bad credentials.
    if (status === 429) {
      return 'Too many attempts. Please wait a moment and try again.'
    }

    if (status && status >= 500) {
      return 'The server had a problem. Please try again shortly.'
    }

    if (error.code === 'ERR_NETWORK') {
      return 'Cannot reach the server. Check that the API is running.'
    }
  }

  return fallback
}

export default client