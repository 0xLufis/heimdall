import { test, expect } from '@playwright/test'

test.describe('Authentication & Dashboard Navigation E2E', () => {
  test('redirects unauthenticated user from protected dashboard to login when session missing', async ({ context, page }) => {
    await context.clearCookies()
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/auth\/login/)
    await expect(page.locator('input[type="email"], input[name="email"], input[id*="email"]').first()).toBeVisible({ timeout: 10000 })
  })

  test('renders login page with email and password inputs', async ({ page }) => {
    await page.goto('/auth/login', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('input[type="email"], input[name="email"], input[id*="email"]').first()).toBeVisible({ timeout: 10000 })
    await expect(page.locator('input[type="password"], input[name="password"], input[id*="password"]').first()).toBeVisible()
  })

  test('navigates seamlessly across primary dashboard sections in authenticated session', async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })

    // Navigate to Inventory
    await page.getByRole('link', { name: 'Inventory', exact: true }).click()
    await page.waitForURL('**/dashboard/inventory')
    await expect(page.getByRole('heading', { name: /inventory/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Clients / Controllers
    await page.getByRole('link', { name: 'Client PCs', exact: true }).click()
    await page.waitForURL('**/dashboard/clients')
    await expect(page.getByRole('heading', { name: /industrial controllers/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Plant Map
    await page.getByRole('link', { name: 'Plant Map', exact: true }).click()
    await page.waitForURL('**/dashboard/map')
    await expect(page.getByRole('heading', { name: /plant layout/i })).toBeVisible({ timeout: 10000 })

    // Navigate to Maintenance Tickets
    await page.getByRole('link', { name: 'Tickets', exact: true }).click()
    await page.waitForURL('**/dashboard/tickets')
    await expect(page.getByRole('heading', { name: /maintenance & floor incidents/i })).toBeVisible({ timeout: 10000 })
  })

  test('retains authentication and does not redirect to login when navigating with real Better-Auth session cookie', async ({ context, page }) => {
    // Clear test bypass cookies so we test ONLY the real Better-Auth session cookie
    await context.clearCookies()
    await context.addCookies([
      {
        name: 'better-auth.session_token',
        value: 'IVIRvvEIj9I6LlGIslm3XfvqjopL5BqN.LkKu%2BWdiGjddYjP5j%2BJX7cUe8heOVCgyFIW8DMKMb3o%3D',
        domain: 'localhost',
        path: '/',
        httpOnly: true
      }
    ])

    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: /welcome back/i })).toBeVisible({ timeout: 10000 })

    // Interact with app: click Client PCs in sidebar
    await page.getByRole('link', { name: 'Client PCs', exact: true }).click()
    await page.waitForURL('**/dashboard/clients', { timeout: 10000 })
    await expect(page.getByRole('heading', { name: /industrial controllers/i })).toBeVisible({ timeout: 10000 })

    // Interact with app: click Tickets in sidebar
    await page.getByRole('link', { name: 'Tickets', exact: true }).click()
    await page.waitForURL('**/dashboard/tickets', { timeout: 10000 })
    await expect(page.getByRole('heading', { name: /maintenance & floor incidents/i })).toBeVisible({ timeout: 10000 })

    // Interact with app: click Inventory in sidebar
    await page.getByRole('link', { name: 'Inventory', exact: true }).click()
    await page.waitForURL('**/dashboard/inventory', { timeout: 10000 })
    await expect(page.getByRole('heading', { name: /inventory/i })).toBeVisible({ timeout: 10000 })

    // Verify user was NOT bounced back to /auth/login
    expect(page.url()).not.toContain('/auth/login')
  })
})
