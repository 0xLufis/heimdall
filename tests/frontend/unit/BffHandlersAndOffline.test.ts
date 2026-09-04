import { describe, it, expect } from 'vitest'
import {
  getTicketsStore,
  addTicketToStore,
  findTicketById,
  updateTicketInStore,
  addCommentToTicket
} from '~~/server/utils/ticketsStore'

describe('Nitro BFF Handlers & Ticket Store Logic', () => {
  it('initializes ticket store with default industrial maintenance tickets', () => {
    const store = getTicketsStore()
    expect(store.length).toBeGreaterThan(0)

    const op10Ticket = findTicketById('tkt-001')
    expect(op10Ticket).toBeDefined()
    expect(op10Ticket?.stationId).toBe('STATION-OP10-01')
    expect(op10Ticket?.priority).toBe('Critical')
  })

  it('adds a new ticket to store and generates correct properties', () => {
    const initialCount = getTicketsStore().length
    const newTkt = addTicketToStore({
      id: 'tkt-test-99',
      ticketNumber: 'TKT-2026-9999',
      stationId: 'STATION-OP40-04',
      stationName: 'OP40 Packaging Unit',
      title: 'Conveyor Belt Motor Jam',
      description: 'Motor thermal overload tripped',
      status: 'Open',
      priority: 'High',
      reportedByUserId: 'usr-op-04',
      reportedByUserName: 'Test Operator',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      slaDueAt: new Date(Date.now() + 8 * 3600 * 1000).toISOString(),
      comments: [],
      attachments: []
    })

    expect(getTicketsStore().length).toBe(initialCount + 1)
    expect(newTkt.id).toBe('tkt-test-99')
  })

  it('updates ticket status and records resolvedAt timestamp', () => {
    const updated = updateTicketInStore('tkt-003', { status: 'Resolved' })
    expect(updated).toBeDefined()
    expect(updated?.status).toBe('Resolved')
    expect(updated?.resolvedAt).toBeDefined()
  })

  it('adds technician comments to ticket timeline', () => {
    const comment = addCommentToTicket('tkt-001', {
      id: 'c-test-1',
      ticketId: 'tkt-001',
      authorUserId: 'usr-tech-01',
      authorName: 'Gábor Varga',
      content: 'Replacement bearing delivered from store room.',
      createdAt: new Date().toISOString()
    })

    expect(comment).toBeDefined()
    const tkt = findTicketById('tkt-001')
    expect(tkt?.comments.some(c => c.id === 'c-test-1')).toBe(true)
  })
})
