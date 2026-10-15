import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export type ToastTone = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  message: string
  tone: ToastTone
}

/**
 * Cross-cutting UI state: toasts, the mini-cart drawer and a global busy counter.
 */
export const useUiStore = defineStore('ui', () => {
  const toasts = ref<Toast[]>([])
  const miniCartOpen = ref(false)
  const busyCount = ref(0)

  let nextId = 1

  const busy = computed(() => busyCount.value > 0)

  function notify(message: string, tone: ToastTone = 'info', timeout = 3200): number {
    const id = nextId++
    toasts.value.push({ id, message, tone })
    if (timeout > 0) {
      window.setTimeout(() => dismiss(id), timeout)
    }
    return id
  }

  const notifySuccess = (message: string) => notify(message, 'success')
  const notifyError = (message: string) => notify(message, 'error', 5000)

  function dismiss(id: number): void {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  function openMiniCart(): void {
    miniCartOpen.value = true
  }

  function closeMiniCart(): void {
    miniCartOpen.value = false
  }

  function toggleMiniCart(): void {
    miniCartOpen.value = !miniCartOpen.value
  }

  /** Increment/decrement around an operation that should show a global spinner. */
  function beginTask(): void {
    busyCount.value++
  }

  function endTask(): void {
    busyCount.value = Math.max(0, busyCount.value - 1)
  }

  async function withBusy<T>(operation: () => Promise<T>): Promise<T> {
    beginTask()
    try {
      return await operation()
    } finally {
      endTask()
    }
  }

  return {
    toasts,
    miniCartOpen,
    busy,
    busyCount,
    notify,
    notifySuccess,
    notifyError,
    dismiss,
    openMiniCart,
    closeMiniCart,
    toggleMiniCart,
    beginTask,
    endTask,
    withBusy,
  }
})