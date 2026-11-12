#!/usr/bin/env node
/**
 * Captures the README gallery (docs/media) from a running development instance.
 *
 *   just gallery [shots|videos|all]
 *   NODE_PATH=src/frontend/web/node_modules node tools/capture-gallery.cjs [shots|videos|all]
 *
 * Needs the dev stack (just start; the simulator makes the live pages move), playwright-core from the
 * frontend's node_modules and a Chrome/Chromium (CHROME_PATH, default /opt/google/chrome/chrome).
 * Signs in through the development SSO simulation (POST /api/auth/sso/simulate, refused in production)
 * as GALLERY_USER (default admin@heimdall.dev); each run leaves that 7-day development session in auth.session
 * (sign-out cannot end it: the simulated cookie is unsigned). The kiosk video dispatches one real ticket.
 * Videos are recorded as WebM and converted with ffmpeg to MP4 (linked) and a GIF preview (inline).
 */
const { chromium } = require('playwright-core')
const { execFileSync } = require('child_process')
const fs = require('fs')
const os = require('os')
const path = require('path')

const BASE = (process.env.BASE_URL || 'http://localhost:3000').replace(/\/+$/, '')
const CHROME = process.env.CHROME_PATH || '/opt/google/chrome/chrome'
const USER = process.env.GALLERY_USER || 'admin@heimdall.dev'
const OUT = path.resolve(__dirname, '..', 'docs', 'media')
const VIDEO_OUT = path.join(OUT, 'videos')
const MODE = process.argv[2] || 'all'

// The Nuxt DevTools button and its timing badge are dev-only chrome that does not belong in the gallery.
const HIDE_DEV_CHROME = `[id^="nuxt-devtools"], .nuxt-devtools-anchor { display: none !important; }`

const SHOTS = [
  { file: '02_dashboard.png', route: '/dashboard/overview' },
  { file: '03_machines_list.png', route: '/dashboard/machines' },
  {
    file: '04_tickets_kanban.png',
    route: '/dashboard/tickets',
    prepare: async page => {
      await page.getByRole('button', { name: 'Kanban', exact: true }).first().click()
      await page.waitForTimeout(2000)
    }
  },
  { file: '05_security_groups.png', route: '/dashboard/security-groups' },
  { file: '06_organizations.png', route: '/dashboard/organizations' },
  { file: '07_clients.png', route: '/dashboard/clients' },
  { file: '08_inventory.png', route: '/dashboard/inventory' },
  {
    file: '09_settings.png',
    route: '/dashboard/settings',
    prepare: async page => {
      await page.getByText('Security & API', { exact: true }).first().click()
      await page.waitForTimeout(1500)
    }
  },
  { file: '10_plant_map.png', route: '/dashboard/map' },
  { file: '11_live_telemetry.png', route: '/dashboard/telemetry' },
  { file: '12_fleet_analytics.png', route: '/dashboard/analytics' },
  {
    file: '13_lua_automation.png',
    route: '/dashboard/scripts',
    // A starter template in the editor (nothing is saved).
    prepare: async page => {
      await page.getByText('Telemetry Anomaly Spike Watchdog', { exact: false }).first().click()
      await page.waitForTimeout(1200)
    }
  },
  {
    file: '14_tickets_monitor.png',
    route: '/dashboard/tickets/monitor',
    // The incident queue rather than the line-stop tab, which is mostly empty on a healthy plant.
    prepare: async page => {
      await page.getByText('Incident Queue', { exact: false }).first().click()
      await page.waitForTimeout(1500)
    }
  }
]

const log = (...a) => console.log('[gallery]', ...a)

async function settle(page, ms = 6000) {
  await page.waitForTimeout(ms)
}

async function newContext(browser, extra = {}) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1.5, ...extra })
  await ctx.addInitScript(css => {
    const add = () => {
      const s = document.createElement('style')
      s.textContent = css
      document.documentElement.appendChild(s)
    }
    if (document.documentElement) add()
    else document.addEventListener('DOMContentLoaded', add)
  }, HIDE_DEV_CHROME)
  return ctx
}

async function signIn(ctx) {
  const res = await ctx.request.post(`${BASE}/api/auth/sso/simulate`, { data: { email: USER } })
  if (!res.ok()) throw new Error(`development sign-in failed: HTTP ${res.status()}`)
  const body = await res.json()
  log(`signed in as ${body.user?.email} (${body.user?.role})`)
  return body.session?.token
}

/** The station with the most open tickets in the BFF cache, and those tickets. */
async function pickKioskStation(ctx, page) {
  // The kiosk lists tickets the BFF has cached; the ticket board fills that cache from the backend.
  await page.goto(`${BASE}/dashboard/tickets`, { waitUntil: 'domcontentloaded', timeout: 90000 })
  await settle(page, 4000)
  const body = await (await ctx.request.get(`${BASE}/api/tickets`)).json().catch(() => [])
  const tickets = Array.isArray(body) ? body : (body.tickets || body.data || [])
  const open = tickets.filter(t => t.stationId && ['Open', 'InProgress', 'Pending'].includes(t.status))
  const byStation = new Map()
  for (const t of open) byStation.set(t.stationId, [...(byStation.get(t.stationId) || []), t])
  const [stationId] = [...byStation.entries()].sort((a, b) => b[1].length - a[1].length)[0] || []
  if (!stationId) throw new Error('no station with open tickets in the BFF cache')
  return stationId
}

async function openKioskTicket(page) {
  const row = page.locator('[data-testid="terminal-ticket-row"]').filter({ hasNotText: /Closed|Resolved/ }).first()
  await row.click()
  await page.waitForTimeout(700)
}

async function captureShots(browser) {
  const anon = await newContext(browser)
  const ap = await anon.newPage()
  await ap.goto(`${BASE}/auth/login`, { waitUntil: 'domcontentloaded', timeout: 90000 })
  await settle(ap, 4000)
  await ap.screenshot({ path: path.join(OUT, '01_sign_in.png') })
  log('01_sign_in.png')
  await anon.close()

  const ctx = await newContext(browser)
  const token = await signIn(ctx)
  const page = await ctx.newPage()
  for (const shot of SHOTS) {
    await page.goto(`${BASE}${shot.route}`, { waitUntil: 'domcontentloaded', timeout: 90000 })
    await settle(page)
    if (shot.prepare) await shot.prepare(page)
    await page.screenshot({ path: path.join(OUT, shot.file) })
    log(shot.file)
  }

  // Kiosk: a station's open ticket opened read-only, then its check-in QR code.
  const stationId = await pickKioskStation(ctx, page)
  await page.goto(`${BASE}/terminal/machine/${encodeURIComponent(stationId)}`, { waitUntil: 'domcontentloaded', timeout: 90000 })
  await settle(page)
  await openKioskTicket(page)
  await page.screenshot({ path: path.join(OUT, '15_kiosk_terminal.png') })
  log('15_kiosk_terminal.png', stationId)
  await page.locator('[data-testid="terminal-ticket-qr"]').click()
  await page.waitForTimeout(700)
  await page.locator('[data-testid="terminal-ticket-detail"]').evaluate(el => el.scrollIntoView({ block: 'start' }))
  await page.waitForTimeout(500)
  await page.screenshot({ path: path.join(OUT, '16_kiosk_ticket_qr.png') })
  log('16_kiosk_ticket_qr.png')

  await ctx.close()
  return { token, stationId }
}

module.exports = { BASE, OUT, VIDEO_OUT, newContext, signIn, pickKioskStation, openKioskTicket, settle, log }

if (require.main === module) {
  ;(async () => {
    fs.mkdirSync(OUT, { recursive: true })
    const browser = await chromium.launch({ executablePath: CHROME, headless: true })
    try {
      if (MODE === 'shots' || MODE === 'all') await captureShots(browser)
      if (MODE === 'videos' || MODE === 'all') await require('./capture-gallery-videos.cjs').captureVideos(browser)
    } finally {
      await browser.close()
    }
  })().catch(e => { console.error('[gallery] failed:', e); process.exit(1) })
}
