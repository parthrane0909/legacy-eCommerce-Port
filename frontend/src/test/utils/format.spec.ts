import { describe, it, expect, vi } from 'vitest'
import { formatCurrency, formatDate, formatNumber, formatRating } from '@/utils/format'

describe('format helpers', () => {
  it('formats currency in INR', () => {
    // Intl renders the rupee sign with the Indian digit grouping.
    expect(formatCurrency(4999)).toContain('4,999')
    expect(formatCurrency(4999)).toMatch(/₹|Rs/)
  })

  it('drops decimals for whole amounts', () => {
    expect(formatCurrency(100)).not.toContain('.00')
  })

  it('keeps two decimals when there is a fractional part', () => {
    expect(formatCurrency(49.5)).toContain('49.50')
  })

  it('handles zero', () => {
    expect(formatCurrency(0)).toMatch(/0/)
  })

  it('formats plain numbers', () => {
    expect(formatNumber(1234)).toBe('1,234')
    expect(formatNumber(12.5)).toBe('12.50')
  })

  it('formats a rating to one decimal place', () => {
    expect(formatRating(4.5)).toBe('4.5')
    expect(formatRating(5)).toBe('5.0')
  })

  it('returns null for a missing rating', () => {
    expect(formatRating(null)).toBeNull()
    expect(formatRating(undefined)).toBeNull()
  })

  it('formats an ISO date for display', () => {
    expect(formatDate('2026-10-14T10:00:00Z')).toMatch(/2026/)
    expect(formatDate('2026-10-14T10:00:00Z')).toMatch(/Oct/)
  })

  it('passes through an unparseable date rather than showing "Invalid Date"', () => {
    expect(formatDate('not-a-date')).toBe('not-a-date')
  })
})

describe('guest cart id helpers', () => {
  it('creates and reads an id', async () => {
    const { newGuestCartId, readGuestCartId, clearGuestCartId } = await import('@/utils/guestCart')

    window.localStorage.clear()
    const id = newGuestCartId()

    expect(id).toBeTruthy()
    expect(readGuestCartId()).toBe(id)

    clearGuestCartId()
    expect(readGuestCartId()).toBeNull()
  })

  it('produces distinct ids', async () => {
    const { newGuestCartId } = await import('@/utils/guestCart')

    window.localStorage.clear()
    const ids = Array.from({ length: 20 }, () => newGuestCartId())

    expect(new Set(ids).size).toBe(20)
  })

  it('survives storage being unavailable', async () => {
    const { writeGuestCartId } = await import('@/utils/guestCart')

    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(() => writeGuestCartId('x')).not.toThrow()
    vi.restoreAllMocks()
  })
})