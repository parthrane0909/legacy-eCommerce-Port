<script setup lang="ts">
/**
 * Multi-step checkout form: Address → Shipping → Review.
 *
 * Extracted from `CheckoutView.vue`, which now owns data loading and navigation only. The step
 * progression, address editing, shipping selection and review table all live here, so the same
 * flow can be reused (or unit-tested) independently of the page.
 *
 * Totals shown are a client-side preview. The server recomputes and validates every figure
 * when the order is placed, and this component says so.
 */
import { computed } from 'vue'
import { formatCurrency } from '@/utils/format'
import AddressForm from '@/components/account/AddressForm.vue'
import type { Address, AddressInput, ShippingOption } from '@/types'

export type CheckoutStep = 'address' | 'shipping' | 'review'

const props = withDefaults(
  defineProps<{
    step: CheckoutStep
    itemCount: number
    subtotal: number
    shippingCost: number
    shippingMethod: string
    shippingAddress: AddressInput
    billingAddress: AddressInput
    billingSameAsShipping: boolean
    savedAddresses: Address[]
    shippingOptions: ShippingOption[]
    submitting?: boolean
    serverErrors?: Record<string, string>
  }>(),
  { submitting: false, serverErrors: () => ({}) },
)

const emit = defineEmits<{
  'update:step': [step: CheckoutStep]
  'update:shippingAddress': [value: AddressInput]
  'update:billingAddress': [value: AddressInput]
  'update:billingSameAsShipping': [value: boolean]
  'update:shippingMethod': [value: string]
  place: []
}>()

const STEPS: { key: CheckoutStep; label: string }[] = [
  { key: 'address', label: 'Address' },
  { key: 'shipping', label: 'Shipping' },
  { key: 'review', label: 'Review' },
]

/** Field keys that must be non-blank, mirroring the API's address validator. */
const REQUIRED_ADDRESS_FIELDS: (keyof AddressInput)[] = [
  'firstName',
  'lastName',
  'line1',
  'city',
  'state',
  'postalCode',
  'country',
]

/** Maps a saved address onto the input shape the form binds to. */
function addressToInput(address: Address) {
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

const shipping = computed(() => props.shippingAddress)

const addressValid = computed(() =>
  REQUIRED_ADDRESS_FIELDS.every((field) => String(shipping.value[field] ?? '').trim().length > 0),
)

const stepIndex = computed(() => STEPS.findIndex((s) => s.key === props.step))
</script>

<template>
  <div>
    <!-- Step indicator -->
    <ol class="d-flex gap-4 list-unstyled mb-4 flex-wrap">
      <li v-for="(s, index) in STEPS" :key="s.key" class="d-flex align-items-center gap-2">
        <span
          class="d-inline-grid place-items-center rounded-circle"
          :style="{
            width: '28px',
            height: '28px',
            background: index <= stepIndex ? 'var(--ink)' : 'var(--line)',
            color: index <= stepIndex ? '#fff' : 'var(--muted)',
            fontSize: '13px',
          }"
        >
          {{ index < stepIndex ? '✓' : index + 1 }}
        </span>
        <span :class="index === stepIndex ? 'fw-semibold' : 'text-secondary'">{{ s.label }}</span>
      </li>
    </ol>

    <!-- Step 1: address -->
    <div v-show="step === 'address'" class="summary-card" style="position: static">
      <div v-if="savedAddresses.length" class="mb-4">
        <p class="kicker mb-2">Saved addresses</p>
        <div class="d-flex gap-2 flex-wrap">
          <button
            v-for="address in savedAddresses"
            :key="address.id"
            class="pill"
            type="button"
            @click="emit('update:shippingAddress', addressToInput(address))"
          >
            {{ address.city }}, {{ address.country }}
          </button>
        </div>
      </div>

      <AddressForm
        :model-value="shippingAddress"
        heading="Shipping address"
        :errors="serverErrors"
        @update:model-value="emit('update:shippingAddress', $event)"
      />

      <hr class="my-4" />

      <label class="d-flex align-items-center gap-2 mb-3">
        <input
          :checked="billingSameAsShipping"
          type="checkbox"
          @change="emit('update:billingSameAsShipping', ($event.target as HTMLInputElement).checked)"
        />
        <span class="small">Billing address is the same as shipping</span>
      </label>

      <AddressForm
        v-if="!billingSameAsShipping"
        :model-value="billingAddress"
        heading="Billing address"
        :errors="serverErrors"
        @update:model-value="emit('update:billingAddress', $event)"
      />

      <button
        class="btn-pill btn-accent mt-3"
        type="button"
        :disabled="!addressValid"
        @click="emit('update:step', 'shipping')"
      >
        Continue to shipping →
      </button>
    </div>

    <!-- Step 2: shipping method -->
    <div v-show="step === 'shipping'" class="summary-card" style="position: static">
      <h2 class="h6 mb-3">Shipping method</h2>

      <label
        v-for="option in shippingOptions"
        :key="option.code"
        class="d-flex align-items-center gap-3 border rounded-3 p-3 mb-2"
        style="cursor: pointer"
        :style="shippingMethod === option.code ? 'border-color: var(--accent)' : ''"
      >
        <input
          :checked="shippingMethod === option.code"
          type="radio"
          :value="option.code"
          @change="emit('update:shippingMethod', option.code)"
        />
        <span class="flex-grow-1">{{ option.label }} · {{ option.estimate }}</span>
        <strong>{{ formatCurrency(option.cost) }}</strong>
      </label>

      <p class="small text-secondary mt-3 mb-0">
        Demonstration only — no payment is taken at this step.
      </p>

      <div class="d-flex gap-2 mt-3">
        <button class="btn-pill" type="button" @click="emit('update:step', 'address')">← Back</button>
        <button class="btn-pill btn-accent" type="button" @click="emit('update:step', 'review')">
          Review order →
        </button>
      </div>
    </div>

    <!-- Step 3: review and place -->
    <div v-show="step === 'review'" class="summary-card" style="position: static">
      <h2 class="h6 mb-3">Review your order</h2>

      <slot name="review-lines">
        <p class="text-secondary small">No items to review.</p>
      </slot>

      <p class="small text-secondary mb-4">
        <strong>Ship to:</strong> {{ shippingAddress.firstName }} {{ shippingAddress.lastName }},
        {{ shippingAddress.line1 }}, {{ shippingAddress.city }}, {{ shippingAddress.state }}
        {{ shippingAddress.postalCode }}, {{ shippingAddress.country }}
      </p>

      <div v-if="serverErrors.submit" class="alert alert-danger py-2" role="alert">
        {{ serverErrors.submit }}
      </div>

      <div class="d-flex gap-2">
        <button class="btn-pill" type="button" @click="emit('update:step', 'shipping')">← Back</button>
        <button
          class="btn-pill btn-accent"
          type="button"
          :disabled="submitting"
          @click="emit('place')"
        >
          <span v-if="submitting" class="spinner-border spinner-border-sm" aria-hidden="true" />
          {{ submitting ? 'Placing order…' : 'Place order' }}
        </button>
      </div>
    </div>
  </div>
</template>
