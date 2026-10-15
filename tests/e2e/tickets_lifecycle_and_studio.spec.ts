import { test, expect } from '@playwright/test'

test.describe('Tickets Lifecycle & Incident Studio E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard/tickets')
    await page.waitForLoadState('networkidle')
  })

  test('renders tickets management dashboard with rollups and filter actions', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /maintenance & floor incidents/i })).toBeVisible({ timeout: 10000 })

    // Verify primary action buttons
    const newIncidentBtn = page.getByRole('link', { name: /report incident|new ticket/i }).first()
    await expect(newIncidentBtn).toBeVisible()

    // Verify quick navigation to full pages (templates, delegations, machine groups)
    const templatesLink = page.getByRole('link', { name: /templates/i }).first()
    await expect(templatesLink).toBeVisible()

    // Test filter pills (e.g. All, Open, Critical)
    const allFilter = page.getByRole('button', { name: /all tickets|all/i }).first()
    if (await allFilter.isVisible()) {
      await allFilter.click()
      await page.waitForTimeout(300)
    }
  })

  test('navigates to /dashboard/tickets/new dedicated studio and dispatches incident', async ({ page }) => {
    await page.goto('/dashboard/tickets/new')
    await page.waitForLoadState('networkidle')

    // Verify Page Header and Breadcrumbs
    await expect(page.getByRole('heading', { name: /report maintenance incident/i })).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(/Report New Incident/i)).toBeVisible()

    // Priority Selection - Select Critical
    const criticalBtn = page.getByRole('button', { name: /critical/i }).first()
    if (await criticalBtn.isVisible()) {
      await criticalBtn.click()
      await page.waitForTimeout(200)
    }

    // Toggle Line-Stop
    const lineStopToggle = page.locator('button[role="switch"], input[type="checkbox"]').first()
    if (await lineStopToggle.isVisible()) {
      await lineStopToggle.click()
      await page.waitForTimeout(200)
    }

    // Fill Title and Description using matching placeholders
    const titleInput = page.locator('input[placeholder*="Spindle"], input[placeholder*="title"], input[id*="title"]').first()
    await expect(titleInput).toBeVisible({ timeout: 5000 })
    await titleInput.fill('E2E Automated Spindle Bearing Anomaly')

    const descInput = page.locator('textarea').first()
    await expect(descInput).toBeVisible({ timeout: 5000 })
    await descInput.fill('Vibration telemetry breached 8.5 mm/s limit. Urgent inspection required.')

    // Submit Dispatch Button
    const submitBtn = page.getByRole('button', { name: /dispatch incident ticket/i })
    await expect(submitBtn).toBeVisible()
    await submitBtn.click()

    // Verify Toast and navigation back to tickets
    await expect(page.getByText(/incident reported|ticket dispatched|successfully/i).first()).toBeVisible({ timeout: 10000 })
  })

  test('navigates to dedicated /dashboard/tickets/[id] resolution page with zero modal dialogs', async ({ page }) => {
    // Navigate to ticket tkt-005 (Line-stop ticket)
    await page.goto('/dashboard/tickets/tkt-005')
    await page.waitForLoadState('networkidle')

    // Verify Ticket Title and Breadcrumb
    await expect(page.getByText(/TKT-20260928-0005/i).first()).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(/Line 05 – Powertrain Sub-Assembly/i).first()).toBeVisible()

    // Verify Line-Stop Alarm Banner
    await expect(page.getByText(/line-stop alarm active|production halted/i).first()).toBeVisible()

    // Verify Resolution / Status transition actions exist
    const actionButtons = page.locator('button:has-text("Investigation"), button:has-text("Resume"), button:has-text("Hold"), button:has-text("Resolve"), button:has-text("Escalate")')
    await expect(actionButtons.first()).toBeVisible()

    // Verify Zero blocking backdrop overlays
    const modalBackdrops = page.locator('.fixed.inset-0.bg-background\\/80, .fixed.inset-0.bg-black\\/80')
    await expect(modalBackdrops).toHaveCount(0)
  })

  test('opens in-page split inspector workbench on /dashboard/tickets without blocking modal backdrops', async ({ page }) => {
    // Enable Split View mode toggle
    const splitBtn = page.getByRole('button', { name: /split/i })
    await expect(splitBtn).toBeVisible({ timeout: 10000 })
    await splitBtn.click()
    await page.waitForTimeout(300)

    // Click on ticket details button
    const detailsBtn = page.getByRole('button', { name: /view ticket details/i }).first()
    await expect(detailsBtn).toBeVisible({ timeout: 5000 })
    await detailsBtn.click()
    await page.waitForTimeout(400)

    // Verify Split Workbench or Inspector is mounted
    await expect(page.getByText(/TKT-[0-9A-Z]{6,}/i).first()).toBeVisible()

    // Verify page background is NOT obscured by modal backdrop
    const modalBackdrops = page.locator('.fixed.inset-0.bg-background\\/80')
    await expect(modalBackdrops).toHaveCount(0)
  })

  test('navigates to /dashboard/tickets/templates full page and opens drawer workbench', async ({ page }) => {
    await page.goto('/dashboard/tickets/templates')
    await page.waitForLoadState('networkidle')

    // Verify Template Manager Header
    await expect(page.getByRole('heading', { name: /ticket templates catalog|ticket templates/i })).toBeVisible({ timeout: 10000 })

    // Click "New Template" button
    const newTemplateBtn = page.getByRole('button', { name: /new template/i })
    await expect(newTemplateBtn).toBeVisible()
    await newTemplateBtn.click()
    await page.waitForTimeout(400)

    // Verify drawer opened with form fields
    await expect(page.getByText(/create 4-tier ticket template/i)).toBeVisible({ timeout: 5000 })
    await expect(page.getByRole('button', { name: /cancel/i })).toBeVisible()

    // Verify no blocking backdrop overlay
    const modalBackdrop = page.locator('.fixed.inset-0.bg-background\\/80')
    await expect(modalBackdrop).toHaveCount(0)

    // Close drawer
    await page.getByRole('button', { name: /cancel/i }).click()
    await page.waitForTimeout(300)
  })
})
