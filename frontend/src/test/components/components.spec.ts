import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import ProductCard from '@/components/catalog/ProductCard.vue'
import Pagination from '@/components/common/Pagination.vue'
import NotificationToast from '@/components/common/NotificationToast.vue'
import CartItemRow from '@/components/cart/CartItemRow.vue'
import { setActivePinia, createPinia } from 'pinia'
import { useUiStore } from '@/stores/ui'
import type { CartItem, ProductSummary } from '@/types'

function product(overrides: Partial<ProductSummary> = {}): ProductSummary {
  return {
    id: 7,
    name: 'Wireless Headphones Pro',
    slug: 'wireless-headphones-pro',
    price: 4999,
    wasPrice: 5879,
    discountPercent: 15,
    imageUrl: '/images/products/headphones.jpg',
    categoryName: 'Audio',
    isFeatured: true,
    inStock: true,
    stock: 4,
    ratingNum: 4.5,
    ...overrides,
  }
}

function cartItem(overrides: Partial<CartItem> = {}): CartItem {
  return {
    id: 1,
    productId: 7,
    productName: 'Wireless Headphones Pro',
    variantName: 'Black',
    imageUrl: '/a.jpg',
    quantity: 2,
    unitPrice: 4999,
    lineTotal: 9998,
    availableStock: 10,
    inStock: true,
    ...overrides,
  }
}

describe('ProductCard', () => {
  it('renders the product name, price and image', () => {
    const wrapper = mount(ProductCard, { props: { product: product() } })

    expect(wrapper.text()).toContain('Wireless Headphones Pro')
    expect(wrapper.text()).toContain('4,999')
    expect(wrapper.find('img').attributes('src')).toBe('/images/products/headphones.jpg')
  })

  it('uses the shared product-image container on the thumbnail', () => {
    // The card and the featured rail share `.product-thumb`, which is what keeps a portrait
    // photo from stretching the tile. Losing the class would silently reintroduce that.
    const wrapper = mount(ProductCard, { props: { product: product() } })

    expect(wrapper.find('.thumb').classes()).toContain('product-thumb')
    expect(wrapper.find('.product-thumb img').exists()).toBe(true)
  })

  it('shows the discount badge and the struck-through was-price', () => {
    const wrapper = mount(ProductCard, { props: { product: product() } })

    expect(wrapper.find('.badge-sale').text()).toBe('-15%')
    expect(wrapper.find('.old-price').exists()).toBe(true)
  })

  it('shows the low-stock label at or below twelve units', () => {
    const wrapper = mount(ProductCard, { props: { product: product({ stock: 4 }) } })

    expect(wrapper.find('.stock.low').text()).toBe('Only 4 left')
  })

  it('marks a sold-out product and disables the button', () => {
    const wrapper = mount(ProductCard, {
      props: { product: product({ inStock: false, stock: 0, discountPercent: null, wasPrice: null }) },
    })

    expect(wrapper.find('.stock.out').text()).toBe('Out of stock')
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })

  it('emits add when the button is pressed', async () => {
    const wrapper = mount(ProductCard, { props: { product: product() } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.emitted('add')).toHaveLength(1)
    expect((wrapper.emitted('add')?.[0] as [{ id: number }])[0].id).toBe(7)
  })

  it('labels the image for assistive technology', () => {
    const wrapper = mount(ProductCard, { props: { product: product() } })

    expect(wrapper.find('img').attributes('alt')).toBe('Wireless Headphones Pro')
  })
})

describe('Pagination', () => {
  it('hides itself when there is only one page', () => {
    const wrapper = mount(Pagination, { props: { page: 1, totalPages: 1 } })

    expect(wrapper.find('nav').exists()).toBe(false)
  })

  it('renders a control for every page when the range is small', () => {
    const wrapper = mount(Pagination, { props: { page: 1, totalPages: 5 } })

    expect(wrapper.findAll('button')).not.toHaveLength(0)
    expect(wrapper.text()).toContain('5')
  })

  it('marks the current page for assistive technology', () => {
    const wrapper = mount(Pagination, { props: { page: 3, totalPages: 5 } })

    const current = wrapper.find('[aria-current="page"]')
    expect(current.exists()).toBe(true)
    expect(current.text()).toBe('3')
  })

  it('disables previous on the first page and next on the last', () => {
    const first = mount(Pagination, { props: { page: 1, totalPages: 3 } })
    expect(first.find('[aria-label="Previous page"]').attributes('disabled')).toBeDefined()

    const last = mount(Pagination, { props: { page: 3, totalPages: 3 } })
    expect(last.find('[aria-label="Next page"]').attributes('disabled')).toBeDefined()
  })

  it('emits the requested page', async () => {
    const wrapper = mount(Pagination, { props: { page: 1, totalPages: 3 } })

    await wrapper.find('[aria-label="Next page"]').trigger('click')

    expect(wrapper.emitted('change')?.[0]).toEqual([2])
  })

  it('does not emit when the current page is clicked again', async () => {
    const wrapper = mount(Pagination, { props: { page: 2, totalPages: 3 } })

    await wrapper.find('[aria-current="page"]').trigger('click')

    expect(wrapper.emitted('change')).toBeUndefined()
  })
})

describe('NotificationToast', () => {
  it('renders messages in a live region', async () => {
    setActivePinia(createPinia())
    const ui = useUiStore()
    ui.notifySuccess('Added to cart')

    const wrapper = mount(NotificationToast)

    expect(wrapper.find('[aria-live="polite"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Added to cart')
  })

  it('applies a tone class', async () => {
    setActivePinia(createPinia())
    const ui = useUiStore()
    ui.notifyError('Something failed')

    const wrapper = mount(NotificationToast)

    expect(wrapper.find('.toast.error').exists()).toBe(true)
  })

  it('dismisses a toast when clicked', async () => {
    setActivePinia(createPinia())
    const ui = useUiStore()
    ui.notify('Gone soon', 'info', 0)

    const wrapper = mount(NotificationToast)
    await wrapper.find('.toast').trigger('click')

    expect(ui.toasts).toHaveLength(0)
  })
})

describe('CartItemRow', () => {
  it('shows the product, quantity and line total', () => {
    const wrapper = mount(CartItemRow, { props: { item: cartItem() } })

    expect(wrapper.text()).toContain('Wireless Headphones Pro')
    expect(wrapper.text()).toContain('Black')
    expect((wrapper.find('input[type="number"]').element as HTMLInputElement).value).toBe('2')
  })

  it('emits an update when the quantity changes', async () => {
    const wrapper = mount(CartItemRow, { props: { item: cartItem() } })
    const input = wrapper.find('input[type="number"]')

    await input.setValue('5')
    await input.trigger('change')

    expect(wrapper.emitted('update')?.[0]).toEqual([7, 5])
  })

  it('emits a remove with the product id', async () => {
    const wrapper = mount(CartItemRow, { props: { item: cartItem() } })

    await wrapper.find('[aria-label^="Remove"]').trigger('click')

    expect(wrapper.emitted('remove')?.[0]).toEqual([7])
  })

  it('caps the quantity input at the available stock', () => {
    const wrapper = mount(CartItemRow, { props: { item: cartItem({ availableStock: 3 }) } })

    expect(wrapper.find('input[type="number"]').attributes('max')).toBe('3')
  })
})