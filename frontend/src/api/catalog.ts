import client from './client'
import type { Category, HomeProducts, PagedResult, ProductDetail, ProductSummary } from '@/types'

export interface ProductQuery {
  page?: number
  pageSize?: number
  categoryId?: number | null
  search?: string
  sort?: string
  minPrice?: number | null
  maxPrice?: number | null
  inStockOnly?: boolean
}

/** Hierarchical category tree with product counts. */
export async function getCategories(): Promise<Category[]> {
  const { data } = await client.get<Category[]>('/categories')
  return data
}

/**
 * Paged product list. Filtering, searching, sorting and paging all happen in the API, so the
 * browser never downloads the whole catalogue.
 */
export async function getProducts(query: ProductQuery = {}): Promise<PagedResult<ProductSummary>> {
  const { data } = await client.get<PagedResult<ProductSummary>>('/products', {
    params: {
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 8,
      categoryId: query.categoryId ?? undefined,
      search: query.search || undefined,
      sort: query.sort ?? undefined,
      minPrice: query.minPrice ?? undefined,
      maxPrice: query.maxPrice ?? undefined,
      inStockOnly: query.inStockOnly ?? undefined,
    },
  })
  return data
}

export async function getProduct(id: number): Promise<ProductDetail> {
  const { data } = await client.get<ProductDetail>(`/products/${id}`)
  return data
}

/** Featured, new-arrival and deal rails used by the home page. */
export async function getHomeProducts(): Promise<HomeProducts> {
  const { data } = await client.get<HomeProducts>('/products/home')
  return data
}