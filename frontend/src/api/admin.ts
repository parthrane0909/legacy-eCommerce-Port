import client from './client'
import type {
  AdminCategory,
  AdminCategoryRequest,
  AdminDashboard,
  AdminOrder,
  AdminProduct,
  AdminProductRequest,
  PagedResult,
} from '@/types'

export async function getAdminProducts(
  page = 1,
  pageSize = 20,
  search?: string,
): Promise<PagedResult<AdminProduct>> {
  const { data } = await client.get<PagedResult<AdminProduct>>('/admin/products', {
    params: { page, pageSize, search: search || undefined },
  })
  return data
}

export async function createProduct(request: AdminProductRequest): Promise<AdminProduct> {
  const { data } = await client.post<AdminProduct>('/admin/products', request)
  return data
}

export async function updateProduct(id: number, request: AdminProductRequest): Promise<AdminProduct> {
  const { data } = await client.put<AdminProduct>(`/admin/products/${id}`, request)
  return data
}

export async function deleteProduct(id: number): Promise<void> {
  await client.delete(`/admin/products/${id}`)
}

export async function getAdminCategories(): Promise<AdminCategory[]> {
  const { data } = await client.get<AdminCategory[]>('/admin/categories')
  return data
}

export async function createCategory(request: AdminCategoryRequest): Promise<AdminCategory> {
  const { data } = await client.post<AdminCategory>('/admin/categories', request)
  return data
}

export async function updateCategory(
  id: number,
  request: AdminCategoryRequest,
): Promise<AdminCategory> {
  const { data } = await client.put<AdminCategory>(`/admin/categories/${id}`, request)
  return data
}

export async function deleteCategory(id: number): Promise<void> {
  await client.delete(`/admin/categories/${id}`)
}

export async function getAdminOrders(
  page = 1,
  pageSize = 20,
  status?: string,
): Promise<PagedResult<AdminOrder>> {
  const { data } = await client.get<PagedResult<AdminOrder>>('/admin/orders', {
    params: { page, pageSize, status: status || undefined },
  })
  return data
}

export async function updateOrderStatus(id: number, status: string): Promise<AdminOrder> {
  const { data } = await client.put<AdminOrder>(`/admin/orders/${id}/status`, { status })
  return data
}

export async function getDashboard(): Promise<AdminDashboard> {
  const { data } = await client.get<AdminDashboard>('/admin/dashboard')
  return data
}