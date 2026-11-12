/**
 * The gallery videos (see tools/capture-gallery.cjs for how to run them). Each clip runs in its own browser
 * context and is recorded from Chrome's screencast (CDP Page.startScreencast: a JPEG whenever the screen
 * changes) once the first page has loaded, so no Playwright video build is needed. The frames are encoded
 * with the system ffmpeg into an H.264 MP4 (linked from the README) and a palette-optimised GIF (inline).
 */
const { execFileSync } = require('child_process')
const fs = require('fs')
const os = require('os')
const path = require('path')
const g = require('./capture-gallery.cjs')

const WIDE = { width: 1280, height: 800 }
const OVERLAY = { width: 460, height: 820 }

const goto = (page, route) => page.goto(`${g.BASE}${route}`, { waitUntil: 'domcontentloaded', timeout: 90000 })
const pause = (page, ms) => page.waitForTimeout(ms)

const VIDEOS = [
  {
    name: 'kiosk_dispatch_and_qr',
    viewport: WIDE,
    gifWidth: 800,
    // Two-tap fault report (dispatches one real ticket), the ticket read-only, its QR code, the station QR code.
    run: async (page, { stationId, markStart }) => {
      await goto(page, `/terminal/machine/${encodeURIComponent(stationId)}`)
      await pause(page, 5000)
      await markStart()
      await pause(page, 1500)
      await page.getByText('Mechanical Jam', { exact: false }).first().click()
      await pause(page, 1500)
      await page.locator('button:has-text("CLICK 2: DISPATCH")').click()
      await pause(page, 3500)
      await g.openKioskTicket(page)
      await pause(page, 2500)
      await page.locator('[data-testid="terminal-ticket-qr"]').click()
      await pause(page, 400)
      await page.locator('[data-testid="terminal-ticket-detail"]').evaluate(el => el.scrollIntoView({ block: 'start', behavior: 'smooth' }))
      await pause(page, 2500)
      await page.locator('[data-testid="terminal-qr-action-view-ticket"]').click()
      await pause(page, 2000)
      await page.locator('[data-testid="terminal-qr-action-check-in"]').click()
      await pause(page, 1500)
      await page.locator('[data-testid="terminal-ticket-back"]').click()
      await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' }))
      await pause(page, 1200)
      await page.locator('[data-testid="terminal-station-qr"]').click()
      await pause(page, 3000)
    }
  },
  {
    name: 'kiosk_overlay',
    viewport: OVERLAY,
    gifWidth: 340,
    // The compact always-on-top window: station QR code, then a ticket read-only with its QR code.
    run: async (page, { stationId, markStart }) => {
      await goto(page, `/terminal/machine/${encodeURIComponent(stationId)}?overlay=1`)
      await pause(page, 5000)
      await markStart()
      await pause(page, 1500)
      await page.locator('[data-testid="terminal-station-qr"]').click()
      await pause(page, 3000)
      await page.locator('[data-testid="terminal-qr-action-view-station"]').click()
      await pause(page, 1500)
      await page.locator('[data-testid="terminal-qr-close"]').click()
      await pause(page, 600)
      const row = page.locator('[data-testid="terminal-ticket-row"]').filter({ hasNotText: /Closed|Resolved/ }).first()
      await row.evaluate(el => el.scrollIntoView({ block: 'center', behavior: 'smooth' }))
      await pause(page, 1200)
      await row.click()
      await pause(page, 1000)
      await page.locator('[data-testid="terminal-ticket-detail"]').evaluate(el => el.scrollIntoView({ block: 'start', behavior: 'smooth' }))
      await pause(page, 2000)
      await page.locator('[data-testid="terminal-ticket-qr"]').click()
      await pause(page, 400)
      await page.locator('[data-testid="terminal-qr-image"]').evaluate(el => el.scrollIntoView({ block: 'center', behavior: 'smooth' }))
      await pause(page, 3500)
    }
  },
  {
    name: 'plant_map_live',
    viewport: WIDE,
    gifWidth: 800,
    // Live station pins, every node's telemetry bubble, then one station's card.
    run: async (page, { markStart }) => {
      await goto(page, '/dashboard/map')
      await pause(page, 6000)
      await markStart()
      await pause(page, 2500)
      await page.getByText('All Nodes', { exact: true }).first().click()
      await pause(page, 4000)
      await page.getByText('Alerts & SLA', { exact: true }).first().click()
      await pause(page, 1500)
      const flagged = page.locator('.station-pin[data-pin-tone="critical"], .station-pin[data-pin-tone="warning"]')
      await ((await flagged.count()) ? flagged.first() : page.locator('.station-pin').first()).click()
      await pause(page, 4500)
    }
  },
  {
    name: 'live_telemetry',
    viewport: WIDE,
    gifWidth: 800,
    // Station dynamics streaming in, then switching station through the filter.
    run: async (page, { markStart }) => {
      await goto(page, '/dashboard/telemetry')
      await pause(page, 6000)
      await markStart()
      await pause(page, 3500)
      const filter = page.getByPlaceholder('Filter stations or lines', { exact: false }).first()
      await filter.click()
      await filter.pressSequentially('OP060', { delay: 140 })
      await pause(page, 1500)
      await page.locator('text=/^L0[1-8]-OP060 –/').first().click()
      await pause(page, 4500)
    }
  },
  {
    name: 'search_and_context_menu',
    viewport: WIDE,
    gifWidth: 800,
    // Ctrl+K global search into a station, then the right-click menu on the ticket board.
    run: async (page, { markStart }) => {
      await goto(page, '/dashboard/overview')
      await pause(page, 6000)
      await markStart()
      await pause(page, 1200)
      await page.keyboard.press('Control+k')
      await pause(page, 800)
      await page.keyboard.type('OP060', { delay: 140 })
      await pause(page, 2500)
      await page.keyboard.press('ArrowDown')
      await pause(page, 500)
      await page.keyboard.press('Enter')
      await pause(page, 4000)
      await goto(page, '/dashboard/tickets')
      await pause(page, 4500)
      const ticket = page.locator('text=/^TKT-[0-9A-F]{6,}/').first()
      const box = await ticket.boundingBox()
      if (box) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 8 })
        await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2, { button: 'right' })
        await pause(page, 3000)
        await page.keyboard.press('Escape')
        await pause(page, 800)
      }
    }
  }
]

/** Starts Chrome's screencast on the page; frames carry their capture time (seconds). */
async function startScreencast(ctx, page, viewport) {
  const cdp = await ctx.newCDPSession(page)
  const frames = []
  cdp.on('Page.screencastFrame', ({ data, metadata, sessionId }) => {
    frames.push({ data, ts: metadata.timestamp })
    cdp.send('Page.screencastFrameAck', { sessionId }).catch(() => {})
  })
  await cdp.send('Page.startScreencast', {
    format: 'jpeg', quality: 92, maxWidth: viewport.width, maxHeight: viewport.height, everyNthFrame: 1
  })
  return {
    frames,
    async stop() {
      await cdp.send('Page.stopScreencast').catch(() => {})
      await cdp.detach().catch(() => {})
      return Date.now() / 1000
    }
  }
}

/** Frames (variable rate, only on change) -> constant-rate MP4 and GIF through ffmpeg's concat demuxer. */
function encode(frames, endTs, video, dir) {
  if (frames.length < 2) throw new Error(`${video.name}: only ${frames.length} frame(s) captured`)
  const list = ['ffconcat version 1.0']
  frames.forEach((f, i) => {
    const file = path.join(dir, `f${String(i).padStart(5, '0')}.jpg`)
    fs.writeFileSync(file, Buffer.from(f.data, 'base64'))
    const next = i + 1 < frames.length ? frames[i + 1].ts : Math.max(endTs, f.ts + 0.5)
    list.push(`file '${file}'`, `duration ${Math.max(0.01, next - f.ts).toFixed(3)}`)
  })
  list.push(`file '${path.join(dir, `f${String(frames.length - 1).padStart(5, '0')}.jpg`)}'`) // concat: last entry needs repeating
  const listFile = path.join(dir, 'frames.ffconcat')
  fs.writeFileSync(listFile, list.join('\n') + '\n')

  fs.mkdirSync(g.VIDEO_OUT, { recursive: true })
  const mp4 = path.join(g.VIDEO_OUT, `${video.name}.mp4`)
  const gif = path.join(g.VIDEO_OUT, `${video.name}.gif`)
  const input = ['-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', listFile]
  execFileSync('ffmpeg', [...input, '-vf', 'fps=25,format=yuv420p', '-c:v', 'libx264', '-preset', 'slow', '-crf', '26',
    '-movflags', '+faststart', '-an', mp4])
  execFileSync('ffmpeg', [...input,
    '-vf', `fps=8,scale=${video.gifWidth}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle`,
    '-loop', '0', gif])
  const kb = f => Math.round(fs.statSync(f).size / 1024)
  const seconds = (endTs - frames[0].ts).toFixed(1)
  g.log(`${video.name}: ${frames.length} frames over ${seconds} s -> mp4 ${kb(mp4)} KB, gif ${kb(gif)} KB`)
}

async function captureVideos(browser, only) {
  const auth = await g.newContext(browser)
  await g.signIn(auth)
  const page = await auth.newPage()
  const stationId = await g.pickKioskStation(auth, page)
  const storageState = await auth.storageState()
  await auth.close()

  for (const video of VIDEOS.filter(v => !only || only.includes(v.name))) {
    const ctx = await g.newContext(browser, { viewport: video.viewport, deviceScaleFactor: 1, storageState })
    const p = await ctx.newPage()
    let cast = null
    try {
      await video.run(p, { stationId, markStart: async () => { cast = await startScreencast(ctx, p, video.viewport) } })
    } catch (e) {
      g.log(`${video.name}: step failed (${String(e).split('\n')[0]}); keeping what was recorded`)
    }
    await pause(p, 600)
    const endTs = cast ? await cast.stop() : 0
    await ctx.close()
    if (!cast) { g.log(`${video.name}: never reached its start mark, skipped`); continue }
    // Frames go to disk, not to a RAM-backed /tmp, unless GALLERY_TMP says otherwise.
    const scratch = process.env.GALLERY_TMP || (fs.existsSync('/var/tmp') ? '/var/tmp' : os.tmpdir())
    const dir = fs.mkdtempSync(path.join(scratch, 'heimdall-gallery-'))
    try {
      encode(cast.frames, endTs, video, dir)
    } finally {
      fs.rmSync(dir, { recursive: true, force: true })
    }
  }
}

module.exports = { captureVideos, VIDEOS }
