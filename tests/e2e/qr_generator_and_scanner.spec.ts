import { test, expect } from '@playwright/test'

test.describe('ISO/IEC 18004 QR Generator, In-App Camera Scanner & Mobile Landing E2E', () => {
  test.beforeEach(async ({ context }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    try {
      await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    } catch {}
  })

  test('generates camera-ready ISO/IEC 18004 QR codes in non-blocking slide-out drawer with LAN host override', async ({ page }) => {
    await page.goto('/dashboard/tickets')
    await page.waitForLoadState('networkidle')

    // Open Settings & Views menu
    const settingsBtn = page.getByRole('button', { name: /settings & views/i })
    await expect(settingsBtn).toBeVisible({ timeout: 10000 })
    await settingsBtn.click()

    // Click Generate Action QR
    const generateQrMenuItem = page.getByRole('menuitem', { name: /generate action qr/i })
    await expect(generateQrMenuItem).toBeVisible()
    await generateQrMenuItem.click()

    // Verify slide-out drawer opened
    const drawer = page.locator('[role="dialog"][aria-label*="QR"], [role="dialog"]').filter({ hasText: /Action QR/i }).first()
    await expect(drawer).toBeVisible({ timeout: 5000 })

    // Verify ZERO blocking backdrop architecture
    const blockingBackdrops = page.locator('.fixed.inset-0.bg-background\\/80, .fixed.inset-0.bg-black\\/80')
    await expect(blockingBackdrops).toHaveCount(0)

    // Verify QR Code image is generated and has high-contrast data URL
    const qrImg = drawer.locator('img[alt="Action QR Code"]')
    await expect(qrImg).toBeVisible()
    const imgSrc = await qrImg.getAttribute('src')
    expect(imgSrc).toMatch(/^data:image\/(png|svg\+xml)/)

    // Verify camera-ready badge
    await expect(drawer.getByText(/Camera-Ready ISO\/IEC 18004/i)).toBeVisible()

    // Switch protocol to Heimdall App (heimdall://)
    const appProtocolBtn = drawer.getByRole('button', { name: /heimdall app/i })
    await appProtocolBtn.click()

    // Verify resolved URI contains heimdall:// scheme
    const uriInput = drawer.locator('input[readonly]')
    await expect(uriInput).toHaveValue(/^heimdall:\/\//)

    // Switch back to Web Universal
    const webProtocolBtn = drawer.getByRole('button', { name: /web universal/i })
    await webProtocolBtn.click()
    await expect(uriInput).toHaveValue(/^http/)

    // Toggle Custom LAN IP / Domain for phone cameras on factory Wi-Fi
    const customHostBtn = drawer.getByRole('button', { name: /custom lan ip/i })
    await customHostBtn.click()

    const customHostInput = drawer.locator('input[placeholder*="192.168"]')
    await expect(customHostInput).toBeVisible()
    await customHostInput.fill('http://192.168.1.150:3000')

    // Verify URI input reflects custom LAN host
    await expect(uriInput).toHaveValue(/^http:\/\/192\.168\.1\.150:3000/)

    // Test Copy button
    const copyBtn = drawer.getByRole('button', { name: /copy/i })
    await expect(copyBtn).toBeVisible()
    await copyBtn.click()
    await expect(drawer.getByText(/copied!/i)).toBeVisible()

    // Close drawer via X button
    const closeBtn = drawer.locator('button').filter({ has: page.locator('svg.lucide-x') }).first()
    await closeBtn.click()
    await expect(drawer).not.toBeVisible()
  })

  test('renders in-app optical QR scanner with camera viewport and simulated feed fallback', async ({ page }) => {
    await page.goto('/dashboard/tickets')
    await page.waitForLoadState('networkidle')

    // Click Scan QR button
    const scanQrBtn = page.getByRole('button', { name: /scan qr/i })
    await expect(scanQrBtn).toBeVisible({ timeout: 10000 })
    await scanQrBtn.click()

    // Verify dynamic in-page scanner rendered without blocking modal overlays
    const blockingBackdrops = page.locator('.fixed.inset-0.bg-background\\/80, .fixed.inset-0.bg-black\\/80')
    await expect(blockingBackdrops).toHaveCount(0)

    // Verify camera video element is attached and configured for auto playback
    const video = page.locator('video')
    await expect(video).toBeAttached()
    await expect(video).toHaveAttribute('autoplay', '')
    await expect(video).toHaveAttribute('playsinline', '')

    // Check for simulated vision feed button or toggle
    const simulateBtn = page.getByRole('button', { name: /simulate vision feed|simulate feed/i }).first()
    if (await simulateBtn.isVisible()) {
      await simulateBtn.click()
      // Verify simulated canvas is now active
      const canvas = page.locator('canvas')
      await expect(canvas).toBeVisible()
      await expect(page.getByText(/simulated vision feed/i)).toBeVisible()
    }

    // Verify quick test target barcodes are available for factory testing
    const testBarcodeBtn = page.getByRole('button', { name: /ROBOT-CELL-01|STATION-OP10-01/i }).first()
    await expect(testBarcodeBtn).toBeVisible()

    // Click close scanner
    const closeScannerBtn = page.getByRole('button', { name: /close scanner/i }).first()
    await closeScannerBtn.click()
    await expect(page.locator('video')).toHaveCount(0)
  })

  test('mobile action landing page /mobile/action processes QR scans with prefilled routing', async ({ page }) => {
    // 1. Test Check-In Mode on mobile action landing page
    await page.goto('/mobile/action?action=check-in&stationId=L06-OP150&stationName=Line%2006%20Battery%20Station&machineType=Screwing&priority=Critical')
    await page.waitForLoadState('domcontentloaded')

    // Verify equipment header metadata
    await expect(page.getByText('L06-OP150')).toBeVisible()
    await expect(page.getByText('Screwing')).toBeVisible()

    // Verify action buttons exist in check-in mode
    const checkInBtn = page.getByRole('button', { name: /confirm physical check-in/i })
    await expect(checkInBtn).toBeVisible()

    // 2. Test Report Incident navigation routes to /dashboard/tickets/new with prefilled params
    await page.goto('/mobile/action?action=report-incident&stationId=L06-OP150&stationName=Line%2006%20Battery%20Station&machineType=Screwing&priority=Critical&title=Axis%20Motor%20Overcurrent')
    await page.waitForLoadState('domcontentloaded')

    const launchIncidentBtn = page.getByRole('button', { name: /launch incident studio/i })
    await expect(launchIncidentBtn).toBeVisible()
    await launchIncidentBtn.click()

    await expect(page).toHaveURL(/\/dashboard\/tickets\/new/)
    await page.waitForLoadState('networkidle')

    // Verify incident creation workbench loaded with station prefilled
    await expect(page.getByText(/report maintenance incident/i)).toBeVisible()
    const stationInput = page.locator('input[placeholder*="Type machine ID"]')
    await expect(stationInput).toHaveValue(/L06-OP150/)
  })
})
