import { test, expect } from '@playwright/test'

test.describe('Maintenance Incident Dispatching & Kanban Board E2E', () => {
  test.beforeEach(async ({ context, page }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
    ])
    await page.goto('/dashboard/tickets')
    await page.waitForLoadState('domcontentloaded')
  })

  test('displays maintenance metrics cards overview', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /maintenance & floor incidents/i })).toBeVisible({ timeout: 10000 })
    await expect(page.getByText('Total Open')).toBeVisible({ timeout: 10000 })
  })

  test('switches between List view and Kanban board view', async ({ page }) => {
    const kanbanToggle = page.getByRole('button', { name: /kanban/i }).first()
    await expect(kanbanToggle).toBeVisible({ timeout: 10000 })
    await kanbanToggle.click()

    await expect(page.getByText('In Progress').first()).toBeVisible({ timeout: 5000 })

    const listToggle = page.getByRole('button', { name: /list/i }).first()
    await listToggle.click()
  })

  test('opens report incident modal and shows form fields', async ({ page }) => {
    const reportBtn = page.getByRole('button', { name: /report incident/i }).first()
    await expect(reportBtn).toBeVisible({ timeout: 10000 })
    await page.waitForTimeout(2000)
    await reportBtn.click()

    await expect(page.locator('[role="dialog"]').first()).toBeVisible({ timeout: 5000 })
    await expect(page.getByText('Report Maintenance Ticket')).toBeVisible({ timeout: 5000 })
  })
})
