import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import NotificationToast from '@/components/common/NotificationToast.vue'
import { useUiStore } from '@/stores/ui'

describe('add-to-cart notification', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('announces the product name on success', () => {
    const ui = useUiStore()
    ui.notifySuccess('Wireless Headphones Pro added to cart')

    const wrapper = mount(NotificationToast)

    expect(wrapper.text()).toContain('Wireless Headphones Pro added to cart')
    expect(wrapper.find('.toast.success').exists()).toBe(true)
  })

  it('is exposed as a polite live region with status semantics', () => {
    const ui = useUiStore()
    ui.notifySuccess('Added to cart')

    const wrapper = mount(NotificationToast)

    expect(wrapper.find('[aria-live="polite"]').exists()).toBe(true)
    expect(wrapper.find('.toast').attributes('role')).toBe('status')
  })

  it('dismisses itself automatically', async () => {
    const ui = useUiStore()
    ui.notifySuccess('Added to cart')

    const wrapper = mount(NotificationToast)
    expect(wrapper.findAll('.toast')).toHaveLength(1)

    // notifySuccess uses the default 3200ms timeout.
    vi.advanceTimersByTime(3300)
    await nextTick()
    expect(ui.toasts).toHaveLength(0)
    expect(wrapper.findAll('.toast')).toHaveLength(0)
  })

  it('keeps rapid additions separate, each with its own dismissal', async () => {
    const ui = useUiStore()
    ui.notifySuccess('First added to cart')

    // A second product is added a second later, as a shopper double-shopping would.
    vi.advanceTimersByTime(1000)
    ui.notifySuccess('Second added to cart')

    const wrapper = mount(NotificationToast)
    expect(wrapper.findAll('.toast')).toHaveLength(2)

    // The first reaches its 3200ms lifetime and goes; the second is still on screen.
    vi.advanceTimersByTime(2300)
    await nextTick()
    expect(wrapper.findAll('.toast')).toHaveLength(1)
    expect(wrapper.text()).toContain('Second added to cart')

    // And it clears on its own afterwards, so nothing is left behind.
    vi.advanceTimersByTime(1000)
    await nextTick()
    expect(wrapper.findAll('.toast')).toHaveLength(0)
  })

  it('reports a failure with the error tone rather than success', () => {
    const ui = useUiStore()
    ui.notifyError('Not enough stock')

    const wrapper = mount(NotificationToast)

    expect(wrapper.find('.toast.error').exists()).toBe(true)
    expect(wrapper.find('.toast.success').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('added to cart')
  })
})
