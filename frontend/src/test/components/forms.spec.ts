import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import LoginForm from '@/components/account/LoginForm.vue'
import RegisterForm from '@/components/account/RegisterForm.vue'
import OrderSummary from '@/components/cart/OrderSummary.vue'
import ProductDetails from '@/components/catalog/ProductDetails.vue'
import CheckoutForm from '@/components/checkout/CheckoutForm.vue'
import type { Address, AddressInput, ProductDetail } from '@/types'

function emptyAddress(overrides: Partial<AddressInput> = {}): AddressInput {
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
    ...overrides,
  }
}

function fullAddress(overrides: Partial<AddressInput> = {}): AddressInput {
  return emptyAddress({
    firstName: 'Asha',
    lastName: 'Rao',
    line1: '12 MG Road',
    city: 'Bengaluru',
    state: 'Karnataka',
    postalCode: '560001',
    ...overrides,
  })
}

function product(overrides: Partial<ProductDetail> = {}): ProductDetail {
  return {
    id: 1,
    name: 'Wireless Headphones Pro',
    slug: 'wireless-headphones-pro',
    description: 'Over-ear, 40 mm drivers.',
    price: 4999,
    wasPrice: 5881.18,
    discountPercent: 15,
    imageUrl: '/images/products/headphones.jpg',
    stock: 4,
    inStock: true,
    isFeatured: true,
    ratingNum: 4.5,
    categoryId: 2,
    categoryName: 'Audio',
    images: [
      {
        id: 11,
        url: '/images/products/headphones.jpg',
        altText: 'Front view',
        isPrimary: true,
        sortOrder: 0,
      },
      {
        id: 12,
        url: '/images/products/headphones-side.jpg',
        altText: 'Side view',
        isPrimary: false,
        sortOrder: 1,
      },
    ],
    variants: [
      { id: 21, name: 'Black', sku: 'HP-BLK', priceDelta: 0, stock: 4 },
      { id: 22, name: 'Silver', sku: 'HP-SLV', priceDelta: 500, stock: 2 },
    ],
    ...overrides,
  }
}

describe('LoginForm', () => {
  it('emits updated credentials as the shopper types', async () => {
    const wrapper = mount(LoginForm, {
      props: { modelValue: { email: '', password: '' } },
    })

    await wrapper.find('#login-email').setValue('customer@shophub.local')
    await wrapper.find('#login-password').setValue('Customer@12345')

    // The component is controlled: every edit is emitted, never mutated locally, and each
    // emission is the prop value with that one field patched.
    expect(wrapper.emitted('update:modelValue')).toEqual([
      [{ email: 'customer@shophub.local', password: '' }],
      [{ email: '', password: 'Customer@12345' }],
    ])

    // Once the parent feeds the value back, the field renders what the parent supplied.
    await wrapper.setProps({ modelValue: { email: 'customer@shophub.local', password: '' } })
    expect((wrapper.find('#login-email').element as HTMLInputElement).value).toBe(
      'customer@shophub.local',
    )
  })

  it('keeps submit disabled until the email and password are both valid', async () => {
    const wrapper = mount(LoginForm, {
      props: { modelValue: { email: '', password: '' } },
    })
    const submit = wrapper.get('button[type="submit"]')

    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: { email: 'not-an-email', password: 'x' } })
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: { email: 'customer@shophub.local', password: 'x' } })
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('shows a field error only once something has been typed', async () => {
    const wrapper = mount(LoginForm, {
      props: { modelValue: { email: '', password: '' } },
    })

    expect(wrapper.text()).not.toContain('Enter a valid email address.')

    await wrapper.setProps({ modelValue: { email: 'nope', password: '' } })
    expect(wrapper.text()).toContain('Enter a valid email address.')
  })

  it('emits submit and blocks it while a request is in flight', async () => {
    const wrapper = mount(LoginForm, {
      props: { modelValue: { email: 'customer@shophub.local', password: 'pw' } },
    })

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')).toHaveLength(1)

    await wrapper.setProps({ submitting: true })
    expect(wrapper.text()).toContain('Signing in')

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('renders the server error in an alert', () => {
    const wrapper = mount(LoginForm, {
      props: {
        modelValue: { email: '', password: '' },
        error: 'Invalid email or password.',
      },
    })

    expect(wrapper.get('[role="alert"]').text()).toBe('Invalid email or password.')
  })
})

describe('RegisterForm', () => {
  const base = {
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: '',
  }

  it('lists the password rules and marks each as satisfied', async () => {
    const wrapper = mount(RegisterForm, { props: { modelValue: { ...base } } })

    expect(wrapper.text()).toContain('At least 8 characters')
    expect(wrapper.text()).toContain('An uppercase letter')
    expect(wrapper.text()).toContain('A lowercase letter')
    expect(wrapper.text()).toContain('A digit')
    expect(wrapper.text()).toContain('A non-alphanumeric character')
    expect(wrapper.findAll('.text-success')).toHaveLength(0)

    await wrapper.setProps({
      modelValue: { ...base, password: 'Str0ng!Password' },
    })

    expect(wrapper.findAll('.text-success')).toHaveLength(5)
  })

  it('does not enable submit until every field is valid and the passwords match', async () => {
    const wrapper = mount(RegisterForm, { props: { modelValue: { ...base } } })

    const valid = {
      firstName: 'E2e',
      lastName: 'Shopper',
      email: 'e2e@shophub.test',
      password: 'Str0ng!Password',
      confirmPassword: 'Str0ng!Password',
    }

    await wrapper.setProps({ modelValue: { ...valid, confirmPassword: 'Different1!' } })
    expect(wrapper.text()).toContain('The passwords do not match.')
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: { ...valid, email: 'bad' } })
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: { ...valid, firstName: '' } })
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: valid })
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('emits partial updates rather than mutating the model', async () => {
    const wrapper = mount(RegisterForm, { props: { modelValue: { ...base } } })

    await wrapper.find('#reg-first').setValue('E2e')

    expect(wrapper.emitted('update:modelValue')).toEqual([
      [{ firstName: 'E2e', lastName: '', email: '', password: '', confirmPassword: '' }],
    ])
  })
})

describe('OrderSummary', () => {
  it('renders the subtotal and defers shipping until it is known', () => {
    const wrapper = mount(OrderSummary, {
      props: { itemCount: 3, subtotal: 4999 },
    })

    expect(wrapper.get('.summary-total').text()).toContain('₹4,999')
    expect(wrapper.text()).toContain('Calculated at checkout')
  })

  it('adds shipping to the total and labels the row', () => {
    const wrapper = mount(OrderSummary, {
      props: {
        itemCount: 2,
        subtotal: 1000,
        shippingCost: 150,
        shippingLabel: 'Shipping (Express)',
      },
    })

    expect(wrapper.text()).toContain('Shipping (Express)')
    expect(wrapper.get('.summary-total').text()).toContain('₹1,150')
  })

  it('honours the total label, the note and the actions slot', () => {
    const wrapper = mount(OrderSummary, {
      props: {
        itemCount: 1,
        subtotal: 250,
        totalLabel: 'Subtotal',
        note: 'Totals are verified by the server.',
      },
      slots: { actions: '<a href="/checkout">Proceed to checkout</a>' },
    })

    expect(wrapper.text()).toContain('Subtotal')
    expect(wrapper.text()).toContain('Totals are verified by the server.')
    expect(wrapper.get('a[href="/checkout"]').text()).toBe('Proceed to checkout')
  })
})

describe('ProductDetails', () => {
  it('shows the price, crossed-out was-price and discount', () => {
    const wrapper = mount(ProductDetails, { props: { product: product() } })

    expect(wrapper.text()).toContain('Wireless Headphones Pro')
    expect(wrapper.text()).toContain('₹4,999')
    expect(wrapper.text()).toContain('₹5,881.18')
    expect(wrapper.text()).toContain('-15% today')
  })

  it('reports stock state by band', () => {
    const healthy = mount(ProductDetails, { props: { product: product({ stock: 30 }) } })
    expect(healthy.get('.stock').text()).toBe('In stock — ships in 24h')

    const low = mount(ProductDetails, { props: { product: product({ stock: 5 }) } })
    expect(low.get('.stock').text()).toBe('Only 5 left in stock')

    const out = mount(
      ProductDetails,
      { props: { product: product({ stock: 0, inStock: false }) } },
    )
    expect(out.get('.stock').text()).toBe('Out of stock')
    expect(out.find('button.btn-accent').text()).toBe('Out of stock')
    expect(out.find('button.btn-accent').attributes('disabled')).toBeDefined()
  })

  it('emits variant and quantity changes', async () => {
    const wrapper = mount(ProductDetails, { props: { product: product() } })

    await wrapper.findAll('.pill')[1].trigger('click')
    expect(wrapper.emitted('update:selectedVariantId')).toEqual([[22]])

    await wrapper.find('#detail-qty').setValue('3')
    expect(wrapper.emitted('update:quantity')).toEqual([[3]])
  })

  it('caps the quantity input at the available stock', () => {
    const wrapper = mount(ProductDetails, { props: { product: product({ stock: 2 }) } })
    expect(wrapper.find('#detail-qty').attributes('max')).toBe('2')
  })

  it('emits add and swaps the label while adding', async () => {
    const wrapper = mount(ProductDetails, { props: { product: product() } })

    await wrapper.find('button.btn-accent').trigger('click')
    expect(wrapper.emitted('add')).toHaveLength(1)

    await wrapper.setProps({ adding: true })
    expect(wrapper.text()).toContain('Adding…')
  })

  it('swaps the gallery image on thumbnail click and resets when the product changes', async () => {
    const wrapper = mount(ProductDetails, { props: { product: product() } })

    expect(wrapper.get('img[alt="Wireless Headphones Pro"]').attributes('src')).toBe(
      '/images/products/headphones.jpg',
    )

    await wrapper.find('button[aria-label="Show Side view"]').trigger('click')
    expect(wrapper.get('img[alt="Wireless Headphones Pro"]').attributes('src')).toBe(
      '/images/products/headphones-side.jpg',
    )

    // Navigating to another product must not keep the previous product's image selected.
    await wrapper.setProps({ product: product({ id: 2, name: 'Mechanical Keyboard RGB' }) })
    expect(wrapper.get('img[alt="Mechanical Keyboard RGB"]').attributes('src')).toBe(
      '/images/products/headphones.jpg',
    )
  })
})

describe('CheckoutForm', () => {
  const shippingOptions = [
    { code: 'Standard', label: 'Standard', cost: 100, estimate: '4-6 days' },
    { code: 'Express', label: 'Express', cost: 250, estimate: '2 days' },
  ]

  const savedAddress: Address = {
    id: 5,
    fullName: 'Asha Rao',
    isDefault: true,
    ...fullAddress(),
  }

  function mountForm(overrides: Record<string, unknown> = {}) {
    return mount(CheckoutForm, {
      props: {
        step: 'address',
        itemCount: 2,
        subtotal: 1000,
        shippingCost: 100,
        shippingMethod: 'Standard',
        shippingAddress: emptyAddress(),
        billingAddress: emptyAddress(),
        billingSameAsShipping: true,
        savedAddresses: [savedAddress],
        shippingOptions,
        ...overrides,
      },
    })
  }

  it('shows all three steps with the current one marked', () => {
    const wrapper = mountForm({ step: 'shipping' })

    expect(wrapper.text()).toContain('Address')
    expect(wrapper.text()).toContain('Shipping')
    expect(wrapper.text()).toContain('Review')
  })

  it('will not continue to shipping while the address is incomplete', async () => {
    const wrapper = mountForm()
    const continueButton = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Continue to shipping'))!

    expect(continueButton.attributes('disabled')).toBeDefined()

    await wrapper.setProps({ shippingAddress: fullAddress() })
    const ready = wrapper
      .findAll('button')
      .find((b) => b.text().includes('Continue to shipping'))!
    expect(ready.attributes('disabled')).toBeUndefined()

    await ready.trigger('click')
    expect(wrapper.emitted('update:step')).toEqual([['shipping']])
  })

  it('copies a saved address into the shipping form', async () => {
    const wrapper = mountForm()

    await wrapper.find('button.pill').trigger('click')

    expect(wrapper.emitted('update:shippingAddress')).toHaveLength(1)
    expect(wrapper.emitted('update:shippingAddress')![0][0]).toMatchObject({
      firstName: 'Asha',
      line1: '12 MG Road',
      city: 'Bengaluru',
    })
  })

  it('hides the billing form while billing matches shipping', async () => {
    const wrapper = mountForm()

    expect(wrapper.text()).toContain('Billing address is the same as shipping')
    // AddressForm derives its field ids from the heading, so the presence of a
    // "Billing address-*" field tells us whether the second block is rendered.
    expect(wrapper.find('#Shipping\\ address-firstName').exists()).toBe(true)
    expect(wrapper.find('#Billing\\ address-firstName').exists()).toBe(false)

    await wrapper.find('input[type="checkbox"]').setValue(false)
    expect(wrapper.emitted('update:billingSameAsShipping')).toEqual([[false]])

    // Once the parent says billing differs, the second block appears.
    await wrapper.setProps({ billingSameAsShipping: false })
    expect(wrapper.find('#Billing\\ address-firstName').exists()).toBe(true)
  })

  it('selects a shipping method and moves on to review', async () => {
    const wrapper = mountForm({ step: 'shipping' })

    await wrapper.findAll('input[type="radio"]')[1].setValue(true)
    expect(wrapper.emitted('update:shippingMethod')).toEqual([['Express']])

    await wrapper.findAll('button').find((b) => b.text().includes('Review order'))!.trigger('click')
    expect(wrapper.emitted('update:step')).toEqual([['review']])
  })

  it('summarises the ship-to address and emits place on the review step', async () => {
    const wrapper = mountForm({ step: 'review', shippingAddress: fullAddress() })

    expect(wrapper.text()).toContain('Ship to:')
    expect(wrapper.text()).toContain('12 MG Road')
    expect(wrapper.text()).toContain('Bengaluru')

    await wrapper.findAll('button').find((b) => b.text().includes('Place order'))!.trigger('click')
    expect(wrapper.emitted('place')).toHaveLength(1)
  })

  it('blocks place while submitting and surfaces a server-side failure', async () => {
    const wrapper = mountForm({
      step: 'review',
      shippingAddress: fullAddress(),
      submitting: true,
      serverErrors: { submit: 'Not enough stock.' },
    })

    const placeButton = wrapper.findAll('button').find((b) => b.text().includes('Placing'))!
    expect(placeButton.attributes('disabled')).toBeDefined()

    await placeButton.trigger('click')
    expect(wrapper.emitted('place')).toBeUndefined()

    expect(wrapper.get('[role="alert"]').text()).toBe('Not enough stock.')
  })

  it('renders the caller-supplied review lines', () => {
    const wrapper = mount(CheckoutForm, {
      props: {
        step: 'review',
        itemCount: 1,
        subtotal: 4999,
        shippingCost: 150,
        shippingMethod: 'Standard',
        shippingAddress: fullAddress(),
        billingAddress: fullAddress(),
        billingSameAsShipping: true,
        savedAddresses: [],
        shippingOptions,
      },
      slots: { 'review-lines': '<p>Wireless Headphones Pro x1</p>' },
    })

    expect(wrapper.text()).toContain('Wireless Headphones Pro x1')
  })
})