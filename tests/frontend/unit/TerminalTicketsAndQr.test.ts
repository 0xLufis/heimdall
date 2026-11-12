import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import {
  toTerminalTicketView,
  terminalTicketsForStation,
  TERMINAL_TICKET_LIMIT,
  TERMINAL_TICKET_COMMENT_LIMIT
} from '~~/server/utils/terminalTickets'
import TerminalTicketDetail from '../../../src/frontend/web/app/components/terminal/TerminalTicketDetail.vue'
import TerminalQrCard from '../../../src/frontend/web/app/components/terminal/TerminalQrCard.vue'

const repoRoot = resolve(__dirname, '../../..')

const ticket = (over: Record<string, any> = {}): any => ({
  id: 't-1',
  ticketNumber: 'TKT-1001',
  stationId: 'm-op60',
  stationName: 'OP060 Vision',
  title: '[ERR-MECH-02] Mechanical Jam / Feeder Error',
  description: 'Part jammed in the feeder track.',
  status: 'InProgress',
  priority: 'High',
  category: 'Error',
  isLineStop: true,
  errorCode: 'ERR-MECH-02',
  reportedByUserId: 'u-secret',
  reportedByUserName: 'Station operator',
  assignedTechnicianId: 'u-tech',
  assignedTechnicianName: 'Ana Tech',
  escalationReason: 'internal',
  createdAt: '2026-11-12T08:00:00.000Z',
  updatedAt: '2026-11-12T08:30:00.000Z',
  comments: [],
  ...over
})

const comment = (createdAt: string, content: string) => ({
  id: `c-${createdAt}`, ticketId: 't-1', authorUserId: 'u-secret', authorName: 'Ana Tech', content, createdAt,
  attachments: [{ id: 'a1', fileName: 'x.png' }]
})

describe('kiosk ticket projection (server)', () => {
  it('keeps what the read-only view shows and drops user ids, attachments and escalation internals', () => {
    const view = toTerminalTicketView(ticket({
      comments: [
        comment('2026-11-12T08:10:00.000Z', 'first'),
        comment('2026-11-12T08:40:00.000Z', 'newest'),
        comment('2026-11-12T08:20:00.000Z', '   '),
        comment('2026-11-12T08:30:00.000Z', 'third'),
        comment('2026-11-12T08:15:00.000Z', 'second')
      ]
    }))

    expect(view).toMatchObject({
      ticketNumber: 'TKT-1001', description: 'Part jammed in the feeder track.', isLineStop: true,
      errorCode: 'ERR-MECH-02', assignedTechnicianName: 'Ana Tech', reportedByUserName: 'Station operator'
    })
    const json = JSON.stringify(view)
    expect(json).not.toContain('u-secret')
    expect(json).not.toContain('u-tech')
    expect(json).not.toContain('x.png')
    expect(json).not.toContain('escalation')
    // Newest first, blank ones skipped, at most the limit.
    expect(view.comments.map(c => c.content)).toEqual(['newest', 'third', 'second'].slice(0, TERMINAL_TICKET_COMMENT_LIMIT))
    expect(Object.keys(view.comments[0]!).sort()).toEqual(['authorName', 'content', 'createdAt'])
  })

  it('reports a pending reason only while the ticket is pending', () => {
    expect(toTerminalTicketView(ticket({ status: 'Pending', pendingReason: 'SparePart' })).pendingReason).toBe('SparePart')
    expect(toTerminalTicketView(ticket({ status: 'Pending', pendingReason: 'None' })).pendingReason).toBeUndefined()
    expect(toTerminalTicketView(ticket({ status: 'InProgress', pendingReason: 'SparePart' })).pendingReason).toBeUndefined()
  })

  it('lists the station\'s tickets by id, identifier or name, never by empty values, up to the limit', () => {
    const machine = { id: 'm-op60', name: 'OP060 Vision', customIdentifier: 'OP060' }
    const tickets = [
      ticket({ id: 'a', stationId: 'm-op60' }),
      ticket({ id: 'b', stationId: 'OP060', stationName: 'renamed' }),
      ticket({ id: 'c', stationId: 'other', stationName: 'OP060 Vision' }),
      ticket({ id: 'd', stationId: 'other', stationName: 'Other' }),
      ticket({ id: 'e', stationId: '', stationName: '' })
    ]
    expect(terminalTicketsForStation(tickets, machine).map(t => t.id)).toEqual(['a', 'b', 'c'])

    // A machine without identifier or name must not pick up tickets that have no station.
    expect(terminalTicketsForStation(tickets, { id: 'm-x' }).map(t => t.id)).toEqual([])

    const many = Array.from({ length: TERMINAL_TICKET_LIMIT + 3 }, (_, i) => ticket({ id: `n${i}` }))
    expect(terminalTicketsForStation(many, machine)).toHaveLength(TERMINAL_TICKET_LIMIT)
  })
})

const station = { id: 'm-op60', name: 'OP060 Vision', displayName: 'OP060 Vision Inspection', customIdentifier: 'OP060', machineType: 'Vision', groupId: 'line-7' }

function qrParams(wrapper: ReturnType<typeof mount>) {
  const url = new URL(wrapper.get('[data-testid="terminal-qr-url"]').text())
  return { path: url.pathname, params: Object.fromEntries(url.searchParams) }
}

describe('kiosk read-only ticket view', () => {
  it('shows the ticket without any control that could change it', () => {
    const wrapper = mount(TerminalTicketDetail, {
      props: { ticket: toTerminalTicketView(ticket({ comments: [comment('2026-11-12T08:40:00.000Z', 'Feeder cleared, testing')] })), station }
    })
    const text = wrapper.text()
    expect(text).toContain('TKT-1001')
    expect(text).toContain('Mechanical Jam')
    expect(text).toContain('Part jammed in the feeder track.')
    expect(text).toContain('Ana Tech')
    expect(text).toContain('Line stop')
    expect(text).toContain('Read-only')
    expect(text).toContain('Feeder cleared, testing')
    expect(wrapper.findAll('input, textarea, select')).toHaveLength(0)
    // Back and Show QR are the only buttons.
    expect(wrapper.findAll('button').map(b => b.attributes('data-testid'))).toEqual(['terminal-ticket-back', 'terminal-ticket-qr'])
  })

  it('goes back to the list', async () => {
    const wrapper = mount(TerminalTicketDetail, { props: { ticket: toTerminalTicketView(ticket()), station } })
    await wrapper.get('[data-testid="terminal-ticket-back"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('shows a check-in QR code for the ticket, or one that opens it on a phone', async () => {
    const wrapper = mount(TerminalTicketDetail, { props: { ticket: toTerminalTicketView(ticket()), station } })
    expect(wrapper.find('[data-testid="terminal-qr-card"]').exists()).toBe(false)

    await wrapper.get('[data-testid="terminal-ticket-qr"]').trigger('click')
    expect(wrapper.get('[data-testid="terminal-qr-image"]').attributes('src')).toMatch(/^data:image\/svg\+xml/)
    let qr = qrParams(wrapper)
    expect(qr.path).toBe('/mobile/action')
    expect(qr.params).toEqual({ action: 'check-in', stationId: 'OP060', ticketId: 't-1' })

    await wrapper.get('[data-testid="terminal-qr-action-view-ticket"]').trigger('click')
    qr = qrParams(wrapper)
    expect(qr.params).toEqual({ action: 'view-ticket', stationId: 'OP060', ticketId: 't-1' })

    await wrapper.get('[data-testid="terminal-qr-close"]').trigger('click')
    expect(wrapper.find('[data-testid="terminal-qr-card"]').exists()).toBe(false)
  })
})

describe('kiosk station QR code', () => {
  it('opens incident reporting for the station on a phone, or the station page', async () => {
    const wrapper = mount(TerminalQrCard, { props: { station } })
    let qr = qrParams(wrapper)
    expect(qr.params).toEqual({ action: 'report-incident', stationId: 'OP060', machineType: 'Vision', groupId: 'line-7' })
    expect(wrapper.text()).toContain('OP060 Vision Inspection')

    await wrapper.get('[data-testid="terminal-qr-action-view-station"]').trigger('click')
    qr = qrParams(wrapper)
    expect(qr.params).toEqual({ action: 'view-station', stationId: 'OP060' })

    await wrapper.get('[data-testid="terminal-qr-close"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})

describe('kiosk page wiring', () => {
  const page = readFileSync(resolve(repoRoot, 'src/frontend/web/app/pages/terminal/machine/[id].vue'), 'utf8')

  it('offers the station QR code and tappable read-only tickets in both layouts', () => {
    expect(page.match(/data-testid="terminal-station-qr"/g)).toHaveLength(2)
    expect(page.match(/data-testid="terminal-ticket-row"/g)).toHaveLength(2)
    expect(page.match(/<TerminalTicketDetail/g)).toHaveLength(2)
    expect(page.match(/<TerminalQrCard/g)).toHaveLength(2)
  })

  it('keeps them as panels in the page, not modal overlays', () => {
    for (const file of ['TerminalTicketDetail.vue', 'TerminalQrCard.vue']) {
      const src = readFileSync(resolve(repoRoot, 'src/frontend/web/app/components/terminal', file), 'utf8')
      expect(src).not.toMatch(/\bfixed\b|\binset-0\b|<Dialog|<Sheet/)
    }
  })
})
