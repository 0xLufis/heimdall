import { test, expect } from '@playwright/test'

test.describe('Industrial Data Model UI Validation E2E', () => {
  test('validates /dashboard/machines overview page and quadrant cards', async ({ page }) => {
    await page.goto('/dashboard/machines', { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: /production machinery & lines/i })).toBeVisible({ timeout: 15000 })

    // Verify stations count and live sync
    await expect(page.locator('text=Stations:').first()).toBeVisible()
    await expect(page.locator('text=Lines:').first()).toBeVisible()
    await expect(page.locator('text=Technologies:').first()).toBeVisible()

    // Verify Four Quadrants summary on station cards
    await expect(page.locator('text=Hardware Slots:').first()).toBeVisible()
    await expect(page.locator('text=SW Assets:').first()).toBeVisible()
    await expect(page.locator('text=Inspect Four Quadrants').first()).toBeVisible()
  })

  test('validates nested route /dashboard/machines/L02-OP070 renders Four Quadrants and no NUXT_E4016 error', async ({ page }) => {
    // Listen for console errors
    const consoleErrors: string[] = []
    page.on('console', msg => {
      if (msg.type() === 'error') consoleErrors.push(msg.text())
    })

    await page.goto('/dashboard/machines/L02-OP070', { waitUntil: 'domcontentloaded' })

    // Verify station title or identifier renders
    await expect(page.locator('text=L02-OP070').first()).toBeVisible({ timeout: 15000 })

    // Verify the Four Quadrants tabs
    await expect(page.getByRole('tab', { name: /hardware/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /software/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /licenses/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /docs/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /delegations/i })).toBeVisible()
    await expect(page.getByRole('tab', { name: /journal/i })).toBeVisible()
    await expect(page.locator('text=Operational Component Slots (Hardware Quadrant)')).toBeVisible()

    // Verify no NUXT_E4016 error occurred
    const e4016 = consoleErrors.find(e => e.includes('NUXT_E4016'))
    expect(e4016).toBeUndefined()

    // Test clicking Commission / Assign Part modal
    const assignBtn = page.getByRole('button', { name: /commission component slot|add functional slot/i }).first()
    if (await assignBtn.isVisible()) {
      await assignBtn.click()
      await expect(page.locator('text=Commission Component Slot').first()).toBeVisible()
      await page.keyboard.press('Escape')
    }
  })

  test('validates /dashboard/delegations hub with all 4 tabs and resolver simulator', async ({ page }) => {
    await page.goto('/dashboard/delegations', { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: /personnel delegations, skills/i })).toBeVisible({ timeout: 15000 })

    // 1. Delegation Recipes tab
    await expect(page.locator('text=Laser Safety & Optical Response Recipe').first()).toBeVisible()
    await expect(page.locator('text=Coverage:').first()).toBeVisible()

    // Open Test Dispatch Resolver modal
    await page.getByRole('button', { name: /test dispatch resolver/i }).click()
    await expect(page.locator('text=Delegation Recipe Router Simulator')).toBeVisible()
    await expect(page.getByRole('button', { name: /execute delegation resolution/i })).toBeVisible()
    await page.keyboard.press('Escape')

    // 2. Skills & Certifications tab
    await page.getByRole('button', { name: /skills & certifications/i }).click()
    await expect(page.locator('text=Laser Class 4 Safety Officer Certification').first()).toBeVisible()
    await expect(page.locator('text=CERT-LASER-4').first()).toBeVisible()

    // 3. Personnel Qualifications Roster tab
    await page.getByRole('button', { name: /personnel qualifications/i }).click()
    await expect(page.locator('text=Engineer Orwell').first()).toBeVisible()
    await expect(page.locator('text=Valid').first()).toBeVisible()

    // 4. Shift Crews & Groups tab
    await page.getByRole('button', { name: /shift crews & groups/i }).click()
    await expect(page.locator('text=Plant Controls & Automation Group').first()).toBeVisible()
  })

  test('validates /dashboard/machine-groups with lines, contracts, and dynamic tech groups', async ({ page }) => {
    await page.goto('/dashboard/machine-groups', { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: /machine groupings & production topology/i })).toBeVisible({ timeout: 15000 })

    // 1. Production Lines tab & Customer Contracts
    await expect(page.locator('text=Contract:').first()).toBeVisible()
    await expect(page.locator('text=Target Cycle Time').first()).toBeVisible()
    await expect(page.locator('text=Sequential Station Layout').first()).toBeVisible()

    // 2. Dynamic Technological Groups tab
    await page.getByRole('button', { name: /dynamic technological groups/i }).click()
    await expect(page.locator('text=Tech Key:').first()).toBeVisible()
    await expect(page.locator('text=Required Gating Skill:').first()).toBeVisible()

    // 3. Operational Clusters tab
    await page.getByRole('button', { name: /operational clusters/i }).click()
    await expect(page.locator('text=Operational Cluster').first()).toBeVisible()
  })

  test('validates /dashboard/inventory/:id loads part without 404 error', async ({ page }) => {
    await page.goto('/dashboard/inventory/9f0ef3fa-29a7-522c-9c98-b99948333984', { waitUntil: 'domcontentloaded' })
    
    // Check that part details load and not an unhandled error
    await expect(page.locator('text=STK-FERR-15MM').or(page.locator('text=Phoenix Contact')).or(page.locator('text=Inventory Part')).first()).toBeVisible({ timeout: 15000 })
  })
})
