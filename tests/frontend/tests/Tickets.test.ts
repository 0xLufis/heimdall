import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import TicketMetricsOverview from '~/components/tickets/TicketMetricsOverview.vue'
import TicketList from '~/components/tickets/TicketList.vue'
import TicketCreateModal from '~/components/tickets/TicketCreateModal.vue'
import TicketDetailDrawer from '~/components/tickets/TicketDetailDrawer.vue'
import type { MaintenanceTicket } from '~/server/utils/ticketsStore'

describe('Maintenance Ticketing UI Components', () => {
  const mockMetrics = {
    totalTickets: 10,
    openCount: 3,
    inProgressCount: 2,
    pendingPartsCount: 1,
    resolvedCount: 3,
    closedCount: 1,
    criticalCount: 2,
    overdueCount: 1,
    slaCompliancePercent: 90
  }

  const mockTickets: MaintenanceTicket[] = [
    {
      id: 'tkt-test-1',
      ticketNumber: 'TKT-2026-0001',
      stationId: 'STATION-OP10-01',
      stationName: 'OP10 Machining Cell',
      title: 'Spindle Bearing Overheating',
      description: 'Thermal sensor reported high temperature',
      status: 'Open',
      priority: 'Critical',
      reportedByUserId: 'usr-1',
      reportedByUserName: 'Operator One',
      assignedTechnicianName: 'Gábor Varga',
      createdAt: '2026-08-30T00:00:00Z',
      updatedAt: '2026-08-30T00:00:00Z',
      slaDueAt: '2026-08-30T04:00:00Z',
      comments: [],
      attachments: []
    },
    {
      id: 'tkt-test-2',
      ticketNumber: 'TKT-2026-0002',
      stationId: 'STATION-OP20-02',
      stationName: 'OP20 Robotic Station',
      title: 'KUKA Servo Alarm',
      description: 'Axis 3 divergence error',
      status: 'In_Progress',
      priority: 'High',
      reportedByUserId: 'usr-2',
      reportedByUserName: 'Operator Two',
      assignedTechnicianName: 'Zoltán Németh',
      createdAt: '2026-08-30T01:00:00Z',
      updatedAt: '2026-08-30T01:00:00Z',
      slaDueAt: '2026-08-30T09:00:00Z',
      comments: [],
      attachments: []
    }
  ]

  it('renders TicketMetricsOverview header stats accurately', () => {
    const wrapper = mount(TicketMetricsOverview, {
      props: { metrics: mockMetrics }
    })

    expect(wrapper.text()).toContain('Critical')
    expect(wrapper.text()).toContain('Pending Parts')
    expect(wrapper.text()).toContain('90%')
  })

  it('renders TicketList rows and priority badges', () => {
    const wrapper = mount(TicketList, {
      props: { tickets: mockTickets },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          Icon: { template: '<span></span>' }
        }
      }
    })

    expect(wrapper.text()).toContain('TKT-2026-0001')
    expect(wrapper.text()).toContain('Spindle Bearing Overheating')
    expect(wrapper.text()).toContain('CRITICAL')
    expect(wrapper.text()).toContain('TKT-2026-0002')
    expect(wrapper.text()).toContain('HIGH')
  })

  it('emits selectTicket event when a ticket row is clicked in TicketList', async () => {
    const wrapper = mount(TicketList, {
      props: { tickets: mockTickets },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          Icon: { template: '<span></span>' }
        }
      }
    })

    const rows = wrapper.findAll('tbody tr')
    expect(rows.length).toBe(2)
    await rows[0].trigger('click')

    expect(wrapper.emitted('selectTicket')).toBeTruthy()
    expect(wrapper.emitted('selectTicket')![0][0]).toEqual(mockTickets[0])
  })

  it('renders TicketCreateModal and handles priority selection', async () => {
    const wrapper = mount(TicketCreateModal, {
      props: { open: true },
      global: {
        stubs: {
          QrScanner: { template: '<div class="stub-qr-scanner"></div>' }
        }
      }
    })

    expect(wrapper.text()).toContain('Report Maintenance Ticket')
    expect(wrapper.text()).toContain('Priority Level')

    // Find Critical priority button
    const buttons = wrapper.findAll('button')
    const criticalBtn = buttons.find(b => b.text().includes('Critical'))
    expect(criticalBtn).toBeDefined()
  })

  it('renders TicketDetailDrawer with incident details and status workflow actions', () => {
    const wrapper = mount(TicketDetailDrawer, {
      props: { ticket: mockTickets[0], open: true }
    })

    expect(wrapper.text()).toContain('TKT-2026-0001')
    expect(wrapper.text()).toContain('OP10 Machining Cell')
    expect(wrapper.text()).toContain('Start Work')
  })
})
