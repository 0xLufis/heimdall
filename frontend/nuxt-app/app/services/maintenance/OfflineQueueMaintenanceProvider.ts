import { openDB, type IDBPDatabase } from 'idb'
import type {
  IMaintenanceService,
  MaintenanceTicket,
  CreateTicketInput,
  TicketFilter,
  TicketMetrics,
  MaintenanceEvent,
  TicketStatus,
  TicketComment
} from '~/types/maintenance'

const DB_NAME = 'heimdall-maintenance-db'
const DB_VERSION = 1
const STORE_TICKETS = 'cached-tickets'
const STORE_QUEUE = 'offline-mutation-queue'

export class OfflineQueueMaintenanceProvider implements IMaintenanceService {
  private dbPromise: Promise<IDBPDatabase> | null = null
  private isOnline = true
  private eventListeners: Set<(event: MaintenanceEvent) => void> = new Set()

  constructor(private innerProvider: IMaintenanceService) {
    if (typeof window !== 'undefined') {
      this.isOnline = navigator.onLine
      window.addEventListener('online', () => this.handleOnline())
      window.addEventListener('offline', () => { this.isOnline = false })
      this.initDB()
      this.innerProvider.subscribeToEvents(event => this.notifyListeners(event))
    }
  }

  private getDB(): Promise<IDBPDatabase> {
    if (!this.dbPromise) {
      this.dbPromise = openDB(DB_NAME, DB_VERSION, {
        upgrade(db) {
          if (!db.objectStoreNames.contains(STORE_TICKETS)) {
            db.createObjectStore(STORE_TICKETS, { keyPath: 'id' })
          }
          if (!db.objectStoreNames.contains(STORE_QUEUE)) {
            db.createObjectStore(STORE_QUEUE, { autoIncrement: true, keyPath: 'queueId' })
          }
        }
      })
    }
    return this.dbPromise
  }

  private async initDB() {
    try {
      await this.getDB()
      if (this.isOnline) {
        await this.replayOfflineQueue()
      }
    } catch (e) {
      console.warn('IndexedDB initialization skipped or failed:', e)
    }
  }

  private async handleOnline() {
    this.isOnline = true
    await this.replayOfflineQueue()
  }

  public async replayOfflineQueue() {
    if (!this.isOnline) return
    try {
      const db = await this.getDB()
      const queue = await db.getAll(STORE_QUEUE)
      if (!queue || queue.length === 0) return

      for (const item of queue) {
        try {
          if (item.action === 'CREATE') {
            await this.innerProvider.createTicket(item.payload)
          } else if (item.action === 'STATUS_UPDATE') {
            await this.innerProvider.updateTicketStatus(item.payload.id, item.payload.status)
          } else if (item.action === 'COMMENT') {
            await this.innerProvider.addComment(item.payload.ticketId, item.payload.authorName, item.payload.content)
          }
          await db.delete(STORE_QUEUE, item.queueId)
        } catch (e) {
          console.error('Failed to replay offline mutation:', item, e)
        }
      }
      this.notifyListeners({
        type: 'TicketUpdated',
        timestamp: new Date().toISOString()
      })
    } catch (e) {
      console.error('Error replaying offline queue:', e)
    }
  }

  public async getPendingCount(): Promise<number> {
    try {
      const db = await this.getDB()
      return await db.count(STORE_QUEUE)
    } catch {
      return 0
    }
  }

  private notifyListeners(event: MaintenanceEvent) {
    this.eventListeners.forEach(listener => {
      try {
        listener(event)
      } catch (e) {
        console.error('Error in offline decorator event listener:', e)
      }
    })
  }

  public async getTickets(filter?: TicketFilter): Promise<MaintenanceTicket[]> {
    if (this.isOnline) {
      try {
        const liveTickets = await this.innerProvider.getTickets(filter)
        // Cache in background
        this.cacheTickets(liveTickets)
        return liveTickets
      } catch {
        // Fallback to cache on network error
      }
    }

    // Offline mode: read from cache
    try {
      const db = await this.getDB()
      let cached: MaintenanceTicket[] = await db.getAll(STORE_TICKETS)
      if (filter?.status && filter.status !== 'all') {
        cached = cached.filter(t => t.status === filter.status)
      }
      if (filter?.priority && filter.priority !== 'all') {
        cached = cached.filter(t => t.priority === filter.priority)
      }
      return cached
    } catch {
      return []
    }
  }

  private async cacheTickets(tickets: MaintenanceTicket[]) {
    try {
      const db = await this.getDB()
      const tx = db.transaction(STORE_TICKETS, 'readwrite')
      for (const t of tickets) {
        await tx.store.put(t)
      }
      await tx.done
    } catch {
      // Ignore cache write errors
    }
  }

  public async getTicketById(id: string): Promise<MaintenanceTicket | null> {
    if (this.isOnline) {
      try {
        return await this.innerProvider.getTicketById(id)
      } catch {
        // Fall back to cache
      }
    }
    try {
      const db = await this.getDB()
      return (await db.get(STORE_TICKETS, id)) || null
    } catch {
      return null
    }
  }

  public async createTicket(input: CreateTicketInput): Promise<MaintenanceTicket> {
    if (this.isOnline) {
      try {
        const created = await this.innerProvider.createTicket(input)
        const db = await this.getDB()
        await db.put(STORE_TICKETS, created)
        return created
      } catch {
        // Fallback to offline queue
      }
    }

    // Queue mutation in IndexedDB
    const fakeId = `tkt-offline-${Date.now()}`
    const fakeTicket: MaintenanceTicket = {
      id: fakeId,
      ticketNumber: `TKT-OFFLINE-${Date.now().toString().slice(-4)}`,
      title: input.title,
      description: input.description,
      stationId: input.stationId,
      stationName: input.stationName,
      controllerId: input.controllerId,
      controllerName: input.controllerName,
      status: 'Open',
      priority: input.priority,
      reportedByUserName: input.reportedByUserName || 'Offline User',
      assignedTechnicianName: input.assignedTechnicianName,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      comments: [],
      attachments: [],
      isOfflinePending: true
    }

    try {
      const db = await this.getDB()
      await db.add(STORE_QUEUE, { action: 'CREATE', payload: input })
      await db.put(STORE_TICKETS, fakeTicket)
    } catch (e) {
      console.warn('Failed to queue offline ticket:', e)
    }

    this.notifyListeners({
      type: 'TicketCreated',
      ticket: fakeTicket,
      ticketId: fakeId,
      timestamp: new Date().toISOString()
    })

    return fakeTicket
  }

  public async updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket> {
    if (this.isOnline) {
      return await this.innerProvider.updateTicket(id, updates)
    }
    const db = await this.getDB()
    const existing = await db.get(STORE_TICKETS, id)
    if (existing) {
      const updated = { ...existing, ...updates, updatedAt: new Date().toISOString() }
      await db.put(STORE_TICKETS, updated)
      return updated
    }
    throw new Error('Ticket not found in offline storage')
  }

  public async updateTicketStatus(id: string, status: TicketStatus, technicianName?: string): Promise<MaintenanceTicket> {
    if (this.isOnline) {
      return await this.innerProvider.updateTicketStatus(id, status, technicianName)
    }

    const db = await this.getDB()
    await db.add(STORE_QUEUE, { action: 'STATUS_UPDATE', payload: { id, status } })
    const existing = await db.get(STORE_TICKETS, id)
    if (existing) {
      existing.status = status
      existing.updatedAt = new Date().toISOString()
      await db.put(STORE_TICKETS, existing)
    }

    this.notifyListeners({
      type: 'StatusChanged',
      ticketId: id,
      status,
      timestamp: new Date().toISOString()
    })

    return existing || ({ id, status } as any)
  }

  public async addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment> {
    if (this.isOnline) {
      return await this.innerProvider.addComment(ticketId, authorName, content)
    }

    const db = await this.getDB()
    await db.add(STORE_QUEUE, { action: 'COMMENT', payload: { ticketId, authorName, content } })
    const comment: TicketComment = {
      id: `c-offline-${Date.now()}`,
      ticketId,
      authorName,
      content,
      createdAt: new Date().toISOString()
    }

    const ticket = await db.get(STORE_TICKETS, ticketId)
    if (ticket) {
      ticket.comments = ticket.comments || []
      ticket.comments.push(comment)
      await db.put(STORE_TICKETS, ticket)
    }

    return comment
  }

  public async getMetrics(): Promise<TicketMetrics> {
    if (this.isOnline) {
      try {
        return await this.innerProvider.getMetrics()
      } catch {
        // Fall back to local calculation
      }
    }
    const tickets = await this.getTickets()
    return {
      totalTickets: tickets.length,
      openCount: tickets.filter(t => t.status === 'Open').length,
      inProgressCount: tickets.filter(t => t.status === 'In_Progress').length,
      pendingPartsCount: tickets.filter(t => t.status === 'Pending_Parts').length,
      resolvedCount: tickets.filter(t => t.status === 'Resolved').length,
      closedCount: tickets.filter(t => t.status === 'Closed').length,
      criticalCount: tickets.filter(t => t.priority === 'Critical').length,
      overdueCount: 0,
      slaCompliancePercent: 100
    }
  }

  public subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void {
    this.eventListeners.add(listener)
    return () => {
      this.eventListeners.delete(listener)
    }
  }
}
