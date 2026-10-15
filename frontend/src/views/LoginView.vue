<script setup lang="ts">
/** Sign-in page. Owns routing and chrome; the form itself is `LoginForm`. */
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import LoginForm, { type LoginCredentials } from '@/components/account/LoginForm.vue'
import { useAuthStore } from '@/stores/auth'
import { useUiStore } from '@/stores/ui'
import { toErrorMessage } from '@/api/client'

const auth = useAuthStore()
const ui = useUiStore()
const router = useRouter()
const route = useRoute()

const credentials = ref<LoginCredentials>({ email: '', password: '' })
const submitting = ref(false)
const serverError = ref<string | null>(null)

async function onSubmit(): Promise<void> {
  submitting.value = true
  serverError.value = null

  try {
    await auth.login(credentials.value.email, credentials.value.password)
    ui.notifySuccess('Welcome back.')

    // The router guard records where an unauthenticated visitor was heading in `?redirect=`
    // (e.g. a guest who opened /checkout). Honour that intent when it exists; otherwise the
    // default landing page is Home.
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
    await router.push(redirect ?? { name: 'home' })
  } catch (error) {
    serverError.value = toErrorMessage(error, 'Invalid email or password.')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="page">
    <div class="wrap">
      <div class="auth-card">
        <p class="kicker">Welcome back</p>
        <h1 class="section-title h2 mb-4">Sign in</h1>

        <LoginForm
          v-model="credentials"
          :submitting="submitting"
          :error="serverError"
          @submit="onSubmit"
        />

        <p class="text-center mt-4 mb-0 small">
          No account?
          <RouterLink class="auth-link" :to="{ name: 'register' }">Create one</RouterLink>
        </p>

        <hr class="my-4" />

        <p class="small text-secondary mb-0">
          Demo accounts — customer:
          <code>customer@shophub.local</code> / <code>Customer@12345</code><br />
          administrator: <code>admin@shophub.local</code> / <code>Admin@12345</code>
        </p>
      </div>
    </div>
  </section>
</template>
