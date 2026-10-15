/**
 * Shared API/domain types. These mirror the DTOs exposed by the .NET 10 Minimal API
 * (Ecommerce.Core/Dtos) so the two sides stay in step.
 */

export interface Category {
  id: number
  name: string
  slug: string
  description: string | null
  isActive: boolean
  sortOrder: number
  parentId: number | null
  productCount: number
  children: Category[]
}

export interface ProductSummary {
  id: number
  name: string
  slug: string
  price: number
  wasPrice: number | null
  discountPercent: number | null
  imageUrl: string
  categoryName: string
  isFeatured: boolean
  inStock: boolean
  stock: number
  ratingNum: number | null
}

export interface ProductVariant {
  id: number
  name: string
  sku: string | null
  priceDelta: number
  stock: number
}

export interface ProductImage {
  id: number
  url: string
  altText: string | null
  isPrimary: boolean
  sortOrder: number
}

export interface ProductDetail {
  id: number
  name: string
  slug: string
  description: string | null
  price: number
  wasPrice: number | null
  discountPercent: number | null
  imageUrl: string
  stock: number
  inStock: boolean
  isFeatured: boolean
  ratingNum: number | null
  categoryId: number
  categoryName: string
  images: ProductImage[]
  variants: ProductVariant[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface CartItem {
  id: number
  productId: number
  productName: string
  variantName: string | null
  imageUrl: string
  quantity: number
  unitPrice: number
  lineTotal: number
  availableStock: number
  inStock: boolean
}

export interface Cart {
  id: number
  isGuest: boolean
  itemCount: number
  subtotal: number
  items: CartItem[]
  warnings?: string[]
}

export interface AddressInput {
  firstName: string
  lastName: string
  line1: string
  line2?: string | null
  city: string
  state: string
  postalCode: string
  country: string
  phone?: string | null
}

export interface Address extends AddressInput {
  id: number
  fullName: string
  isDefault: boolean
}

export interface PlaceOrderRequest {
  shippingMethod: string
  shippingAddress: AddressInput
  billingAddress: AddressInput
  billingSameAsShipping: boolean
}

export interface ShippingOption {
  code: string
  label: string
  cost: number
  estimate: string
}

export interface OrderItem {
  id: number
  productId: number
  productName: string
  variantName: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface AddressSnapshot {
  fullName: string
  line1: string
  line2: string | null
  city: string
  state: string
  postalCode: string
  country: string
  phone: string | null
}

export interface OrderSummary {
  id: number
  orderNumber: string
  orderDate: string
  status: string
  subtotal: number
  shippingCost: number
  total: number
  shippingMethod: string
  itemCount: number
}

export interface OrderDetail {
  id: number
  orderNumber: string
  orderDate: string
  status: string
  subtotal: number
  shippingCost: number
  total: number
  shippingMethod: string
  paymentMethod: string
  shippingAddress: AddressSnapshot
  billingAddress: AddressSnapshot
  items: OrderItem[]
}

export interface User {
  id: number
  email: string
  firstName: string
  lastName: string
  role: string
  fullName: string
}

export interface AuthResponse {
  accessToken: string
  tokenType: string
  expiresInSeconds: number
  user: User
}

export interface Profile {
  id: number
  email: string
  firstName: string
  lastName: string
  fullName: string
  role: string
  createdAt: string
}

export interface HomeProducts {
  featured: ProductSummary[]
  newArrivals: ProductSummary[]
  deals: ProductSummary[]
  categories: Category[]
}

export interface AdminProduct {
  id: number
  name: string
  slug: string
  description: string | null
  price: number
  stock: number
  imageUrl: string
  discountPercent: number | null
  ratingNum: number | null
  isFeatured: boolean
  isActive: boolean
  categoryId: number
  categoryName: string
  variantCount: number | null
  imageCount: number | null
  createdAt: string
}

export interface AdminProductRequest {
  name: string
  slug?: string | null
  description?: string | null
  price: number
  stock: number
  imageUrl?: string | null
  discountPercent?: number | null
  ratingNum?: number | null
  isFeatured: boolean
  isActive: boolean
  categoryId: number
}

export interface AdminCategory {
  id: number
  name: string
  slug: string
  description: string | null
  isActive: boolean
  sortOrder: number
  parentId: number | null
  parentName: string | null
  productCount: number
}

export interface AdminCategoryRequest {
  name: string
  slug?: string | null
  description?: string | null
  isActive: boolean
  sortOrder: number
  parentId?: number | null
}

export interface AdminOrder {
  id: number
  orderNumber: string
  orderDate: string
  status: string
  total: number
  itemCount: number
  customerName: string
  customerEmail: string
}

export interface AdminDashboard {
  productCount: number
  categoryCount: number
  customerCount: number
  orderCount: number
  lowStockCount: number
  totalRevenue: number
  recentOrders: AdminOrder[]
}

/** RFC 7807 error payload returned by the API. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
  traceId?: string
}

export const ORDER_STATUSES = ['Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled'] as const
export type OrderStatus = (typeof ORDER_STATUSES)[number]