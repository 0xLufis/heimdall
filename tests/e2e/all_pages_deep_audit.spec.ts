import { test, expect } from '@playwright/test'

const ROUTES_TO_AUDIT = [
  { path: '/', name: 'Landing Page', auth: false },
  { path: '/auth/login', name: 'Login Page', auth: false },
  { path: '/auth/signup', name: 'Signup Page', auth: false },
  { path: '/auth/forgot-password', name: 'Forgot Password Page', auth: false },
  { path: '/dashboard', name: 'Dashboard Root', auth: true },
  { path: '/dashboard/overview', name: 'Dashboard Overview', auth: true },
  { path: '/dashboard/clients', name: 'Client PCs', auth: true },
  { path: '/dashboard/machines', name: 'Machines', auth: true },
  { path: '/dashboard/machine-groups', name: 'Machine Groups', auth: true },
  { path: '/dashboard/map', name: 'Plant Map', auth: true },
  { path: '/dashboard/inventory', name: 'Inventory Index', auth: true },
  { path: '/dashboard/inventory/new', name: 'Inventory Intake Studio', auth: true },
  { path: '/dashboard/inventory/part-fanuc-srv-01', name: 'Inventory Asset Detail', auth: true },
  { path: '/dashboard/telemetry', name: 'Live Telemetry Hub', auth: true },
  { path: '/dashboard/telemetry/stream', name: 'Telemetry Stream', auth: true },
  { path: '/dashboard/telemetry/templates', name: 'Telemetry Templates', auth: true },
  { path: '/dashboard/telemetry/configure', name: 'Telemetry Configure', auth: true },
  { path: '/dashboard/analytics', name: 'Fleet Analytics', auth: true },
  { path: '/dashboard/tickets', name: 'Tickets Index', auth: true },
  { path: '/dashboard/tickets/new', name: 'New Ticket Studio', auth: true },
  { path: '/dashboard/tickets/monitor', name: 'Tickets Monitor', auth: true },
  { path: '/dashboard/tickets/templates', name: 'Ticket Templates', auth: true },
  { path: '/dashboard/delegations', name: 'Technician Delegations', auth: true },
  { path: '/dashboard/users', name: 'Users & Roles', auth: true },
  { path: '/dashboard/organizations', name: 'Organizations Management', auth: true },
  { path: '/dashboard/security-groups', name: 'Security Groups Governance', auth: true },
  { path: '/dashboard/admin/system-settings', name: 'System Settings / Governance', auth: true },
  { path: '/dashboard/settings', name: 'User Settings', auth: true },
  { path: '/dashboard/help', name: 'Help & Support', auth: true },
  { path: '/terminal/machine/STATION-OP10-01?token=kiosk-token-op10', name: 'Shopfloor Terminal', auth: false },
]

test.describe('Comprehensive Application Routes & Screens Audit', () => {
  for (const route of ROUTES_TO_AUDIT) {
    test(`renders ${route.name} (${route.path}) without errors or blank screens`, async ({ context, page }) => {
      const pageErrors: string[] = []
      const consoleErrors: string[] = []

      page.on('pageerror', (err) => {
        pageErrors.push(err.message)
      })

      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text())
        }
      })

      if (route.auth) {
        await context.addCookies([
          { name: 'heimdall_test_session', value: 'true', domain: 'localhost', path: '/' }
        ])
      } else {
        await context.clearCookies()
      }

      const response = await page.goto(route.path, { waitUntil: 'domcontentloaded' })
      expect(response?.status()).toBeLessThan(400)

      // Ensure page actually rendered content and not a 404 or blank document
      await page.waitForTimeout(500)
      const bodyText = await page.locator('body').innerText()
      expect(bodyText.length).toBeGreaterThan(20)
      expect(bodyText).not.toContain('404 Page Not Found')
      expect(bodyText).not.toContain('This page could not be found')

      // Assert no unhandled JavaScript runtime exceptions crashed the page
      if (pageErrors.length > 0) {
        console.error(`Page error on ${route.path}:`, pageErrors)
      }
      expect(pageErrors).toEqual([])
    })
  }
})
