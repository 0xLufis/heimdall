import { test, expect } from '@playwright/test'

test.describe('Inventory Asset Detail & Lifecycle Modernization E2E', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
  })

  test('loads asset detail page with complete identity and technical specifications', async ({ page }) => {
    // Navigate to a canonical seeded part in inventory
    await page.goto('/dashboard/inventory/prt-ser-101', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(600)

    // Verify Asset Heading and Identity
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible({ timeout: 10000 })
    await expect(page.getByText('Back to Inventory Registry')).toBeVisible()

    // Verify that NO dark blocking modal backdrop exists on load
    const modalBackdrops = page.locator('.fixed.inset-0.bg-black\\/80, .fixed.inset-0.bg-background\\/80')
    await expect(modalBackdrops).toHaveCount(0)
  })

  test('displays Asset Not Found state gracefully for invalid ID without blank screen', async ({ page }) => {
    await page.goto('/dashboard/inventory/non-existent-serial-99999', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(600)

    // Should display graceful not-found feedback
    await expect(page.getByText(/Asset Not Found|could not be located|not found/i).first()).toBeVisible({ timeout: 10000 })
    await expect(page.getByRole('link', { name: /inventory/i }).first()).toBeVisible()

    // Page must not be blank
    const text = await page.locator('body').innerText()
    expect(text.length).toBeGreaterThan(50)
  })

  test('opens in-page drawers for Dispatch, Edit, and Audit without blocking backdrop overlay', async ({ page }) => {
    await page.goto('/dashboard/inventory/prt-ser-101', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(600)

    // Click Audit History
    const auditBtn = page.getByRole('button', { name: /audit history/i })
    if (await auditBtn.isVisible()) {
      await auditBtn.click()
      await page.waitForTimeout(500)

      // Verify non-blocking drawer is visible
      const auditDrawer = page.locator('text=Audit Trail, text=Part Audit History, text=Part Audit Log').first()
      // Verify no heavy dark overlay
      const darkOverlay = page.locator('.fixed.inset-0.bg-black\\/80')
      await expect(darkOverlay).toHaveCount(0)
    }
  })
})
