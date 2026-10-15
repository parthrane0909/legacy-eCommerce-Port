import { describe, it, expect, vi, beforeEach } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useHorizontalRail, type HorizontalRailOptions } from '@/composables/useHorizontalRail'

/**
 * jsdom does not lay out or scroll, so the strip is faked: `scrollWidth` and `clientWidth` are
 * read-only on a real element and are therefore defined explicitly, and `scrollBy` is a spy.
 */
function fakeStrip(options: {
  scrollWidth: number
  clientWidth: number
  cardWidth?: number
  gap?: number
}) {
  const el = document.createElement('div')
  Object.defineProperty(el, 'scrollWidth', { value: options.scrollWidth, configurable: true })
  Object.defineProperty(el, 'clientWidth', { value: options.clientWidth, configurable: true })
  el.scrollLeft = 0
  el.scrollBy = vi.fn()

  if (options.cardWidth !== undefined) {
    Object.defineProperty(el, 'firstElementChild', {
      value: { getBoundingClientRect: () => ({ width: options.cardWidth }) },
      configurable: true,
    })
  }

  if (options.gap !== undefined) {
    vi.spyOn(window, 'getComputedStyle').mockReturnValue({
      columnGap: `${options.gap}px`,
    } as unknown as CSSStyleDeclaration)
  }

  return el
}

/** Runs the composable inside a real component so the lifecycle hooks are exercised. */
function withRail<T>(labels: HorizontalRailOptions, run: (rail: ReturnType<typeof useHorizontalRail>) => T) {
  let rail!: ReturnType<typeof useHorizontalRail>

  const Harness = defineComponent({
    setup() {
      rail = useHorizontalRail(labels)
      return () => h('div')
    },
  })

  mount(Harness)
  return run(rail)
}

describe('useHorizontalRail', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('exposes the accessible labels for the two buttons', () => {
    withRail({ leftLabel: 'Scroll categories left', rightLabel: 'Scroll categories right' }, (rail) => {
      expect(rail.leftLabel).toBe('Scroll categories left')
      expect(rail.rightLabel).toBe('Scroll categories right')
    })
  })

  it('disables the left button at the start and the right button at the end', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      const el = fakeStrip({ scrollWidth: 1400, clientWidth: 1000 })
      rail.viewport.value = el

      el.scrollLeft = 0
      rail.update()
      expect(rail.canScrollLeft.value).toBe(false)
      expect(rail.canScrollRight.value).toBe(true)

      el.scrollLeft = 200
      rail.update()
      expect(rail.canScrollLeft.value).toBe(true)
      expect(rail.canScrollRight.value).toBe(true)

      el.scrollLeft = 400
      rail.update()
      expect(rail.canScrollLeft.value).toBe(true)
      expect(rail.canScrollRight.value).toBe(false)
    })
  })

  it('treats a sub-pixel remainder as the end of the rail', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      const el = fakeStrip({ scrollWidth: 1000, clientWidth: 600 })
      rail.viewport.value = el

      // Browsers report fractional scrollLeft, so a sub-pixel remainder must not leave the
      // button permanently enabled.
      el.scrollLeft = 399.5
      rail.update()

      expect(rail.canScrollRight.value).toBe(false)
    })
  })

  it('reports no scrolling when the content already fits', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      const el = fakeStrip({ scrollWidth: 800, clientWidth: 1000 })
      rail.viewport.value = el
      el.scrollLeft = 0
      rail.update()

      expect(rail.canScrollLeft.value).toBe(false)
      expect(rail.canScrollRight.value).toBe(false)
    })
  })

  it('scrolls by one card plus its gap, in both directions', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      const el = fakeStrip({ scrollWidth: 1400, clientWidth: 1000, cardWidth: 200, gap: 16 })
      rail.viewport.value = el

      rail.scrollBy(1)
      expect(el.scrollBy).toHaveBeenCalledWith({ left: 216, behavior: 'smooth' })

      rail.scrollBy(-1)
      expect(el.scrollBy).toHaveBeenLastCalledWith({ left: -216, behavior: 'smooth' })
    })
  })

  it('falls back to most of the viewport when the strip has no measurable card', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      const el = fakeStrip({ scrollWidth: 1400, clientWidth: 1000, gap: 0 })
      rail.viewport.value = el

      rail.scrollBy(1)
      // 80% of a 1000px viewport.
      expect(el.scrollBy).toHaveBeenCalledWith({ left: 800, behavior: 'smooth' })
    })
  })

  it('does nothing when the strip has not mounted yet', () => {
    withRail({ leftLabel: 'L', rightLabel: 'R' }, (rail) => {
      rail.viewport.value = null

      expect(() => rail.scrollBy(1)).not.toThrow()
      expect(() => rail.update()).not.toThrow()
      expect(rail.canScrollLeft.value).toBe(false)
      expect(rail.canScrollRight.value).toBe(false)
    })
  })
})
