import client, { setAccessToken } from './client'
import type { AuthResponse, User } from '@/types'

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
}

export interface LoginRequest {
  email: string
  password: string
}

/** Registers and signs the new customer in. */
export async function register(request: RegisterRequest): Promise<User> {
  const { data } = await client.post<AuthResponse>('/auth/register', request)
  setAccessToken(data.accessToken)
  return data.user
}

/** Signs in. The refresh token is set as an HttpOnly cookie by the API. */
export async function login(request: LoginRequest): Promise<User> {
  const { data } = await client.post<AuthResponse>('/auth/login', request)
  setAccessToken(data.accessToken)
  return data.user
}

/** Revokes the refresh token server-side and clears it client-side. */
export async function logout(): Promise<void> {
  try {
    await client.post('/auth/logout')
  } finally {
    setAccessToken(null)
  }
}

/** Returns the current user, or null when the caller is not authenticated. */
export async function me(): Promise<User | null> {
  try {
    const { data } = await client.get<User>('/auth/me')
    return data
  } catch {
    return null
  }
}