import { onBeforeUnmount, onMounted, ref, watch, nextTick } from 'vue'
import type { ComponentPublicInstance } from 'vue'

/**
 * Shared behaviour for the home page's horizontal strips (category tiles and the featured
 * rail).
 *
 * Two movement modes:
 *
 *  - **Continuous** (category tiles). A duplicated track is translated by a linear CSS
 *    animation, so the cards flow steadily rather than jumping a card at a time. The loop
 *    distance is the *measured* width of one pass, including its gaps, published to CSS as a
 *    custom property, so it stays correct when the card basis, the category count or the
 *    viewport changes. The manual arrows seek the same animation timeline instead of writing
 *    `scrollLeft`, so the two never fight over one position.
 *
 *  - **On demand** (featured products). The element scrolls with `scrollBy`; nothing moves
 *    unless asked.
 *
 * In both modes wheel, trackpad and touch panning keep working, and the scrollbar is
 * suppressed by the stylesheet.
 */
export interface HorizontalRailContinuous {
  /**
   * Number of cards in one pass of the loop, as a value or a getter. The caller renders the
   * set twice, so this is the index at which the clone begins. A getter is used because the
   * cards arrive from the API after this composable is created, so the count is 0 up front.
   */
  itemCount: number | (() => number)
  /** Track speed in CSS pixels per second. Modest by design, so a card is readable. */
  pxPerSecond?: number
}

export interface HorizontalRailOptions {
  /** Accessible name of the left button, e.g. "Scroll categories left". */
  leftLabel: string
  /** Accessible name of the right button. */
  rightLabel: string
  /** Matches the card-width media query in main.css, so the step tracks the responsive basis. */
  cardBasis?: number
  /**
   * Enables continuous automatic movement. Omit it for a strip that only moves on demand.
   *
   * The motion itself is a CSS keyframe animation, not a JavaScript interval: an interval
   * would have to be driven from timers and could only ever move in discrete steps, which is
   * exactly the stepping this mode replaces. A `transform` animation on an inner track gives
   * genuinely continuous travel, and because it does not touch `scrollLeft` it also leaves
   * touch panning on the outer container free.
   */
  continuous?: HorizontalRailContinuous
}

const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)'

export function useHorizontalRail(options: HorizontalRailOptions) {
  const viewport = ref<HTMLElement | null>(null)
  const canScrollLeft = ref(false)
  const canScrollRight = ref(false)
  /** True while the continuous track is playing (not suspended by hover or focus). */
  const playing = ref(false)
  /** Measured width of one pass of the track, in pixels. Null until it has been measured. */
  const loopDistance = ref<number | null>(null)
  /** Duration of one loop in milliseconds, derived from the measured distance and the speed. */
  const loopDuration = ref(0)

  let observer: ResizeObserver | null = null
  let attachedTo: HTMLElement | null = null
  let track: HTMLElement | null = null
  let cardStep = 0
  /** Position within the loop applied as a negative `animation-delay`, in milliseconds. */
  let seekOffset = 0

  const continuous = options.continuous

  function prefersReducedMotion(): boolean {
    return (
      typeof window !== 'undefined' &&
      typeof window.matchMedia === 'function' &&
      window.matchMedia(REDUCED_MOTION_QUERY).matches
    )
  }

  function overflows(): boolean {
    const el = viewport.value
    if (!el) return false
    // In continuous mode the track is wider than the window by construction.
    if (track) return track.scrollWidth > el.clientWidth + 1
    return el.scrollWidth > el.clientWidth + 1
  }

  /** Distance between the start of a card and the start of the next one. */
  function step(): number {
    const el = viewport.value
    if (!el) return 0
    const firstCard = (track ?? el).firstElementChild as HTMLElement | null
    const gap = parseFloat(getComputedStyle(track ?? el).columnGap) || 0
    return (firstCard?.getBoundingClientRect().width ?? el.clientWidth * 0.8) + gap
  }

  /**
   * The scroll offset at which the cloned set sits exactly where the real one started, which
   * is where the strip may jump back to zero without anything visibly changing. Returns null
   * when there is no clone to measure.
   */
  function loopPoint(): number | null {
    const el = viewport.value
    if (!el || !continuous) return null
    const count = typeof continuous.itemCount === 'function' ? continuous.itemCount() : continuous.itemCount
    if (count <= 0) return null

    const firstCard = el.firstElementChild as HTMLElement | undefined
    const firstClone = el.children[count] as HTMLElement | undefined
    if (!firstCard || !firstClone) return null

    // `offsetLeft` is relative to the shared offsetParent, not to the strip, so the loop
    // offset is the *difference* between the clone and the first card. Using the clone's
    // raw offsetLeft would overshoot by however far the strip sits from that origin and put a
    // visible jump into the loop.
    const distance = firstClone.offsetLeft - firstCard.offsetLeft
    return distance > 0 ? distance : null
  }

  /**
   * Returns the strip to the start of the loop once it has travelled past one full set.
   * The jump is instant but invisible: the clone is pixel-identical to the real set.
   */
  function normalise(): void {
    const el = viewport.value
    if (!el) return
    const loop = loopPoint()
    if (loop !== null && loop > 0 && el.scrollLeft >= loop) {
      el.scrollTo({ left: 0, behavior: 'auto' })
    }
  }

  function update(): void {
    const el = viewport.value
    if (!el) return

    // A continuous strip loops forever, so both directions always have somewhere to go.
    if (continuous) {
      canScrollLeft.value = overflows()
      canScrollRight.value = overflows()
      return
    }

    // scrollLeft is fractional in some browsers, so allow a pixel of slack before reporting an
    // end state; otherwise the buttons flicker on the last pixel of travel.
    const maxScroll = el.scrollWidth - el.clientWidth
    canScrollLeft.value = el.scrollLeft > 1
    canScrollRight.value = el.scrollLeft < maxScroll - 1
  }

  /**
   * Measures one pass of the track and publishes it to CSS.
   *
   * The measurement is the gap-inclusive distance from the first card to the clone's first
   * card. `getBoundingClientRect` is used rather than a hardcoded figure so the loop follows
   * the card basis, the category count and the viewport; both cards share the same transform,
   * so the difference is unaffected by where the track currently is in its animation.
   */
  function measureTrack(): boolean {
    const el = viewport.value
    if (!el || !continuous) return false

    const found = el.firstElementChild as HTMLElement | null
    if (!found) return false
    track = found

    const count = typeof continuous.itemCount === 'function' ? continuous.itemCount() : continuous.itemCount
    if (count <= 0) return false

    const first = found.children[0] as HTMLElement | undefined
    const clone = found.children[count] as HTMLElement | undefined
    if (!first || !clone) return false

    const distance = clone.getBoundingClientRect().left - first.getBoundingClientRect().left
    if (distance <= 0) return false

    const second = found.children[1] as HTMLElement | undefined
    cardStep = second
      ? second.getBoundingClientRect().left - first.getBoundingClientRect().left
      : distance

    const speed = continuous.pxPerSecond ?? 34
    const duration = Math.round((distance / speed) * 1000)

    loopDistance.value = distance
    loopDuration.value = duration
    // Any offset already applied was expressed against the previous geometry, so it is dropped
    // rather than carried across a re-measure, which would land the track mid-strip.
    if (seekOffset !== 0) {
      seekOffset = 0
      found.style.removeProperty('animation-delay')
    }

    el.style.setProperty('--loop-distance', `${distance}px`)
    el.style.setProperty('--loop-duration', `${duration}ms`)
    return true
  }

  /** The animation that carries the continuous track, when one is running. */
  function trackAnimation(): Animation | null {
    if (!track || typeof track.getAnimations !== 'function') return null
    return track.getAnimations()[0] ?? null
  }

  function setPlaying(next: boolean): void {
    playing.value = next
    track?.classList.toggle('is-paused', !next)
  }

  /**
   * Moves the continuous track by one card by scrubbing the animation's delay, so the manual
   * controls and the automatic flow share one timeline.
   *
   * Two details matter here:
   *
   *  - The play state is left to CSS. Imperatively pausing and resuming an animation detaches
   *    it from `animation-play-state`, after which the hover and focus pause silently stops
   *    working.
   *  - The offset is tracked here rather than read back from the animation. On an *infinite*
   *    CSS animation `Animation.currentTime` is unbounded elapsed time and does not account for
   *    a negative `animation-delay`, so using it as the seek base made a single "left" click
   *    jump several cards. Re-setting the delay restarts the elapsed accounting, which also
   *    makes two clicks move exactly two cards.
   *
   * Falls back to a plain scroll when no animation is present, which is the case under reduced
   * motion.
   */
  function seek(direction: 1 | -1): boolean {
    if (!track || !trackAnimation() || loopDuration.value <= 0 || cardStep <= 0) return false

    const perCard = Math.round((cardStep / (loopDistance.value ?? 1)) * loopDuration.value)
    seekOffset = (seekOffset + direction * perCard + loopDuration.value) % loopDuration.value

    track.style.animationDelay = `-${seekOffset}ms`
    return true
  }

  function scrollBy(direction: 1 | -1): void {
    const el = viewport.value
    if (!el) return

    if (continuous && seek(direction)) return

    el.scrollBy({ left: direction * step(), behavior: 'smooth' })

    // Stepping past the end of one set lands in the clone, which looks identical to the
    // start, so the position is folded back to keep the arrows from dead-ending.
    window.setTimeout(() => {
      normalise()
      update()
    }, 350)
  }

  onMounted(() => {
    window.addEventListener('resize', update)
    attach()
  })

  /**
   * Binds the strip's interaction listeners and starts the continuous track.
   * Idempotent, because the element can be replaced when the section re-renders.
   */
  function attach(): void {
    observe()
    measureTrack()
    applyMotionPreference()
    update()

    const el = viewport.value
    if (!continuous || !el) return
    if (el === attachedTo) return

    detachListeners(attachedTo)
    el.addEventListener('pointerenter', onPointerEnter)
    el.addEventListener('pointerleave', onPointerLeave)
    el.addEventListener('focusin', onFocusIn)
    el.addEventListener('focusout', onFocusOut)
    attachedTo = el
  }

  function detachListeners(el: HTMLElement | null): void {
    if (!el) return
    el.removeEventListener('pointerenter', onPointerEnter)
    el.removeEventListener('pointerleave', onPointerLeave)
    el.removeEventListener('focusin', onFocusIn)
    el.removeEventListener('focusout', onFocusOut)
  }

  /**
   * Reduced motion keeps the strip fully usable but stops it moving on its own: the track is
   * not animated and the arrows fall back to scrolling it.
   */
  function applyMotionPreference(): void {
    const el = viewport.value
    if (!el || !continuous) return

    if (prefersReducedMotion()) {
      el.classList.add('is-manual')
      setPlaying(false)
    } else {
      el.classList.remove('is-manual')
      setPlaying(true)
    }
  }

  /** Hovering pauses movement so a card can be read and clicked. */
  function onPointerEnter(): void {
    setPlaying(false)
  }

  function onPointerLeave(): void {
    applyMotionPreference()
  }

  /** Keyboard focus pauses movement too, for the same reason. */
  function onFocusIn(): void {
    setPlaying(false)
  }

  function onFocusOut(event: FocusEvent): void {
    // Moving focus between cards inside the strip keeps the movement paused.
    const next = event.relatedTarget as Node | null
    if (next && viewport.value?.contains(next)) return
    applyMotionPreference()
  }

  // The strip only exists once its section renders, and its contents arrive asynchronously.
  watch(viewport, async () => {
    await nextTick()
    attach()
  })

  /**
   * Re-measures whenever the strip's box changes. The cards arrive from the API after the
   * element is mounted, and the card basis changes at the responsive breakpoints, so watching
   * the element reference alone would leave a stale loop distance.
   */
  function observe(): void {
    observer?.disconnect()
    const el = viewport.value
    if (!el || typeof ResizeObserver === 'undefined') return

    observer = new ResizeObserver(() => {
      measureTrack()
      update()
    })
    observer.observe(el)

    // The children can change without the strip's own box changing (a scrollWidth-only
    // change), so the children are observed as well.
    for (const child of Array.from(el.children)) observer.observe(child)
  }

  onBeforeUnmount(() => {
    window.removeEventListener('resize', update)
    observer?.disconnect()
    observer = null
    detachListeners(attachedTo)
    attachedTo = null

    const el = viewport.value
    if (el) {
      el.style.removeProperty('--loop-distance')
      el.style.removeProperty('--loop-duration')
      el.classList.remove('is-manual')
    }
    if (track) track.style.removeProperty('animation-delay')
    track = null
    playing.value = false
  })

  /**
   * Callback ref, used as `:ref="rail.setViewport"`.
   *
   * A string ref such as `ref="rail.viewport"` depends on Vue writing through a dotted path
   * into a nested ref, which it does not do reliably here — the element was never recorded and
   * the buttons stayed disabled. An explicit setter removes the ambiguity.
   */
  function setViewport(el: Element | ComponentPublicInstance | null): void {
    viewport.value = (el as HTMLElement | null) ?? null
  }

  return {
    viewport,
    setViewport,
    canScrollLeft,
    canScrollRight,
    /** True while the continuous track is playing. */
    playing,
    /** Measured loop distance in pixels, exposed so it can be asserted in tests. */
    loopDistance,
    /** Measured loop duration in milliseconds. */
    loopDuration,
    /** Accessible names for the two buttons, so callers do not repeat the wording. */
    leftLabel: options.leftLabel,
    rightLabel: options.rightLabel,
    update,
    /** Re-observes, re-measures and re-applies the motion preference. Call after loading. */
    refresh() {
      attach()
    },
    scrollBy,
  }
}
