import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import AdminProductForm from '@/components/admin/AdminProductForm.vue'
import AdminOrderTable from '@/components/admin/AdminOrderTable.vue'
import AddressForm from '@/components/account/AddressForm.vue'
import type { AdminCategory, AdminOrder, AdminProduct, AddressInput } from '@/types'

const categories: AdminCategory[] = [
  {
    id: 1,
    name: 'Electronics',
    slug: 'electronics',
    description: null,
    isActive: true,
    sortOrder: 1,
    parentId: null,
    parentName: null,
    productCount: 3,
  },
  {
    id: 2,
    name: 'Audio',
    slug: 'audio',
    description: null,
    isActive: true,
    sortOrder: 1,
    parentId: 1,
    parentName: 'Electronics',
    productCount: 1,
  },
]

function product(overrides: Partial<AdminProduct> = {}): AdminProduct {
  return {
    id: 7,
    name: 'Desk Lamp',
    slug: 'desk-lamp',
    description: 'A lamp',
    price: 1499,
    stock: 12,
    imageUrl: '/images/products/lamp.jpg',
    discountPercent: null,
    ratingNum: null,
    isFeatured: false,
    isActive: true,
    categoryId: 1,
    categoryName: 'Electronics',
    variantCount: 0,
    imageCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function order(overrides: Partial<AdminOrder> = {}): AdminOrder {
  return {
    id: 3,
    orderNumber: 'ORD-20261014-000003-B2C3',
    orderDate: '2026-10-14T10:00:00Z',
    status: 'Pending',
    total: 2550,
    itemCount: 2,
    customerName: 'Rohan Mehta',
    customerEmail: 'customer@shophub.local',
    ...overrides,
  }
}

describe('AdminProductForm', () => {
  it('starts blank for a new product', () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    expect(wrapper.text()).toContain('New product')
    expect((wrapper.find('#product-name').element as HTMLInputElement).value).toBe('')
    expect((wrapper.find('#product-category').element as HTMLSelectElement).value).toBe('1')
  })

  it('enables submit once the categories arrive late', async () => {
    // Regression: the page fetches categories asynchronously, so the form can be constructed
    // before any category exists. Without adopting the first category when it lands, categoryId
    // stayed null, "Choose a category" was a permanent error, and Create stayed disabled.
    const wrapper = mount(AdminProductForm, { props: { product: null, categories: [] } })

    expect(wrapper.find('#product-category').exists()).toBe(true)
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ categories })

    expect((wrapper.find('#product-category').element as HTMLSelectElement).value).toBe('1')
  })

  it('enables submit for a completed new-product form', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories: [] } })

    await wrapper.setProps({ categories })
    await wrapper.find('#product-name').setValue('E2E Product')
    await wrapper.find('#product-price').setValue('1499')
    await wrapper.find('#product-stock').setValue('7')

    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('keeps the loaded category when categories change during an edit', async () => {
    const wrapper = mount(AdminProductForm, {
      props: { product: product({ categoryId: 2 }), categories },
    })

    expect((wrapper.find('#product-category').element as HTMLSelectElement).value).toBe('2')

    // A later refresh must not overwrite the product's own category.
    await wrapper.setProps({ categories: [...categories] })
    expect((wrapper.find('#product-category').element as HTMLSelectElement).value).toBe('2')
  })

  it('loads the product being edited', () => {
    const wrapper = mount(AdminProductForm, {
      props: { product: product({ name: 'Existing', price: 99, stock: 3, isFeatured: true }), categories },
    })

    expect(wrapper.text()).toContain('Edit “Existing”')
    expect((wrapper.find('#product-name').element as HTMLInputElement).value).toBe('Existing')
    expect((wrapper.find('#product-price').element as HTMLInputElement).value).toBe('99')
    expect((wrapper.find('#product-stock').element as HTMLInputElement).value).toBe('3')
  })

  it('requires a name', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.find('#product-name').setValue('')

    expect(wrapper.text()).toContain('Name is required')
    expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it('rejects a non-positive price', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.find('#product-name').setValue('Thing')
    await wrapper.find('#product-price').setValue('0')

    expect(wrapper.text()).toContain('Price must be greater than 0')
  })

  it('rejects negative stock', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.find('#product-name').setValue('Thing')
    await wrapper.find('#product-stock').setValue('-1')

    expect(wrapper.text()).toContain('Stock cannot be negative')
  })

  it('rejects an out-of-range discount', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.find('#product-name').setValue('Thing')
    await wrapper.find('#product-discount').setValue('95')

    expect(wrapper.text()).toContain('Discount must be between 0 and 90')
  })

  it('emits a valid payload and asks the API to derive the slug', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.find('#product-name').setValue('  New Lamp  ')
    await wrapper.find('#product-price').setValue('1500')
    await wrapper.find('#product-stock').setValue('4')
    await wrapper.find('form').trigger('submit')

    const payload = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>

    expect(payload.name).toBe('New Lamp')
    expect(payload.slug).toBeNull()
    expect(payload.price).toBe(1500)
    expect(payload.stock).toBe(4)
    expect(payload.categoryId).toBe(1)
  })

  it('emits a cancel event', async () => {
    const wrapper = mount(AdminProductForm, { props: { product: null, categories } })

    await wrapper.findAll('button').at(1)!.trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })
})

describe('AdminOrderTable', () => {
  it('renders one row per order', () => {
    const wrapper = mount(AdminOrderTable, { props: { orders: [order(), order({ id: 4 })] } })

    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
  })

  it('shows the customer and total', () => {
    const wrapper = mount(AdminOrderTable, { props: { orders: [order()] } })

    expect(wrapper.text()).toContain('Rohan Mehta')
    expect(wrapper.text()).toContain('customer@shophub.local')
  })

  it('offers the valid next statuses for a pending order', () => {
    const wrapper = mount(AdminOrderTable, { props: { orders: [order({ status: 'Pending' })] } })

    const options = wrapper.findAll('select option').map((o) => o.text())
    expect(options).toContain('Paid')
    expect(options).toContain('Cancelled')
    expect(options).not.toContain('Delivered')
  })

  it('offers nothing further for a delivered order', () => {
    const wrapper = mount(AdminOrderTable, { props: { orders: [order({ status: 'Delivered' })] } })

    const options = wrapper.findAll('select option').map((o) => o.text())
    expect(options).toEqual(['Delivered'])
  })

  it('emits the new status when the select changes', async () => {
    const wrapper = mount(AdminOrderTable, { props: { orders: [order({ status: 'Pending' })] } })

    await wrapper.find('select').setValue('Paid')

    expect(wrapper.emitted('status')?.[0]).toEqual([3, 'Paid'])
  })
})

describe('AddressForm', () => {
  function blank(): AddressInput {
    return {
      firstName: '',
      lastName: '',
      line1: '',
      line2: '',
      city: '',
      state: '',
      postalCode: '',
      country: 'India',
      phone: '',
    }
  }

  it('shows a required-field message for an empty field', () => {
    const wrapper = mount(AddressForm, { props: { modelValue: blank(), heading: 'Shipping address' } })

    expect(wrapper.text()).toContain('First name is required')
    expect(wrapper.text()).toContain('Address line 1 is required')
  })

  it('clears the message once a field is filled', async () => {
    const wrapper = mount(AddressForm, { props: { modelValue: blank() } })

    await wrapper.find('input[id$="firstName"]').setValue('Asha')

    expect(wrapper.text()).not.toContain('First name is required')
  })

  it('emits the updated model on input so the parent gate updates live', async () => {
    const model = blank()
    const wrapper = mount(AddressForm, { props: { modelValue: model } })

    await wrapper.find('input[id$="city"]').setValue('Pune')

    const emitted = wrapper.emitted('update:modelValue') as unknown as [AddressInput][]
    expect(emitted.at(-1)?.[0].city).toBe('Pune')
  })

  it('prefers a server-side validation message', () => {
    const wrapper = mount(AddressForm, {
      props: { modelValue: blank(), errors: { city: 'That city is not serviceable.' } },
    })

    expect(wrapper.text()).toContain('That city is not serviceable.')
  })

  it('shows the default toggle only when asked', () => {
    const plain = mount(AddressForm, { props: { modelValue: blank() } })
    const withToggle = mount(AddressForm, {
      props: { modelValue: blank(), showDefaultToggle: true },
    })

    expect(plain.text()).not.toContain('Use as my default address')
    expect(withToggle.text()).toContain('Use as my default address')
  })

  it('emits the default toggle value', async () => {
    const wrapper = mount(AddressForm, {
      props: { modelValue: blank(), showDefaultToggle: true },
    })

    await wrapper.find('input[type="checkbox"]').setValue(true)

    expect(wrapper.emitted('update:isDefault')?.[0]).toEqual([true])
  })
})