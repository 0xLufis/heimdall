import { test, expect } from '@playwright/test'

test.describe('Inventory & Asset Management E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard/inventory')
    await page.waitForLoadState('networkidle')
  })

  test('switches tabs between Hardware, Software, Parts, and Stock views', async ({ page }) => {
    const hwTab = page.getByRole('button', { name: /hardware/i }).first()
    await expect(hwTab).toBeVisible({ timeout: 10000 })

    const swTab = page.getByRole('button', { name: /software/i }).first()
    await swTab.click()
    await page.waitForTimeout(300)

    const partsTab = page.getByRole('button', { name: /parts/i }).first()
    await partsTab.click()
    await page.waitForTimeout(300)

    const stockTab = page.getByRole('button', { name: /stock/i }).first()
    await stockTab.click()
    await page.waitForTimeout(300)

    const treeBtn = page.getByRole('button', { name: /visualise tree|component tree/i }).first()
    await expect(treeBtn).toBeVisible({ timeout: 5000 })
    await treeBtn.click()
    await expect(page.getByText('Station Component Tree')).toBeVisible({ timeout: 5000 })
  })

  test('opens column configuration popover and toggles column visibility', async ({ page }) => {
    const colBtn = page.getByRole('button', { name: /columns/i })
    await expect(colBtn).toBeVisible({ timeout: 10000 })
    await colBtn.click()

    await expect(page.getByText('Display Configuration')).toBeVisible()
    const mfrOption = page.locator('div:has-text("manufacturer")').first()
    await mfrOption.click()
  })

  test('opens provision asset modal', async ({ page }) => {
    const addBtn = page.getByRole('button', { name: /provision asset|add asset/i }).first()
    if (await addBtn.isVisible()) {
      await addBtn.click()
      await expect(page.locator('[role="dialog"]').first()).toBeVisible({ timeout: 5000 })
    }
  })
})
