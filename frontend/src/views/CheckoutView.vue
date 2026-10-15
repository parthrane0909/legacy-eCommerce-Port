<script setup lang="ts">
/**
 * Checkout page. Owns data loading, state and navigation; the three-step flow is
 * `CheckoutForm` and the totals card is `OrderSummary`.
 *
 * The address fields are held in refs rather than `reactive` consts: `v-model` on a reactive
 * const compiles to a plain assignment, so the parent would never observe the child's edits.
 */
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import * as ordersApi from '@/api/orders'
import * as accountApi from '@/api/account'
import CheckoutForm, { type CheckoutStep } from '@/components/checkout/CheckoutForm.vue'
import OrderSummary from '@/components/cart/OrderSummary.vue'
import { useCartStore } from '@/stores/cart'
import { useOrderStore } from '@/stores/orders'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'
import { formatCurrency } from '@/utils/format'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import type { Address, AddressInput, ShippingOption } from '@/types'

const cart = useCartStore()
const orders = useOrderStore()
const ui = useUiStore()
const router = useRouter()

function emptyAddress(): AddressInput {
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

const step = ref<CheckoutStep>('address')
const savedAddresses = ref<Address[]>([])
const shippingOptions = ref<ShippingOption[]>([])
const selectedShipping = ref('Standard')
const billingSameAsShipping = ref(true)
const serverErrors = ref<Record<string, string>>({})
const submitting = ref(false)
const loadError = ref<string | null>(null)

const shippingAddress = ref<AddressInput>(emptyAddress())
const billingAddress = ref<AddressInput>(emptyAddress())

const shippingCost = computed(
  () => shippingOptions.value.find((o) => o.code === selectedShipping.value)?.cost ?? 150,
)

function addressToInput(address: Address): AddressInput {
  return {
    firstName: address.firstName,
    lastName: address.lastName,
    line1: address.line1,
    line2: address.line2 ?? '',
    city: address.city,
    state: address.state,
    postalCode: address.postalCode,
    country: address.country,
    phone: address.phone ?? '',
  }
}

onMounted(async () => {
  try {
    await cart.fetch()
    const [addresses, options] = await Promise.all([
      accountApi.getAddresses(),
      ordersApi.getShippingOptions(),
    ])

    savedAddresses.value = addresses
    shippingOptions.value = options

    const preferred = addresses.find((a) => a.isDefault) ?? addresses[0]
    if (preferred) useSavedAddress(preferred)
  } catch (cause) {
    loadError.value = toErrorMessage(cause, 'Checkout could not be prepared.')
  }
})

function useSavedAddress(address: Address): void {
  const fromSaved = addressToInput(address)
  shippingAddress.value = fromSaved
  if (billingSameAsShipping.value) billingAddress.value = { ...fromSaved }
}

// `CheckoutForm` disables "Continue to shipping" until the address is complete, so the parent
// does not re-validate here; it only owns which step is current.
function onStepChange(next: CheckoutStep): void {
  step.value = next
}

function onBillingSameAsShipping(value: boolean): void {
  billingSameAsShipping.value = value
  if (value) billingAddress.value = { ...shippingAddress.value }
}

async function placeOrder(): Promise<void> {
  submitting.value = true
  serverErrors.value = {}

  const order = await orders.place({
    shippingMethod: selectedShipping.value,
    shippingAddress: { ...shippingAddress.value },
    billingAddress: billingSameAsShipping.value
      ? { ...shippingAddress.value }
      : { ...billingAddress.value },
    billingSameAsShipping: billingSameAsShipping.value,
  })

  if (!order) {
    serverErrors.value = { submit: orders.error ?? 'The order could not be placed.' }
    submitting.value = false
    return
  }

  // The API emptied the cart as part of the transaction; resync the badge.
  await cart.fetch()
  ui.notifySuccess('Order placed.')
  await router.push({ name: 'order-details', params: { id: order.id } })
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <p class="kicker">Checkout</p>
      <h1 class="section-title mb-4">Complete your order</h1>

      <div v-if="loadError" class="error-state" role="alert">
        <h3>Checkout unavailable</h3>
        <p>{{ loadError }}</p>
      </div>

      <div v-else-if="cart.isEmpty" class="empty-state">
        <h3>Your cart is empty</h3>
        <p>Add something to your cart before checking out.</p>
        <RouterLink class="btn-pill btn-accent mt-2" :to="{ name: 'products' }">
          Browse products →
        </RouterLink>
      </div>

      <LoadingSpinner v-else-if="cart.loading" label="Preparing checkout…" />

      <div v-else class="cart-layout">
        <CheckoutForm
          :step="step"
          :item-count="cart.itemCount"
          :subtotal="cart.subtotal"
          :shipping-cost="shippingCost"
          :shipping-method="selectedShipping"
          :shipping-address="shippingAddress"
          :billing-address="billingAddress"
          :billing-same-as-shipping="billingSameAsShipping"
          :saved-addresses="savedAddresses"
          :shipping-options="shippingOptions"
          :submitting="submitting"
          :server-errors="serverErrors"
          @update:step="onStepChange"
          @update:shipping-address="shippingAddress = $event"
          @update:billing-address="billingAddress = $event"
          @update:billing-same-as-shipping="onBillingSameAsShipping"
          @update:shipping-method="selectedShipping = $event"
          @place="placeOrder"
        >
          <template #review-lines>
            <table class="table table-sm align-middle">
              <thead>
                <tr>
                  <th>Product</th>
                  <th class="text-end">Qty</th>
                  <th class="text-end">Total</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in cart.items" :key="item.id">
                  <td>
                    {{ item.productName }}
                    <span v-if="item.variantName" class="text-secondary">({{ item.variantName }})</span>
                  </td>
                  <td class="text-end">{{ item.quantity }}</td>
                  <td class="text-end">{{ formatCurrency(item.lineTotal) }}</td>
                </tr>
              </tbody>
            </table>
          </template>
        </CheckoutForm>

        <OrderSummary
          :item-count="cart.itemCount"
          :subtotal="cart.subtotal"
          :shipping-cost="shippingCost"
          :shipping-label="`Shipping (${selectedShipping})`"
          note="Totals are recalculated and verified by the server when the order is placed."
        />
      </div>
    </div>
  </section>
</template>
