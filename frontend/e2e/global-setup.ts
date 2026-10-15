/**
 * Playwright global setup.
 *
 * Waits for the .NET 10 API to report healthy before any journey runs.
 *
 * Why this is needed: the suite's `webServer` entry only covers the Vite dev server. The
 * API is started separately (`backend/scripts/run-api-e2e.sh`), and on a cold start it still
 * has to apply migrations, seed the catalogue and JIT-warm before it can answer. Without this
 * gate, the first test to write hits a connection that is not ready yet and burns a 60 s
 * timeout — the suite took 1m18s and reported one spurious failure on a cold API, versus
 * ~13 s and 17/17 once warm.
 *
 * This does not relax any assertion; it only makes start-up deterministic.
 */

const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5099'
const TIMEOUT_MS = Number(process.env.E2E_API_READY_TIMEOUT_MS ?? 120_000)
const INTERVAL_MS = 500

export default async function globalSetup(): Promise<void> {
  const deadline = Date.now() + TIMEOUT_MS
  let lastError = 'no attempt made'

  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${API_URL}/health`, {
        signal: AbortSignal.timeout(5_000),
      })

      if (response.ok) {
        const body = (await response.json()) as { status?: string }
        if (body.status === 'Healthy') {
          console.log(`API ready at ${API_URL}`)
          return
        }

        lastError = `health reported "${body.status}"`
      } else {
        lastError = `health returned HTTP ${response.status}`
      }
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error)
    }

    await new Promise((resolve) => setTimeout(resolve, INTERVAL_MS))
  }

  throw new Error(
    `The API at ${API_URL} did not become healthy within ${TIMEOUT_MS} ms. ` +
      `Start it with "backend/scripts/run-api-e2e.sh" first. Last error: ${lastError}`,
  )
}
