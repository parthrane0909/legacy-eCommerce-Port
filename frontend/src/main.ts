import { createApp } from 'vue'
import { createPinia } from 'pinia'

import 'bootstrap/dist/css/bootstrap.min.css'
import '@/assets/styles/main.css'

import App from './App.vue'
import router from './router'
import { registerAuthFailureHandler } from '@/stores/auth'

const app = createApp(App)

app.use(createPinia())
app.use(router)

// When a token refresh fails, the auth store drops to the guest state.
registerAuthFailureHandler()

app.mount('#app')