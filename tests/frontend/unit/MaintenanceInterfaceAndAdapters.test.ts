import { describe, it, expect, vi, beforeEach } from 'vitest'
import { RestFallbackMaintenanceProvider } from '~/services/maintenance/RestFallbackMaintenanceProvider'
import { ExternalEnterpriseMaintenanceAdapter } from '~/services/maintenance/ExternalEnterpriseMaintenanceAdapter'
import type { IMaintenanceService } from '~/types/maintenance'

describe('IMaintenanceService & Provider Architecture', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string, opts?: any) => {
      if (url.includes('/api/proxy/MaintenanceTicket')) {
        if (opts?.method === 'POST') {
          return Promise.resolve({
            id: 'tkt-new-1',
            title: opts.body.title,
            description: opts.body.description,
            priority: opts.body.priority,
            status: 'Open',
            createdAt: new Date().toISOString()
          })
        }
        return Promise.resolve([
          {
            id: 'tkt-1',
            ticketNumber: 'TKT-001',
            title: 'Spindle Bearing Overheating',
            status: 'Open',
            priority: 'Critical',
            machine: { name: 'OP10 Cell' },
            createdAt: new Date().toISOString()
          },
          {
            id: 'tkt-2',
            ticketNumber: 'TKT-002',
            title: 'KUKA Robot Axis Fault',
            status: 'In_Progress',
            priority: 'High',
            machine: { name: 'OP20 Robot' },
            createdAt: new Date().toISOString()
          }
        ])
      }
      return Promise.resolve([])
    }))
  })

  it('implements IMaintenanceService contract in RestFallbackMaintenanceProvider', async () => {
    const provider: IMaintenanceService = new RestFallbackMaintenanceProvider()
    const tickets = await provider.getTickets()

    expect(tickets.length).toBe(2)
    expect(tickets[0].id).toBe('tkt-1')
    expect(tickets[0].priority).toBe('Critical')
  })

  it('creates maintenance ticket using provider interface', async () => {
    const provider: IMaintenanceService = new RestFallbackMaintenanceProvider()
    const created = await provider.createTicket({
      title: 'Conveyor Jam',
      priority: 'High',
      stationId: 'OP40'
    })

    expect(created.id).toBe('tkt-new-1')
    expect(created.title).toBe('Conveyor Jam')
    expect(created.status).toBe('Open')
  })

  it('calculates metrics from live ticket data', async () => {
    const provider: IMaintenanceService = new RestFallbackMaintenanceProvider()
    const metrics = await provider.getMetrics()

    expect(metrics.totalTickets).toBe(2)
    expect(metrics.openCount).toBe(1)
    expect(metrics.inProgressCount).toBe(1)
    expect(metrics.criticalCount).toBe(1)
  })

  it('supports pluggable ExternalEnterpriseMaintenanceAdapter', async () => {
    const enterpriseAdapter: IMaintenanceService = new ExternalEnterpriseMaintenanceAdapter({
      systemType: 'SAP_PM',
      endpointUrl: 'https://sap.enterprise.corp/api/pm'
    })

    const tkt = await enterpriseAdapter.createTicket({
      title: 'SAP Work Order Trigger',
      priority: 'Medium'
    })

    expect(tkt.ticketNumber).toContain('EXT-')
  })
})
