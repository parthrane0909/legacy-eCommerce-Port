<script setup lang="ts">
/**
 * Registration form.
 *
 * Extracted from `RegisterView.vue`. Presentational by design: the parent owns the async
 * register call and passes `submitting` and `error` back down. The password checklist mirrors
 * `PasswordPolicy` on the API, so the shopper is never rejected after submitting.
 */
import { computed } from 'vue'

export interface RegisterModel {
  firstName: string
  lastName: string
  email: string
  password: string
  confirmPassword: string
}

const props = withDefaults(
  defineProps<{
    modelValue: RegisterModel
    submitting?: boolean
    error?: string | null
  }>(),
  { submitting: false, error: null },
)

const emit = defineEmits<{
  'update:modelValue': [value: RegisterModel]
  submit: []
}>()

const model = computed(() => props.modelValue)

/** Same rules the API enforces, shown live as a checklist. */
const PASSWORD_RULES = [
  { label: 'At least 8 characters', test: (p: string) => p.length >= 8 },
  { label: 'An uppercase letter', test: (p: string) => /[A-Z]/.test(p) },
  { label: 'A lowercase letter', test: (p: string) => /[a-z]/.test(p) },
  { label: 'A digit', test: (p: string) => /\d/.test(p) },
  { label: 'A non-alphanumeric character', test: (p: string) => /[^A-Za-z0-9]/.test(p) },
]

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(model.value.email.trim()))
const passwordValid = computed(() => PASSWORD_RULES.every((rule) => rule.test(model.value.password)))
const passwordsMatch = computed(() => model.value.password === model.value.confirmPassword)

const canSubmit = computed(
  () =>
    emailValid.value &&
    passwordValid.value &&
    passwordsMatch.value &&
    model.value.firstName.trim().length > 0 &&
    model.value.lastName.trim().length > 0 &&
    !props.submitting,
)

function patch(changes: Partial<RegisterModel>): void {
  emit('update:modelValue', { ...model.value, ...changes })
}
</script>

<template>
  <div>
    <div v-if="error" class="alert alert-danger py-2" role="alert">{{ error }}</div>

    <form novalidate @submit.prevent="canSubmit && emit('submit')">
      <div class="row g-3">
        <div class="col-md-6">
          <div class="field">
            <label for="reg-first">First name</label>
            <input
              id="reg-first"
              :value="model.firstName"
              type="text"
              autocomplete="given-name"
              required
              @input="patch({ firstName: ($event.target as HTMLInputElement).value })"
            />
          </div>
        </div>
        <div class="col-md-6">
          <div class="field">
            <label for="reg-last">Last name</label>
            <input
              id="reg-last"
              :value="model.lastName"
              type="text"
              autocomplete="family-name"
              required
              @input="patch({ lastName: ($event.target as HTMLInputElement).value })"
            />
          </div>
        </div>
      </div>

      <div class="field">
        <label for="reg-email">Email</label>
        <input
          id="reg-email"
          :value="model.email"
          type="email"
          autocomplete="email"
          required
          @input="patch({ email: ($event.target as HTMLInputElement).value })"
        />
        <span v-if="model.email && !emailValid" class="field-error">
          Enter a valid email address.
        </span>
      </div>

      <div class="field">
        <label for="reg-password">Password</label>
        <input
          id="reg-password"
          :value="model.password"
          type="password"
          autocomplete="new-password"
          required
          @input="patch({ password: ($event.target as HTMLInputElement).value })"
        />
        <ul class="list-unstyled small mt-1 mb-0">
          <li v-for="rule in PASSWORD_RULES" :key="rule.label">
            <span :class="rule.test(model.password) ? 'text-success' : 'text-secondary'">
              {{ rule.test(model.password) ? '✓' : '•' }} {{ rule.label }}
            </span>
          </li>
        </ul>
      </div>

      <div class="field">
        <label for="reg-confirm">Confirm password</label>
        <input
          id="reg-confirm"
          :value="model.confirmPassword"
          type="password"
          autocomplete="new-password"
          required
          @input="patch({ confirmPassword: ($event.target as HTMLInputElement).value })"
        />
        <span v-if="model.confirmPassword && !passwordsMatch" class="field-error">
          The passwords do not match.
        </span>
      </div>

      <button
        class="btn-pill btn-accent w-100 justify-content-center"
        type="submit"
        :disabled="!canSubmit"
      >
        <span v-if="submitting" class="spinner-border spinner-border-sm" aria-hidden="true" />
        {{ submitting ? 'Creating account…' : 'Create account' }}
      </button>
    </form>
  </div>
</template>
