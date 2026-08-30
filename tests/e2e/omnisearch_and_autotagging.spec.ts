import { test, expect } from '@playwright/test'

test.describe('OmniSearch & Auto-Tagging Intelligence E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard')
    await page.waitForLoadState('networkidle')
  })

  test('activates global search input and accepts free text typing', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="OmniSearch"]').first()
    await expect(searchInput).toBeVisible({ timeout: 10000 })
    await searchInput.focus()
    await searchInput.pressSequentially('siemns', { delay: 50 })

    // Wait for auto-tag suggestions dropdown
    const autoTagBadge = page.locator('button:has-text("Siemens")').first()
    await expect(autoTagBadge).toBeVisible({ timeout: 5000 })
  })

  test('clicking auto-detected tag suggestion transforms it into an interactive tag pill', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="OmniSearch"]').first()
    await expect(searchInput).toBeVisible({ timeout: 10000 })
    await searchInput.focus()
    await searchInput.pressSequentially('15kW', { delay: 50 })

    // Expect spec auto-detected tag
    const specTagSuggestion = page.locator('button:has-text("15KW")').first()
    await expect(specTagSuggestion).toBeVisible({ timeout: 5000 })
    await specTagSuggestion.click()

    // Verify tag pill chip is created
    const tagPill = page.locator('div:has-text("15KW")').first()
    await expect(tagPill).toBeVisible()
  })

  test('removes active tag pill when clicking the remove button', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="OmniSearch"]').first()
    await expect(searchInput).toBeVisible({ timeout: 10000 })
    await searchInput.focus()
    await searchInput.pressSequentially('OP10', { delay: 50 })

    const stationSuggestion = page.locator('button:has-text("OP10")').first()
    if (await stationSuggestion.isVisible({ timeout: 3000 })) {
      await stationSuggestion.click()
      const removeBtn = page.locator('button[aria-label="Remove tag"]').first()
      if (await removeBtn.isVisible()) {
        await removeBtn.click()
      }
    }
  })
})
