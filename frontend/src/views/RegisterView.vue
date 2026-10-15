<script setup lang="ts">
/** Registration page. Owns routing and chrome; the form itself is `RegisterForm`. */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import RegisterForm, { type RegisterModel } from '@/components/account/RegisterForm.vue'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'

const auth = useAuthStore()
const ui = useUiStore()
const router = useRouter()

const model = ref<RegisterModel>({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
})

const submitting = ref(false)
const serverError = ref<string | null>(null)

async function onSubmit(): Promise<void> {
  submitting.value = true
  serverError.value = null

  try {
    await auth.register({
      email: model.value.email.trim(),
      password: model.value.password,
      firstName: model.value.firstName.trim(),
      lastName: model.value.lastName.trim(),
    })
    ui.notifySuccess('Welcome to ShopHub.')
    await router.push({ name: 'account' })
  } catch (error) {
    serverError.value = toErrorMessage(error, 'Registration failed. Please try again.')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <div class="auth-card">
        <p class="kicker">Join ShopHub</p>
        <h1 class="section-title h2 mb-4">Create account</h1>

        <RegisterForm
          v-model="model"
          :submitting="submitting"
          :error="serverError"
          @submit="onSubmit"
        />

        <p class="text-center mt-4 mb-0 small">
          Already registered?
          <RouterLink :to="{ name: 'login' }">Sign in</RouterLink>
        </p>
      </div>
    </div>
  </section>
</template>
