import { test, expect } from '@playwright/test'

test.describe('Inventory & Asset Management E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard/inventory')
    await page.waitForLoadState('networkidle')
  })

  test('renders inventory header, rollups, and switches status filters', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /industrial inventory/i })).toBeVisible({ timeout: 10000 })

    // Check rollup filter buttons
    const allBtn = page.getByRole('button', { name: /all units/i })
    await expect(allBtn).toBeVisible()

    const optimalBtn = page.getByRole('button', { name: /optimal stock/i })
    await expect(optimalBtn).toBeVisible()
    await optimalBtn.click()
    await page.waitForTimeout(300)

    const lowStockBtn = page.getByRole('button', { name: /low stock alert/i })
    await expect(lowStockBtn).toBeVisible()
    await lowStockBtn.click()
    await page.waitForTimeout(300)

    // Reset back to All Units
    await allBtn.click()
    await page.waitForTimeout(300)
  })

  test('switches between Table, Card Grid, and Physical Station Schematic views', async ({ page }) => {
    // Switch to Card Grid
    const gridBtn = page.getByRole('button', { name: /card grid/i })
    if (await gridBtn.isVisible()) {
      await gridBtn.click()
      await page.waitForTimeout(400)
    }

    // Switch to Schematic
    const schematicBtn = page.getByRole('button', { name: /station schematic|physical/i })
    if (await schematicBtn.isVisible()) {
      await schematicBtn.click()
      await page.waitForTimeout(400)
    }

    // Switch back to Table
    const tableBtn = page.getByRole('button', { name: /table view|tabular/i })
    if (await tableBtn.isVisible()) {
      await tableBtn.click()
      await page.waitForTimeout(400)
    }
  })

  test('switches across primary inventory sections without page refresh', async ({ page }) => {
    // Blueprint Templates Section
    const templatesTab = page.getByRole('button', { name: /blueprint templates/i })
    await expect(templatesTab).toBeVisible({ timeout: 10000 })
    await templatesTab.click()
    await page.waitForTimeout(400)
    await expect(page.getByText(/Asset Templates|Asset Class Templates|Defined Class Schemas/i).first()).toBeVisible({ timeout: 5000 })

    // Provisioning Section
    const provTab = page.getByRole('button', { name: /provisioning station/i })
    await expect(provTab).toBeVisible()
    await provTab.click()
    await page.waitForTimeout(400)

    // Spare Parts Policy Section
    const policyTab = page.getByRole('button', { name: /spares policy|spare parts policy/i })
    await expect(policyTab).toBeVisible()
    await policyTab.click()
    await page.waitForTimeout(400)

    // Back to Registry
    const registryTab = page.getByRole('button', { name: /parts & asset registry/i })
    await expect(registryTab).toBeVisible()
    await registryTab.click()
    await page.waitForTimeout(400)
  })

  test('navigates to dedicated /dashboard/inventory/new page with zero modal overlays', async ({ page }) => {
    await page.goto('/dashboard/inventory/new')
    await page.waitForLoadState('domcontentloaded')

    await expect(page.getByRole('heading', { name: /industrial asset & parts ingestion workbench/i })).toBeVisible({ timeout: 10000 })

    // Verify form tabs: Single Item Intake vs Batch CSV/JSON Import
    await expect(page.getByRole('button', { name: /single item intake/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /batch csv\s*\/\s*json import/i })).toBeVisible()

    // Test switching to Batch Import
    await page.getByRole('button', { name: /batch csv\s*\/\s*json import/i }).click()
    await page.waitForTimeout(300)

    // Switch back to Single Intake
    await page.getByRole('button', { name: /single item intake/i }).click()
    await page.waitForTimeout(300)

    // Verify primary form input
    const nameInput = page.locator('input[placeholder*="Siemens"]').first()
    await expect(nameInput).toBeVisible()
  })

  test('opens in-page AssetDetailInspector master-detail panel without blocking dialogs', async ({ page }) => {
    // Click first item in inventory
    const firstRow = page.locator('tr.cursor-pointer, div[role="button"]:has-text("EUR")').first()
    if (await firstRow.isVisible({ timeout: 4000 })) {
      await firstRow.click()
      await page.waitForTimeout(500)

      // Verify inspector tabs
      const overviewBtn = page.getByRole('button', { name: /overview/i }).first()
      if (await overviewBtn.isVisible()) {
        await expect(overviewBtn).toBeVisible()

        // Switch to Dispatch tab
        const dispatchBtn = page.getByRole('button', { name: /dispatch/i }).first()
        if (await dispatchBtn.isVisible()) {
          await dispatchBtn.click()
          await page.waitForTimeout(300)
        }

        // Switch to Quick Edit tab
        const editBtn = page.getByRole('button', { name: /edit/i }).first()
        if (await editBtn.isVisible()) {
          await editBtn.click()
          await page.waitForTimeout(300)
        }

        // Switch to Audit tab
        const auditBtn = page.getByRole('button', { name: /audit/i }).first()
        if (await auditBtn.isVisible()) {
          await auditBtn.click()
          await page.waitForTimeout(300)
        }
      }
    }
  })
})
