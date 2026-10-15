import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import ProductList from '@/components/catalog/ProductList.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import CategoryTree from '@/components/catalog/CategoryTree.vue'
import CartDrawer from '@/components/cart/CartDrawer.vue'
import { useUiStore } from '@/stores/ui'
import { useCartStore } from '@/stores/cart'
import type { Category, ProductSummary } from '@/types'

vi.mock('@/api/cart', () => ({
  getCart: vi.fn().mockResolvedValue({
    id: 1,
    isGuest: true,
    itemCount: 0,
    subtotal: 0,
    items: [],
  }),
  addCartItem: vi.fn(),
  updateCartItem: vi.fn(),
  removeCartItem: vi.fn(),
  clearCart: vi.fn(),
  mergeCart: vi.fn(),
}))

function product(id: number, name: string): ProductSummary {
  return {
    id,
    name,
    slug: name.toLowerCase().replace(/\s+/g, '-'),
    price: 100,
    wasPrice: null,
    discountPercent: null,
    imageUrl: '/a.jpg',
    categoryName: 'Audio',
    isFeatured: false,
    inStock: true,
    stock: 10,
    ratingNum: null,
  }
}

const categories: Category[] = [
  {
    id: 1,
    name: 'Electronics',
    slug: 'electronics',
    description: null,
    isActive: true,
    sortOrder: 1,
    parentId: null,
    productCount: 12,
    children: [
      {
        id: 10,
        name: 'Audio',
        slug: 'audio',
        description: null,
        isActive: true,
        sortOrder: 1,
        parentId: 1,
        productCount: 5,
        children: [],
      },
    ],
  },
]

describe('ProductList', () => {
  it('renders a card per product', () => {
    const wrapper = mount(ProductList, { props: { products: [product(1, 'A'), product(2, 'B')] } })

    expect(wrapper.findAll('.product-card')).toHaveLength(2)
  })

  it('shows a spinner while loading instead of an empty grid', () => {
    const wrapper = mount(ProductList, { props: { products: [], loading: true } })

    expect(wrapper.find('.spinner-border').exists()).toBe(true)
    expect(wrapper.find('.empty-state').exists()).toBe(false)
  })

  it('shows an empty state when there are no results', () => {
    const wrapper = mount(ProductList, { props: { products: [] } })

    expect(wrapper.find('.empty-state').exists()).toBe(true)
  })

  it('surfaces the supplied empty-state message', () => {
    const wrapper = mount(ProductList, {
      props: { products: [], emptyMessage: 'Nothing matches "xyz"' },
    })

    expect(wrapper.text()).toContain('Nothing matches "xyz"')
  })

  it('shows an error state with role=alert', () => {
    const wrapper = mount(ProductList, { props: { products: [], error: 'Network down' } })

    const alert = wrapper.find('[role="alert"]')
    expect(alert.exists()).toBe(true)
    expect(alert.text()).toContain('Network down')
  })

  it('forwards the add event', async () => {
    const wrapper = mount(ProductList, { props: { products: [product(1, 'A')] } })

    await wrapper.find('.product-card button').trigger('click')

    expect(wrapper.emitted('add')).toHaveLength(1)
  })
})

describe('LoadingSpinner', () => {
  it('exposes a polite live region', () => {
    const wrapper = mount(LoadingSpinner, { props: { label: 'Loading products…' } })

    expect(wrapper.find('[role="status"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Loading products…')
  })
})

describe('CategoryTree', () => {
  it('renders the "all products" entry and the hierarchy', () => {
    const wrapper = mount(CategoryTree, { props: { categories, activeId: null } })

    expect(wrapper.text()).toContain('All products')
    expect(wrapper.text()).toContain('Electronics')
    // The nested child is rendered too.
    expect(wrapper.text()).toContain('Audio')
  })

  it('shows the recursive product count', () => {
    const wrapper = mount(CategoryTree, { props: { categories, activeId: null } })

    expect(wrapper.text()).toContain('12')
  })

  it('marks the active category', () => {
    const wrapper = mount(CategoryTree, { props: { categories, activeId: 10 } })

    const pressed = wrapper.findAll('button[aria-pressed="true"]')
    expect(pressed).toHaveLength(1)
    expect(pressed[0].text()).toContain('Audio')
  })

  it('emits the selected category id', async () => {
    const wrapper = mount(CategoryTree, { props: { categories, activeId: null } })

    const electronics = wrapper.findAll('button').find((b) => b.text().includes('Electronics'))!
    await electronics.trigger('click')

    expect(wrapper.emitted('select')?.[0]).toEqual([1])
  })

  it('emits null when "all products" is chosen', async () => {
    const wrapper = mount(CategoryTree, { props: { categories, activeId: 1 } })

    await wrapper.findAll('button')[0].trigger('click')

    expect(wrapper.emitted('select')?.[0]).toEqual([null])
  })
})

describe('CartDrawer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('renders nothing while the drawer is closed', () => {
    const wrapper = mount(CartDrawer)

    expect(wrapper.find('.drawer').exists()).toBe(false)
  })

  it('shows the empty message when open with an empty cart', async () => {
    const ui = useUiStore()
    ui.openMiniCart()

    const wrapper = mount(CartDrawer, { attachTo: document.body })

    // The drawer is teleported into document.body.
    expect(document.body.textContent).toContain('Your cart is empty')

    wrapper.unmount()
  })

  it('offers checkout and view-cart links when there are items', async () => {
    const ui = useUiStore()
    ui.openMiniCart()

    const cart = useCartStore()
    cart.cart = {
      id: 1,
      isGuest: true,
      itemCount: 1,
      subtotal: 4999,
      items: [
        {
          id: 1,
          productId: 1,
          productName: 'Headphones',
          variantName: null,
          imageUrl: '/a.jpg',
          quantity: 1,
          unitPrice: 4999,
          lineTotal: 4999,
          availableStock: 5,
          inStock: true,
        },
      ],
    }

    const wrapper = mount(CartDrawer, { attachTo: document.body })

    const content = document.body.textContent ?? ''
    expect(content).toContain('Headphones')
    expect(content).toContain('View cart')
    expect(content).toContain('Checkout')

    wrapper.unmount()
  })
})