<script setup lang="ts">
/**
 * Sign-in form.
 *
 * Extracted from `LoginView.vue` so the view owns routing and page chrome only.
 * Presentational by design: the parent owns the async login call and passes `submitting`
 * and `error` back down. Validation mirrors the API's `LoginRequestValidator`, so the shopper
 * gets immediate feedback while the API remains the authority that rejects anything invalid.
 */
import { computed } from 'vue'

export interface LoginCredentials {
  email: string
  password: string
}

const props = withDefaults(
  defineProps<{
    modelValue: LoginCredentials
    submitting?: boolean
    error?: string | null
  }>(),
  { submitting: false, error: null },
)

const emit = defineEmits<{
  'update:modelValue': [value: LoginCredentials]
  submit: []
}>()

const credentials = computed(() => props.modelValue)

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(credentials.value.email.trim()))

const canSubmit = computed(
  () => emailValid.value && credentials.value.password.length > 0 && !props.submitting,
)

function patch(changes: Partial<LoginCredentials>): void {
  emit('update:modelValue', { ...credentials.value, ...changes })
}
</script>

<template>
  <div>
    <div v-if="error" class="alert alert-danger py-2" role="alert">{{ error }}</div>

    <form novalidate @submit.prevent="canSubmit && emit('submit')">
      <div class="field">
        <label for="login-email">Email</label>
        <input
          id="login-email"
          :value="credentials.email"
          type="email"
          autocomplete="email"
          required
          @input="patch({ email: ($event.target as HTMLInputElement).value })"
        />
        <span v-if="credentials.email && !emailValid" class="field-error">
          Enter a valid email address.
        </span>
      </div>

      <div class="field">
        <label for="login-password">Password</label>
        <input
          id="login-password"
          :value="credentials.password"
          type="password"
          autocomplete="current-password"
          required
          @input="patch({ password: ($event.target as HTMLInputElement).value })"
        />
      </div>

      <button
        class="btn-pill btn-accent w-100 justify-content-center"
        type="submit"
        :disabled="!canSubmit"
      >
        <span v-if="submitting" class="spinner-border spinner-border-sm" aria-hidden="true" />
        {{ submitting ? 'Signing in…' : 'Sign in' }}
      </button>
    </form>
  </div>
</template>
