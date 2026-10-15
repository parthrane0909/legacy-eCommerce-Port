import { test, expect } from '@playwright/test'

/**
 * Guards the OpenAPI document and Swagger UI wiring.
 *
 * Regression test for a real defect: `SwaggerEndpoint` pointed at
 * "/swagger/v1/swagger-ui.html" — the UI's own HTML page rather than a specification —
 * which returned 404. The UI shell loaded, so the failure was silent, but no operations
 * were displayed. This asserts the UI actually renders the operations, because a 200 on
 * /swagger/index.html alone proves nothing.
 *
 * Swagger UI is registered only in the Development environment, so the API must be
 * running with ASPNETCORE_ENVIRONMENT=Development.
 */
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5099'

test.describe('OpenAPI and Swagger UI', () => {
  test('serves the OpenAPI document with every operation', async ({ request }) => {
    const response = await request.get(`${API_URL}/openapi/v1.json`)

    expect(response.status()).toBe(200)
    expect(response.headers()['content-type']).toContain('json')

    const document = await response.json()

    // Title, version and the bearer scheme come from the document transformer.
    expect(document.info.title).toBe('ShopHub API')
    expect(document.info.version).toBe('v1')
    expect(Object.keys(document.components.securitySchemes)).toContain('Bearer')
    expect(document.security).toEqual([{ Bearer: [] }])

    const operations = Object.values(document.paths as Record<string, Record<string, unknown>>)
      .flatMap((path) => Object.keys(path))
      .filter((method) => ['get', 'post', 'put', 'delete', 'patch'].includes(method))

    // Every endpoint group the assignment requires is present.
    expect(operations.length).toBeGreaterThanOrEqual(34)
  })

  test('swagger UI renders the API operations', async ({ page }) => {
    const consoleErrors: string[] = []
    page.on('console', (message) => {
      if (message.type() === 'error') consoleErrors.push(message.text())
    })
    page.on('requestfailed', (request) => {
      consoleErrors.push(`request failed: ${request.url()}`)
    })

    await page.goto(`${API_URL}/swagger`, { waitUntil: 'networkidle' })

    await expect(page.locator('.swagger-ui .info .title')).toContainText('ShopHub API', {
      timeout: 30_000,
    })

    // One block per operation. This is the assertion that failed before the fix.
    const operationBlocks = page.locator('.swagger-ui .opblock')
    await expect(operationBlocks).toHaveCount(await page.locator('.swagger-ui .opblock').count())
    expect(await operationBlocks.count()).toBeGreaterThan(20)

    // Representative operations from each group are rendered.
    for (const operation of ['ListProducts', 'MergeCart', 'AdminDashboard']) {
      await expect(page.locator(`[id^="operations-"][id*="${operation}"]`).first()).toBeVisible()
    }

    // The Authorize affordance exists because the bearer scheme is declared.
    await expect(page.locator('.swagger-ui .auth-wrapper .btn.authorize')).toBeVisible()

    // No failed spec fetch — the symptom of the original misconfiguration.
    expect(consoleErrors).toEqual([])
  })
})
