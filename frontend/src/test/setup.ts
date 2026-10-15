/**
 * Vitest setup. Registers the matchMedia stub jsdom omits and silences expected
 * console noise from failed network assertions in store tests.
 */
import { config } from '@vue/test-utils'
import { vi } from 'vitest'

// jsdom does not implement matchMedia; several Bootstrap components query it.
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }),
  })
}

// Router-link/router-view stubs so components can be mounted in isolation.
config.global.stubs = {
  RouterLink: {
    props: ['to'],
    template: '<a :href="typeof to === \'string\' ? to : \'#\'"><slot /></a>',
  },
  RouterView: true,
}

// Teleport is deliberately NOT stubbed globally: components that use it (such as the cart
// drawer) should render into document.body so their content is visible to assertions.