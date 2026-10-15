import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { useHorizontalRail, type HorizontalRailOptions } from '@/composables/useHorizontalRail'

/**
 * Continuous-movement behaviour.
 *
 * jsdom does not lay out or animate, so the track is faked: the children carry the geometry
 * the composable measures, `getAnimations` is stubbed to stand in for the CSS animation, and
 * `matchMedia` decides the reduced-motion branch.
 */
const CARD = 200
const GAP = 16
const STEP = CARD + GAP
const SETS = 4
const SET_WIDTH = STEP * SETS
const SPEED = 100 // px/s, chosen so the derived duration is a round 80s
const DURATION = (SET_WIDTH / SPEED) * 1000

/**
 * A stand-in for the running CSS animation. The real one is driven by the compositor; the
 * composable only ever reads and seeks `currentTime`, so a plain object is enough.
 */
class FakeAnimation {
  currentTime = 0
  playState: AnimationPlayState = 'running'
  cancelled = false
  play = vi.fn(() => {
    this.playState = 'running'
  })
  pause = vi.fn(() => {
    this.playState = 'paused'
  })
  cancel = vi.fn(() => {
    this.cancelled = true
  })
}

function fakeTrack() {
  const viewport = document.createElement('div')
  viewport.getBoundingClientRect = () => ({ width: 600, left: 0 }) as DOMRect
  Object.defineProperty(viewport, 'clientWidth', { value: 600, configurable: true })

  const track = document.createElement('div')
  const card = (index: number) => {
    const el = document.createElement('button')
    // Cards sit at index * STEP along the track, so the distance from card 0 to the clone is
    // exactly one pass, gaps included.
    el.getBoundingClientRect = () => ({ width: CARD, left: index * STEP }) as DOMRect
    Object.defineProperty(el, 'offsetLeft', { value: 76 + index * STEP, configurable: true })
    return el
  }
  for (let i = 0; i < SETS * 2; i++) track.appendChild(card(i))
  viewport.appendChild(track)

  const animation = new FakeAnimation()
  // With no animation applied the browser reports none, which is what the reduced-motion
  // branch relies on when it falls back to scrolling.
  track.getAnimations = vi.fn(() =>
    viewport.classList.contains('is-manual') ? [] : [animation],
  ) as unknown as HTMLElement['getAnimations']

  Object.defineProperty(track, 'scrollWidth', { value: SET_WIDTH * 2, configurable: true })
  Object.defineProperty(viewport, 'scrollWidth', { value: SET_WIDTH * 2, configurable: true })
  viewport.scrollBy = vi.fn()
  viewport.scrollTo = vi.fn()

  vi.spyOn(window, 'getComputedStyle').mockReturnValue({
    columnGap: `${GAP}px`,
  } as unknown as CSSStyleDeclaration)

  return { viewport, track, animation }
}

function mountRail(options: HorizontalRailOptions) {
  let rail!: ReturnType<typeof useHorizontalRail>

  const Harness = defineComponent({
    setup() {
      rail = useHorizontalRail(options)
      return () => h('div')
    },
  })

  const wrapper = mount(Harness)
  return { wrapper, rail }
}

function setReducedMotion(matches: boolean) {
  vi.spyOn(window, 'matchMedia').mockReturnValue({ matches } as MediaQueryList)
}

describe('useHorizontalRail — continuous movement', () => {
  beforeEach(() => {
    setReducedMotion(false)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('publishes the measured loop distance rather than a hardcoded figure', () => {
    const { viewport, track } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    // One pass is four cards plus the gaps between them.
    expect(rail.loopDistance.value).toBe(SET_WIDTH)
    expect(viewport.style.getPropertyValue('--loop-distance')).toBe(`${SET_WIDTH}px`)
    expect(viewport.style.getPropertyValue('--loop-duration')).toBe(`${DURATION}ms`)
    expect(track.getAnimations).toBeDefined()
  })

  it('recomputes the loop when the card basis or viewport changes', () => {
    const { viewport, track } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()
    expect(rail.loopDistance.value).toBe(SET_WIDTH)

    // A narrower card basis shortens one pass, so the loop must follow it.
    const resized = 120
    for (let i = 0; i < track.children.length; i++) {
      const el = track.children[i] as HTMLElement
      el.getBoundingClientRect = () => ({ width: resized, left: i * (resized + GAP) }) as DOMRect
    }
    rail.refresh()

    const expected = (resized + GAP) * SETS
    expect(rail.loopDistance.value).toBe(expected)
    expect(viewport.style.getPropertyValue('--loop-distance')).toBe(`${expected}px`)
  })

  it('has both navigation controls available because the track loops', () => {
    const { viewport } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    // The strip never reaches a dead end, so neither arrow is disabled.
    expect(rail.canScrollLeft.value).toBe(true)
    expect(rail.canScrollRight.value).toBe(true)
  })

  it('moves the arrows by seeking the animation, not by scrolling', () => {
    const { viewport, track, animation } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    rail.scrollBy(1)

    // One card out of four cards makes up a quarter of the loop, applied as a delay so the
    // play state stays owned by the stylesheet.
    expect(track.style.animationDelay).toBe(`-${DURATION / 4}ms`)
    // The animation is never paused imperatively, which would detach it from
    // `animation-play-state` and break the hover and focus pause.
    expect(animation.pause).not.toHaveBeenCalled()
    expect(animation.play).not.toHaveBeenCalled()
    // The container is never scrolled, so nothing fights the animation.
    expect(viewport.scrollBy).not.toHaveBeenCalled()

    // A second click moves exactly one more card, whatever the animation's own clock says.
    animation.currentTime = 987_654
    rail.scrollBy(1)
    expect(track.style.animationDelay).toBe(`-${DURATION / 2}ms`)
  })

  it('seeks backwards and wraps within the loop', () => {
    const { viewport, track } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    // Seeking back from the start wraps to the end of the previous loop rather than producing
    // a negative offset, which would restart the animation from zero.
    rail.scrollBy(-1)
    expect(track.style.animationDelay).toBe(`-${DURATION - DURATION / 4}ms`)

    // Coming back a quarter of a loop lands exactly on the start of the loop, which is the
    // seamless boundary the duplicated pass exists for. A zero offset normalises to no delay.
    rail.scrollBy(1)
    expect(track.style.animationDelay).toBe('0ms')

    // Repeated clicks stay inside one loop and never accumulate drift: four cards make one
    // full circuit, so the offset returns exactly to where it started.
    for (let i = 0; i < 4; i++) rail.scrollBy(-1)
    expect(track.style.animationDelay).toBe('0ms')
  })

  it('pauses while hovered and resumes on leave', () => {
    const { viewport, track } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    expect(rail.playing.value).toBe(true)
    expect(track.classList.contains('is-paused')).toBe(false)

    viewport.dispatchEvent(new Event('pointerenter'))
    expect(rail.playing.value).toBe(false)
    expect(track.classList.contains('is-paused')).toBe(true)

    viewport.dispatchEvent(new Event('pointerleave'))
    expect(rail.playing.value).toBe(true)
    expect(track.classList.contains('is-paused')).toBe(false)
  })

  it('pauses while a card has keyboard focus', () => {
    const { viewport, track } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    viewport.dispatchEvent(new Event('focusin'))
    expect(rail.playing.value).toBe(false)
    expect(track.classList.contains('is-paused')).toBe(true)

    // Focus moving between cards inside the strip keeps it paused.
    const inner = document.createElement('button')
    viewport.appendChild(inner)
    viewport.dispatchEvent(new FocusEvent('focusout', { relatedTarget: inner }))
    expect(rail.playing.value).toBe(false)

    viewport.dispatchEvent(new FocusEvent('focusout', { relatedTarget: document.body }))
    expect(rail.playing.value).toBe(true)
  })

  it('disables automatic movement under reduced motion but keeps the arrows working', () => {
    setReducedMotion(true)
    const { viewport, track, animation } = fakeTrack()
    const { rail } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    // Not playing, and the container is switched to manual scrolling.
    expect(rail.playing.value).toBe(false)
    expect(track.classList.contains('is-paused')).toBe(true)
    expect(viewport.classList.contains('is-manual')).toBe(true)

    // The geometry is still measured, so the arrows can scroll the strip by hand.
    expect(rail.loopDistance.value).toBe(SET_WIDTH)

    // No animation is running, so the arrow falls back to scrolling rather than seeking.
    animation.currentTime = 0
    rail.scrollBy(1)
    expect(viewport.scrollBy).toHaveBeenCalled()
  })

  it('cleans up listeners and published state on unmount', () => {
    const { viewport, track } = fakeTrack()
    const { rail, wrapper } = mountRail({
      leftLabel: 'L',
      rightLabel: 'R',
      continuous: { itemCount: SETS, pxPerSecond: SPEED },
    })
    rail.viewport.value = viewport
    rail.refresh()

    rail.scrollBy(1)
    expect(track.style.animationDelay).not.toBe('')

    const removeSpy = vi.spyOn(viewport, 'removeEventListener')
    wrapper.unmount()

    expect(removeSpy).toHaveBeenCalledWith('pointerenter', expect.any(Function))
    expect(removeSpy).toHaveBeenCalledWith('focusin', expect.any(Function))
    // No timer or observer survives, and the published geometry is withdrawn.
    expect(viewport.style.getPropertyValue('--loop-distance')).toBe('')
    expect(track.style.animationDelay).toBe('')
    expect(rail.playing.value).toBe(false)
  })

  it('leaves a strip without continuous movement untouched', () => {
    const { viewport } = fakeTrack()
    const { rail } = mountRail({ leftLabel: 'L', rightLabel: 'R' })
    rail.viewport.value = viewport
    rail.refresh()

    expect(rail.playing.value).toBe(false)
    expect(rail.loopDistance.value).toBeNull()
    expect(viewport.style.getPropertyValue('--loop-distance')).toBe('')
  })
})
