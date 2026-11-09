import { test, expect } from '@playwright/test'

test.describe('Escalation Tree & Dispatcher E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard/tickets')
    await page.waitForLoadState('networkidle')
  })

  test('opens escalation tree drawer from Kanban board without blocking modal backdrops', async ({ page }) => {
    // Switch to Kanban view
    const kanbanToggle = page.getByRole('button', { name: /kanban/i }).first()
    await expect(kanbanToggle).toBeVisible({ timeout: 10000 })
    await kanbanToggle.click()
    await page.waitForTimeout(500)

    // Find first Escalate button on a ticket card
    const escalateBtn = page.getByRole('button', { name: /^escalate$/i }).first()
    await expect(escalateBtn).toBeVisible({ timeout: 10000 })
    await escalateBtn.click()

    // Verify slide-out drawer opened
    const drawer = page.locator('[role="dialog"][aria-label*="Escalation"]')
    await expect(drawer).toBeVisible({ timeout: 5000 })

    // Verify non-blocking slide-out architecture (no fixed inset-0 backdrop)
    const modalBackdrops = page.locator('.fixed.inset-0.bg-background\\/80, .fixed.inset-0.bg-black\\/80')
    await expect(modalBackdrops).toHaveCount(0)

    // Verify hierarchy departments rendered in the tree
    await expect(page.getByText('Plant Operations & Command')).toBeVisible()
    await expect(page.getByText('Engineering & Controls (OT / PLC / Vision)')).toBeVisible()
    await expect(page.getByText('Mechanical Maintenance & Robotics')).toBeVisible()
    await expect(page.getByText('Safety & EHS Critical Response')).toBeVisible()

    // Verify On-Call Channels
    await expect(page.getByText('#Incident-Command')).toBeVisible()
    await expect(page.getByText('#Controls-Emergency')).toBeVisible()

    // Test search filter
    const searchInput = drawer.locator('input[type="text"]').first()
    await searchInput.fill('Sally')
    await expect(drawer.getByText('Engineer Sally').first()).toBeVisible()
    await expect(drawer.getByText('Shift Leader Ferenc')).not.toBeVisible()

    // Clear search
    await searchInput.fill('')
    await expect(drawer.getByText('Plant Operations & Command')).toBeVisible()

    // Close drawer via cancel button
    const cancelBtn = drawer.getByRole('button', { name: /cancel/i })
    await cancelBtn.click()
    await expect(drawer).not.toBeVisible()
  })

  test('dispatches escalation with selected target, handover protocol and card preview', async ({ page }) => {
    // Switch to Kanban view
    const kanbanToggle = page.getByRole('button', { name: /kanban/i }).first()
    await expect(kanbanToggle).toBeVisible({ timeout: 10000 })
    await kanbanToggle.click()
    await page.waitForTimeout(500)

    const escalateBtn = page.getByRole('button', { name: /^escalate$/i }).first()
    await expect(escalateBtn).toBeVisible({ timeout: 10000 })
    await escalateBtn.click()

    const drawer = page.locator('[role="dialog"][aria-label*="Escalation"]')
    await expect(drawer).toBeVisible({ timeout: 5000 })

    // Select channel #Controls-Emergency
    const channelNode = drawer.locator('.group').filter({ hasText: '#Controls-Emergency' }).first()
    await channelNode.click()

    // Verify confirmation card updated
    await expect(drawer.getByText('Target Recipient Selected')).toBeVisible()
    await expect(drawer.getByText('controls-emergency@heimdall.plant')).toBeVisible()

    // Select Parallel Work handover protocol
    const parallelProtocol = drawer.locator('.cursor-pointer').filter({ hasText: 'Parallel Work' }).first()
    await parallelProtocol.click()

    // Click a 1-click reason template
    const templateChip = drawer.locator('button[type="button"]').filter({ hasText: '...' }).first()
    await templateChip.click()

    // Toggle Card preview
    const previewToggle = drawer.getByRole('button', { name: /Incident Dispatch Card Preview/i })
    await previewToggle.click()
    await expect(drawer.getByText('Heimdall Incident Command')).toBeVisible()
    await expect(drawer.getByText('Acknowledge & Join Bridge')).toBeVisible()

    // Dispatch Escalation
    const dispatchBtn = drawer.getByRole('button', { name: /dispatch escalation/i })
    await expect(dispatchBtn).toBeEnabled()
    await dispatchBtn.click()

    // Verify toast notification
    await expect(page.getByText(/escalated to/i).first()).toBeVisible({ timeout: 10000 })

    // Verify drawer closed
    await expect(drawer).not.toBeVisible()
  })

  test('escalation tree drawer functions seamlessly in dedicated ticket page /dashboard/tickets/[id]', async ({ page }) => {
    // Navigate to tickets and open first ticket
    const firstTicketLink = page.locator('table tbody tr a, .group a[href*="/dashboard/tickets/"]').first()
    if (await firstTicketLink.isVisible()) {
      await firstTicketLink.click()
    } else {
      await page.goto('/dashboard/tickets/tkt-001')
    }
    await page.waitForLoadState('networkidle')

    // If already escalated, clear escalation first
    const clearBtn = page.getByRole('button', { name: /resolve escalation|clear/i }).first()
    if (await clearBtn.isVisible()) {
      await clearBtn.click()
      await page.waitForTimeout(500)
    }

    // Click Escalate button
    const escalateBtn = page.getByRole('button', { name: /escalate/i }).first()
    await expect(escalateBtn).toBeVisible({ timeout: 10000 })
    await escalateBtn.click()

    // Verify Escalation Tree Drawer opens
    const drawer = page.locator('[role="dialog"][aria-label*="Escalation"]')
    await expect(drawer).toBeVisible({ timeout: 5000 })

    // Verify presence indicators
    await expect(drawer.getByText('Available').first()).toBeVisible()

    // Select Engineer Sally
    const sallyNode = drawer.locator('.group').filter({ hasText: 'Engineer Sally' }).first()
    await sallyNode.click()

    // Fill escalation reason
    const textarea = drawer.locator('textarea')
    await textarea.fill('E2E PLC diagnostics required for Beckhoff TwinCAT station.')

    // Dispatch
    const dispatchBtn = drawer.getByRole('button', { name: /dispatch escalation/i })
    await dispatchBtn.click()

    // Verify escalation banner on ticket page
    await expect(page.getByText(/ESCALATED TO/i).first()).toBeVisible({ timeout: 10000 })
  })
})
