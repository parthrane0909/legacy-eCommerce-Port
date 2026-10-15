import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useUiStore } from '@/stores/ui'

describe('ui store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  it('starts with no toasts and a closed drawer', () => {
    const ui = useUiStore()

    expect(ui.toasts).toHaveLength(0)
    expect(ui.miniCartOpen).toBe(false)
    expect(ui.busy).toBe(false)
  })

  it('adds an info toast', () => {
    const ui = useUiStore()
    ui.notify('Saved')

    expect(ui.toasts).toHaveLength(1)
    expect(ui.toasts[0].tone).toBe('info')
  })

  it('adds success and error toasts', () => {
    const ui = useUiStore()
    ui.notifySuccess('Added')
    ui.notifyError('Failed')

    expect(ui.toasts.map((t) => t.tone)).toEqual(['success', 'error'])
  })

  it('auto-dismisses a toast after its timeout', () => {
    const ui = useUiStore()
    const id = ui.notify('Temporary', 'info', 1000)

    expect(ui.toasts).toHaveLength(1)

    vi.advanceTimersByTime(1001)

    expect(ui.toasts).toHaveLength(0)
    expect(id).toBeGreaterThan(0)
  })

  it('keeps a toast when the timeout is zero', () => {
    const ui = useUiStore()
    ui.notify('Sticky', 'info', 0)

    vi.advanceTimersByTime(10_000)

    expect(ui.toasts).toHaveLength(1)
  })

  it('dismisses a specific toast by id', () => {
    const ui = useUiStore()
    const first = ui.notify('One', 'info', 0)
    ui.notify('Two', 'info', 0)

    ui.dismiss(first)

    expect(ui.toasts).toHaveLength(1)
    expect(ui.toasts[0].message).toBe('Two')
  })

  it('opens, closes and toggles the mini-cart drawer', () => {
    const ui = useUiStore()

    ui.openMiniCart()
    expect(ui.miniCartOpen).toBe(true)

    ui.closeMiniCart()
    expect(ui.miniCartOpen).toBe(false)

    ui.toggleMiniCart()
    expect(ui.miniCartOpen).toBe(true)

    ui.toggleMiniCart()
    expect(ui.miniCartOpen).toBe(false)
  })

  it('tracks a busy counter across nested operations', async () => {
    const ui = useUiStore()

    expect(ui.busy).toBe(false)

    ui.beginTask()
    expect(ui.busy).toBe(true)

    ui.beginTask()
    ui.endTask()
    expect(ui.busy).toBe(true)

    ui.endTask()
    expect(ui.busy).toBe(false)
  })

  it('never lets the busy counter go negative', () => {
    const ui = useUiStore()

    ui.endTask()
    ui.endTask()

    expect(ui.busyCount).toBe(0)
    expect(ui.busy).toBe(false)
  })

  it('wraps an operation and releases the counter even when it throws', async () => {
    const ui = useUiStore()

    await expect(
      ui.withBusy(async () => {
        throw new Error('failed')
      }),
    ).rejects.toThrow('failed')

    expect(ui.busyCount).toBe(0)
  })

  it('returns the operation result from withBusy', async () => {
    const ui = useUiStore()

    await expect(ui.withBusy(async () => 'done')).resolves.toBe('done')
  })
})