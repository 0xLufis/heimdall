import { test, expect } from '@playwright/test'

test.describe('MFA Policy, AD VLAN Host Import, and PKI Root CA Governance', () => {
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

  test('MFA Policy: Displays thresholds, adds custom group rule, and simulates live evaluation', async ({ page }) => {
    await page.goto('/dashboard/admin/system-settings?mock_auth=true', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(1000)

    // Verify System Settings header
    await expect(page.getByRole('heading', { name: /Master System Governance & Settings/i })).toBeVisible({ timeout: 10000 })

    // Verify MFA Policy Tab is active
    await page.getByRole('button', { name: /Authentication & MFA Governance|MFA Policy & Timeout Governance/i }).click()
    await page.waitForTimeout(600)

    // Check table cells & initial rules with exact: true
    await expect(page.getByRole('cell', { name: 'SystemAdministrator', exact: true })).toBeVisible({ timeout: 10000 })
    await expect(page.getByRole('cell', { name: /Always/i }).first()).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Engineer', exact: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: /Once a week/i }).first()).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Technician', exact: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: /Once a month/i }).first()).toBeVisible()

    // Add a custom security group rule
    const groupInput = page.getByPlaceholder(/e\.g\. Quality Assurance/i)
    await groupInput.fill('Quality Assurance Leads')
    await page.getByRole('button', { name: /Enforce Policy Rule|Add Rule/i }).click()
    await page.waitForTimeout(800)

    await expect(page.getByRole('cell', { name: 'Quality Assurance Leads', exact: true }).first()).toBeVisible({ timeout: 5000 })

    // Interactive Sandbox testing: Click preset '35 days ago' -> should trigger MFA challenge
    await page.getByRole('button', { name: '35 days ago' }).click()
    await page.waitForTimeout(600)
    await expect(page.getByText('MFA CHALLENGE REQUIRED')).toBeVisible({ timeout: 5000 })

    // Click preset '10m ago' for Engineer -> should be valid session
    await page.getByRole('button', { name: '10m ago' }).click()
    await page.waitForTimeout(600)
    await expect(page.getByText('MFA SESSION VALID (ACTIVE)')).toBeVisible({ timeout: 5000 })

    // Take screenshot of MFA Governance tab with live sandbox
    await page.screenshot({ path: `${artifactDir}/mfa_governance_and_timeout_thresholds.png`, fullPage: true })
  })

  test('Active Directory: Discovers VLAN-partitioned OUs and executes templating mass import', async ({ page }) => {
    await page.goto('/dashboard/admin/system-settings?mock_auth=true', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(1000)

    // Switch to AD Host Discovery tab
    await page.getByRole('button', { name: /Active Directory & Network Segmentation|AD Host Discovery/i }).click()
    await page.waitForTimeout(600)

    // Verify VLAN metrics and OU table
    await expect(page.getByText('VLAN 10').first()).toBeVisible({ timeout: 10000 })
    await expect(page.getByText('VLAN 50').first()).toBeVisible()
    await expect(page.getByText('VLAN 20').first()).toBeVisible()

    // Take screenshot of AD OU Tree
    await page.screenshot({ path: `${artifactDir}/ad_host_discovery_and_vlan_separation.png`, fullPage: true })

    // Open Ingestion Wizard Modal
    await page.getByRole('button', { name: /Launch Host Ingestion Wizard|Mass Import via Templating/i }).click()
    await page.waitForTimeout(800)

    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 })
    await expect(page.getByText(/Select Target Organizational Units|Select Active Directory OUs Partitioned by VLAN/i)).toBeVisible()

    // Trigger Evaluate Ingestion Preview
    const updatePreviewBtn = page.getByRole('button', { name: /Evaluate Ingestion Preview|Update Live Preview/i })
    await updatePreviewBtn.click()
    await page.waitForTimeout(800)

    // Screenshot of Mass Import modal with templating rules
    await page.screenshot({ path: `${artifactDir}/ad_mass_import_modal_preview.png` })

    // Click Execute Fleet Ingestion button
    const massImportBtn = page.getByRole('button', { name: /Execute Fleet Ingestion|Mass Import/i })
    await expect(massImportBtn).toBeEnabled({ timeout: 5000 })
    await massImportBtn.click()
    await page.waitForTimeout(1500)

    await expect(page.getByText(/Successfully ingested|Successfully processed|Successfully imported/i)).toBeVisible({ timeout: 10000 })
  })

  test('Certificates: Project Root CA inspection, existing cert import, and OU auto-enrollment rule', async ({ page }) => {
    await page.goto('/dashboard/admin/system-settings?mock_auth=true', { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(1000)

    // Switch to PKI & Certificates tab
    await page.getByRole('button', { name: /Public Key Infrastructure & Certificates|Project Root CA/i }).click()
    await page.waitForTimeout(600)

    // Verify Project Root CA card and thumbprint
    await expect(page.getByText(/Project Root Certificate Authority/i).first()).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(/Active Project Root CA/i).first()).toBeVisible()

    // Take screenshot of PKI tab
    await page.screenshot({ path: `${artifactDir}/project_root_ca_and_ou_certificate_rules.png`, fullPage: true })

    // Open Install External Root CA modal
    await page.getByRole('button', { name: /Install External Root CA|Import Existing Root Certificate/i }).click()
    await page.waitForTimeout(600)

    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 })
    await expect(page.getByText(/Install Corporate Root Certificate Authority|Import Existing Certificate as Project Root CA/i)).toBeVisible()

    // Click Load Industrial Demo CA Template
    await page.getByRole('button', { name: /Load Industrial Demo CA Template|Insert Sample Certificate/i }).click()
    await page.waitForTimeout(400)

    // Real-time verification preview should be visible
    await expect(page.getByText(/Valid X\.509 Certificate Detected/i)).toBeVisible({ timeout: 5000 })

    // Screenshot of Root CA Import Dialog
    await page.screenshot({ path: `${artifactDir}/root_ca_import_dialog.png` })

    // Commit & Activate Root CA
    await page.getByRole('button', { name: /Commit & Activate Root CA|Set as Project Root CA/i }).click()
    await page.waitForTimeout(1200)

    // Verify dialog closed
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 5000 })

    // Test Define OU Enrollment Rule
    await page.getByRole('button', { name: /Define OU Enrollment Rule|Add Assignment Rule/i }).click()
    await page.waitForTimeout(600)
    await expect(page.getByText(/Configure Active Directory Certificate Enrollment Policy|New OU Certificate Assignment Rule/i)).toBeVisible({ timeout: 5000 })

    // Pick sample OU
    await page.getByRole('button', { name: /Milling/i }).first().click()
    await page.waitForTimeout(300)

    await page.getByRole('button', { name: /Commit Policy Rule|Save Assignment Rule/i }).click()
    await page.waitForTimeout(1000)

    // Verify rule is listed (use .first() to handle any potential duplicate match)
    await expect(page.getByRole('cell', { name: /OU=Milling/i }).first()).toBeVisible({ timeout: 5000 })

    // Trigger Synchronize Fleet Certificates
    await page.getByRole('button', { name: /Synchronize Fleet Certificates|Sync & Auto-Enroll/i }).click()
    await page.waitForTimeout(2000)

    // Check that button text returned to normal (sync finished)
    await expect(page.getByRole('button', { name: /Synchronize Fleet Certificates|Sync & Auto-Enroll/i })).toBeVisible({ timeout: 8000 })
  })
})
