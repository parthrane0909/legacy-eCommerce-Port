<script setup lang="ts">
/**
 * Address form used for both shipping and billing.
 *
 * Validation is duplicated from the FluentValidation rules on the API on purpose: the shopper
 * gets immediate feedback, and the API remains the authority that rejects anything invalid.
 *
 * Changes are emitted on every keystroke rather than on blur, so a parent that gates its submit
 * button on validity re-evaluates as the shopper types.
 */
import { reactive, computed, watch } from 'vue'
import type { AddressInput } from '@/types'

const props = withDefaults(
  defineProps<{
    modelValue: AddressInput
    heading?: string
    showDefaultToggle?: boolean
    isDefault?: boolean
    errors?: Record<string, string>
  }>(),
  {
    heading: 'Address',
    showDefaultToggle: false,
    isDefault: false,
    errors: () => ({}),
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: AddressInput]
  'update:isDefault': [value: boolean]
}>()

const form = reactive<AddressInput>({ ...props.modelValue })

watch(
  () => props.modelValue,
  (value) => Object.assign(form, value),
  { deep: true },
)

/** Field -> validator. Kept as a plain map so it can be called per render. */
const RULES: Record<string, (value: string) => string> = {
  firstName: (v) => (v.trim() ? '' : 'First name is required'),
  lastName: (v) => (v.trim() ? '' : 'Last name is required'),
  line1: (v) => (v.trim() ? '' : 'Address line 1 is required'),
  city: (v) => (v.trim() ? '' : 'City is required'),
  state: (v) => (v.trim() ? '' : 'State is required'),
  postalCode: (v) => (v.trim() ? '' : 'Postal code is required'),
  country: (v) => (v.trim() ? '' : 'Country is required'),
}

/**
 * `form` is typed as AddressInput, which has no string index signature, so reading it with a
 * dynamic key needs an explicit widening.
 */
const formFields = form as unknown as Record<string, string | null | undefined>

/** Server-side error wins; otherwise apply the local rule. */
function errorFor(field: string): string {
  return props.errors[field] ?? RULES[field]?.(String(formFields[field] ?? '')) ?? ''
}

const hasErrors = computed(() =>
  Object.keys(RULES).some((field) => errorFor(field).length > 0),
)

function commit(): void {
  emit('update:modelValue', { ...form })
}

defineExpose({ form, hasErrors, commit })
</script>

<template>
  <fieldset class="border-0 p-0 m-0">
    <legend class="h6 mb-3">{{ heading }}</legend>

    <div class="row g-3">
      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-firstName`">First name</label>
          <input
            :id="`${heading}-firstName`"
            v-model="form.firstName"
            type="text"
            autocomplete="given-name"
            @input="commit"
          />
          <span v-if="errorFor('firstName')" class="field-error">{{ errorFor('firstName') }}</span>
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-lastName`">Last name</label>
          <input
            :id="`${heading}-lastName`"
            v-model="form.lastName"
            type="text"
            autocomplete="family-name"
            @input="commit"
          />
          <span v-if="errorFor('lastName')" class="field-error">{{ errorFor('lastName') }}</span>
        </div>
      </div>

      <div class="col-12">
        <div class="field">
          <label :for="`${heading}-line1`">Address line 1</label>
          <input
            :id="`${heading}-line1`"
            v-model="form.line1"
            type="text"
            autocomplete="address-line1"
            @input="commit"
          />
          <span v-if="errorFor('line1')" class="field-error">{{ errorFor('line1') }}</span>
        </div>
      </div>

      <div class="col-12">
        <div class="field">
          <label :for="`${heading}-line2`">Address line 2 (optional)</label>
          <input
            :id="`${heading}-line2`"
            v-model="form.line2"
            type="text"
            autocomplete="address-line2"
            @input="commit"
          />
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-city`">City</label>
          <input
            :id="`${heading}-city`"
            v-model="form.city"
            type="text"
            autocomplete="address-level2"
            @input="commit"
          />
          <span v-if="errorFor('city')" class="field-error">{{ errorFor('city') }}</span>
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-state`">State</label>
          <input
            :id="`${heading}-state`"
            v-model="form.state"
            type="text"
            autocomplete="address-level1"
            @input="commit"
          />
          <span v-if="errorFor('state')" class="field-error">{{ errorFor('state') }}</span>
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-postalCode`">Postal code</label>
          <input
            :id="`${heading}-postalCode`"
            v-model="form.postalCode"
            type="text"
            autocomplete="postal-code"
            @input="commit"
          />
          <span v-if="errorFor('postalCode')" class="field-error">{{ errorFor('postalCode') }}</span>
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-country`">Country</label>
          <input
            :id="`${heading}-country`"
            v-model="form.country"
            type="text"
            autocomplete="country-name"
            @input="commit"
          />
          <span v-if="errorFor('country')" class="field-error">{{ errorFor('country') }}</span>
        </div>
      </div>

      <div class="col-md-6">
        <div class="field">
          <label :for="`${heading}-phone`">Phone</label>
          <input
            :id="`${heading}-phone`"
            v-model="form.phone"
            type="tel"
            autocomplete="tel"
            @input="commit"
          />
        </div>
      </div>
    </div>

    <label v-if="showDefaultToggle" class="d-flex align-items-center gap-2 mt-3">
      <input
        type="checkbox"
        :checked="isDefault"
        @change="emit('update:isDefault', ($event.target as HTMLInputElement).checked)"
      />
      <span class="small">Use as my default address</span>
    </label>
  </fieldset>
</template>