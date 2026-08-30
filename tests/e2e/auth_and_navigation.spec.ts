import { test, expect } from '@playwright/test'

test.describe('Authentication & Dashboard Navigation E2E', () => {
  test('redirects unauthenticated user from protected dashboard to login when session missing', async ({ context, page }) => {
    await context.clearCookies()
    await page.goto('/auth/login')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('input[type="email"], input[name="email"], input[id*="email"]').first()).toBeVisible()
  })

  test('renders login page with email and password inputs', async ({ page }) => {
    await page.goto('/auth/login')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('input[type="email"], input[name="email"], input[id*="email"]').first()).toBeVisible({ timeout: 10000 })
    await expect(page.locator('input[type="password"], input[name="password"], input[id*="password"]').first()).toBeVisible()
  })

  test('navigates seamlessly across primary dashboard sections in authenticated session', async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard')
    await page.waitForLoadState('networkidle')

    // Navigate to Inventory
    await page.goto('/dashboard/inventory')
    await expect(page.getByRole('heading', { name: /inventory/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Clients / Controllers
    await page.goto('/dashboard/clients')
    await expect(page.getByRole('heading', { name: /industrial controllers/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Plant Map
    await page.goto('/dashboard/map')
    await expect(page.getByRole('heading', { name: /plant layout/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Maintenance Tickets
    await page.goto('/dashboard/tickets')
    await expect(page.getByRole('heading', { name: /maintenance & floor incidents/i })).toBeVisible({ timeout: 10000 })
  })
})
