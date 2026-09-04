import { test, expect } from '@playwright/test'

test.describe('Controllers, Telemetry & Plant Map E2E', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
  })

  test('renders Controller Grid and telemetry metrics on clients page', async ({ page }) => {
    await page.goto('/dashboard/clients')
    await page.waitForLoadState('domcontentloaded')

    await expect(page.getByRole('heading', { name: /industrial controllers/i })).toBeVisible({ timeout: 10000 })
    await expect(page.getByRole('button', { name: /grid view/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /plant.*map/i })).toBeVisible()
  })

  test('switches to Plant Map view and verifies DXF SVG canvas controls', async ({ page }) => {
    await page.goto('/dashboard/map')
    await page.waitForLoadState('domcontentloaded')

    await expect(page.getByRole('heading', { name: /plant layout/i })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('button[title="Zoom In"]')).toBeVisible({ timeout: 10000 })
    await expect(page.locator('button[title="Zoom Out"]')).toBeVisible()
    await expect(page.locator('button[title="Reset Bounds"]')).toBeVisible()
  })
})
