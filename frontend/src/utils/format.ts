/** Currency and number formatting shared across the storefront. */

/** The storefront is priced in INR, matching the legacy catalogue. */
export function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: value % 1 === 0 ? 0 : 2,
  }).format(value)
}

/** Trims trailing zeros: 4999 -> "4,999", 49.5 -> "49.50". */
export function formatNumber(value: number): string {
  return new Intl.NumberFormat('en-IN', {
    minimumFractionDigits: value % 1 === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(value)
}

/** Formats a rating for display: 4.5 -> "4.5". */
export function formatRating(value: number | null | undefined): string | null {
  return value === null || value === undefined ? null : value.toFixed(1)
}

/** Formats an ISO timestamp as a short date: "2026-10-14" -> "14 Oct 2026". */
export function formatDate(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(date)
}