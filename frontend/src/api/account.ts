import client from './client'
import type { Address, Profile } from '@/types'

export interface UpdateProfileRequest {
  firstName: string
  lastName: string
  email: string
}

export interface AddressRequest {
  firstName: string
  lastName: string
  line1: string
  line2?: string | null
  city: string
  state: string
  postalCode: string
  country: string
  phone?: string | null
  isDefault?: boolean
}

export async function getProfile(): Promise<Profile> {
  const { data } = await client.get<Profile>('/account/profile')
  return data
}

export async function updateProfile(request: UpdateProfileRequest): Promise<Profile> {
  const { data } = await client.put<Profile>('/account/profile', request)
  return data
}

export async function getAddresses(): Promise<Address[]> {
  const { data } = await client.get<Address[]>('/account/addresses')
  return data
}

export async function createAddress(request: AddressRequest): Promise<Address> {
  const { data } = await client.post<Address>('/account/addresses', request)
  return data
}

export async function updateAddress(id: number, request: AddressRequest): Promise<Address> {
  const { data } = await client.put<Address>(`/account/addresses/${id}`, request)
  return data
}

export async function deleteAddress(id: number): Promise<void> {
  await client.delete(`/account/addresses/${id}`)
}