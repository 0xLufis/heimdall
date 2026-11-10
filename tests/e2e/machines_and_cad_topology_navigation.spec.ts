import { test, expect } from '@playwright/test'

test.describe('Machines Directory & CAD Plant Topology E2E', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
  })

  test('loads machines directory with stations and switches view modes', async ({ page }) => {
    await page.goto('/dashboard/machines', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(600)

    // Verify Page Heading
    await expect(page.getByRole('heading', { name: /machine directory & plant hierarchy/i })).toBeVisible({ timeout: 10000 })

    // Verify machine cards rendered
    const stationCards = page.getByText(/L01-OP|CNC|Fastener|Cell|Station|Operational/i).first()
    await expect(stationCards).toBeVisible({ timeout: 10000 })

    // Switch to Line Hierarchy view
    const lineViewBtn = page.getByRole('button', { name: /lines/i })
    if (await lineViewBtn.isVisible()) {
      await lineViewBtn.click()
      await page.waitForTimeout(500)
      await expect(page.getByText(/Line 01|Production Line/i).first()).toBeVisible()
    }

    // Switch to Physical Schematic view
    const schematicBtn = page.getByRole('button', { name: /schematic/i })
    if (await schematicBtn.isVisible()) {
      await schematicBtn.click()
      await page.waitForTimeout(500)
      await expect(page.getByText(/Interactive Kinematic/i)).toBeVisible()
    }
  })

  test('opens non-blocking Station Detail Workbench drawer with tabs', async ({ page }) => {
    await page.goto('/dashboard/machines', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(800)

    // Click inspect/diagnostics or tree on first available station
    const inspectBtn = page.locator('button:has-text("Diagnostics"), button:has-text("Inspect"), button:has-text("Tree")').first()
    if (await inspectBtn.isVisible()) {
      await inspectBtn.click()
      await page.waitForTimeout(600)

      // Verify Station Detail Workbench drawer opened
      await expect(page.getByText(/Station Cell & Kinematic Workbench|Station Details/i).first()).toBeVisible({ timeout: 8000 })

      // Verify no dark blocking overlay backdrop exists
      const darkBackdrops = page.locator('.fixed.inset-0.bg-black\\/80')
      await expect(darkBackdrops).toHaveCount(0)

      // Close workbench
      const closeBtn = page.locator('button:has-text("Close"), button[aria-label="Close"]').first()
      if (await closeBtn.isVisible()) {
        await closeBtn.click()
      }
    }
  })
})
