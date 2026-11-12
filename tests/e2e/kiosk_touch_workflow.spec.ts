import { test, expect } from '@playwright/test'

test.describe('Shopfloor Kiosk Touch Workflow E2E', () => {
  test('renders shopfloor terminal interface with live telemetry and 2-click dispatch', async ({ page }) => {
    // Navigate directly with authorized query token
    await page.goto('/terminal/machine/STATION-OP10-01?token=kiosk-token-op10')
    await page.waitForLoadState('domcontentloaded')

    // Verify Station Terminal Header
    await expect(page.getByText(/OP-10 CNC Machining Cell/i)).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(/Live PLC Telemetry/i)).toBeVisible()

    // Verify Read-Only Telemetry Cards
    await expect(page.getByText(/PLC State/i)).toBeVisible()
    await expect(page.getByText(/Cycle Time/i)).toBeVisible()
    await expect(page.getByText(/Spindle Temp/i)).toBeVisible()
    await expect(page.getByText('Vibration (RMS)').first()).toBeVisible()

    // Verify 2-Click Fault Presets Grid
    await expect(page.locator('button:has-text("E-STOP / Safety Interlock")').first()).toBeVisible()
    await expect(page.locator('button:has-text("Mechanical Jam")').first()).toBeVisible()
    await expect(page.locator('button:has-text("Spindle Overheat")').first()).toBeVisible()

    // Click 1: Select Fault Preset
    const safetyPreset = page.locator('button:has-text("E-STOP / Safety Interlock")').first()
    await safetyPreset.click()
    await page.waitForTimeout(300)

    // Verify Dispatch button updates text to reflect selection
    const dispatchBtn = page.locator('button:has-text("CLICK 2: DISPATCH")')
    await expect(dispatchBtn).toBeVisible()
    await expect(dispatchBtn).toContainText('ERR-SAFETY-01')

    // Click 2: Dispatch Ticket
    await dispatchBtn.click()

    // Verify In-Page Tactile Acknowledgment Banner (Zero Modal Backdrop!)
    const ackBanner = page.locator('text=Ticket Dispatched Successfully').first()
    await expect(ackBanner).toBeVisible({ timeout: 8000 })

    // Verify that NO blocking full-screen modal overlay exists
    const modalBackdrops = page.locator('.fixed.inset-0.bg-slate-950\\/80')
    await expect(modalBackdrops).toHaveCount(0)

    // Verify dismiss button is tactile and clickable
    const dismissBtn = page.locator('button:has-text("Dismiss")').first()
    await expect(dismissBtn).toBeVisible()
    await dismissBtn.click()

    // Banner should close
    await expect(ackBanner).not.toBeVisible({ timeout: 3000 })
  })

  test('supports PIN / token authentication pairing screen when unauthenticated', async ({ page }) => {
    // Open a fresh unauthenticated station with clean storage
    await page.goto('/terminal/machine/LINE1-OP20')
    await page.evaluate(() => localStorage.clear())
    await page.reload()
    await page.waitForLoadState('domcontentloaded')

    // Should prompt for Workstation Terminal Pairing
    await expect(page.getByRole('heading', { name: /workstation terminal pairing/i })).toBeVisible({ timeout: 10000 })
    // No on-screen keypad: the IPC's hardware or OS touch keyboard types into a normal input
    await expect(page.getByRole('button', { name: '1', exact: true })).toHaveCount(0)

    // Fill in pairing token
    const tokenInput = page.locator('input[placeholder*="token"], input[placeholder*="Token"]').first()
    await expect(tokenInput).toBeVisible()
    await tokenInput.fill('kiosk-token-op10')

    // Click Authenticate
    const authBtn = page.getByRole('button', { name: /authenticate & bind/i })
    await authBtn.click()

    // Should bind and load telemetry dashboard
    await expect(page.getByText(/Live PLC Telemetry/i)).toBeVisible({ timeout: 10000 })
  })
})
