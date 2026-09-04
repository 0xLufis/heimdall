import { test, expect } from '@playwright/test'

test.describe('Better-Auth & Entra ID / Active Directory Security Group Org Governance', () => {
  const artifactDir = '/home/lufis/.gemini/antigravity-cli/brain/abde6d85-f09e-487f-921a-080bff43bd43'

  test.beforeEach(async ({ context }) => {
    await context.addCookies([
      { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' },
      {
        name: 'better-auth.session_token',
        value: 'IVIRvvEIj9I6LlGIslm3XfvqjopL5BqN.LkKu%2BWdiGjddYjP5j%2BJX7cUe8heOVCgyFIW8DMKMb3o%3D',
        domain: 'localhost',
        path: '/',
        httpOnly: true,
      },
    ])
  })

  test('Security Groups: Evaluates Entra ID / AD groups and maps organizations dynamically', async ({ page }) => {
    await page.goto('/dashboard/security-groups?mock_auth=true', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(1200)

    // Verify page header
    await expect(page.getByRole('heading', { name: /Active Directory & Entra ID Security Groups/i })).toBeVisible({ timeout: 10000 })

    // Take full-page baseline screenshot
    await page.screenshot({ path: `${artifactDir}/security_groups_org_mapping_governance.png`, fullPage: true })

    // 1. Click Sally Vance preset
    const sallyPreset = page.getByRole('button', { name: /Sally Vance/i })
    await expect(sallyPreset).toBeVisible({ timeout: 5000 })
    await sallyPreset.click()
    await page.waitForTimeout(400)

    // Click "Evaluate Claims & Org Provisioning" button
    const evalButton = page.getByRole('button', { name: /Evaluate Claims & Org Provisioning/i })
    await evalButton.click()
    await page.waitForTimeout(800)

    // Assert evaluation results for Sally Vance
    await expect(page.getByText('Line 06 – Battery Module Line').first()).toBeVisible({ timeout: 5000 })
    await expect(page.getByText('On-Prem Controls Engineers').first()).toBeVisible()
    await expect(page.getByText('line-06-battery-module-line').first()).toBeVisible()

    // Capture Sally Vance evaluation sandbox screenshot
    await page.screenshot({ path: `${artifactDir}/security_groups_evaluation_sandbox_sally_vance.png` })

    // 2. Click Root Admin preset
    const rootPreset = page.getByRole('button', { name: /Root Admin/i })
    await rootPreset.click()
    await page.waitForTimeout(400)

    // Click Evaluate again
    await evalButton.click()
    await page.waitForTimeout(800)

    // Assert evaluation results for Root Admin
    await expect(page.getByText('Factory Operations').first()).toBeVisible({ timeout: 5000 })
    await expect(page.getByText('OT Plant Administrators').first()).toBeVisible()
    await expect(page.getByText('factory-operations').first()).toBeVisible()

    // Capture Root Admin evaluation sandbox screenshot
    await page.screenshot({ path: `${artifactDir}/security_groups_evaluation_sandbox_root_admin.png` })
  })
})
