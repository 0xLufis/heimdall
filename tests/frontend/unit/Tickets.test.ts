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

  it('supports interactive filter clicking and toggling on TicketMetricsOverview', async () => {
    const wrapper = mount(TicketMetricsOverview, {
      props: { metrics: mockMetrics, activeFilter: null }
    })

    const cards = wrapper.findAll('[role="button"]')
    expect(cards.length).toBe(6)

    // Click Critical card (2nd card)
    await cards[1].trigger('click')
    expect(wrapper.emitted('filter-change')).toBeTruthy()
    expect(wrapper.emitted('filter-change')![0]).toEqual(['critical'])

    // When activeFilter is already 'critical', clicking it emits null to toggle off
    await wrapper.setProps({ activeFilter: 'critical' })
    expect(cards[1].classes()).toContain('ring-2')
    expect(cards[1].classes()).toContain('ring-rose-500')

    await cards[1].trigger('click')
    expect(wrapper.emitted('filter-change')![1]).toEqual([null])
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

  it('renders TicketKanbanBoard with drag/drop targets and quick status buttons', async () => {
    const { default: TicketKanbanBoard } = await import('~/components/tickets/TicketKanbanBoard.vue')
    const wrapper = mount(TicketKanbanBoard, {
      props: { tickets: mockTickets }
    })

    expect(wrapper.text()).toContain('Open')
    expect(wrapper.text()).toContain('In Progress')
    expect(wrapper.text()).toContain('TKT-2026-0001')
    expect(wrapper.text()).toContain('Start')

    // Find and click the quick move button
    const buttons = wrapper.findAll('button')
    const startBtn = buttons.find(b => b.text().includes('Start'))
    expect(startBtn).toBeDefined()
    await startBtn!.trigger('click')

    expect(wrapper.emitted('moveStatus')).toBeTruthy()
    expect(wrapper.emitted('moveStatus')![0]).toEqual(['tkt-test-1', 'In_Progress'])
  })

  it('useMaintenance handles atomic on-demand live events and recalibrates metrics', async () => {
    const { useMaintenance } = await import('~/composables/useMaintenance')
    const { tickets, metrics, handleLiveEvent, recalculateMetrics } = useMaintenance()

    // Simulate initial ticket list
    tickets.value = [...mockTickets]
    recalculateMetrics()

    expect(metrics.value?.openTickets).toBe(1)
    expect(metrics.value?.inProgressTickets).toBe(1)

    // Simulate on-demand StatusChanged live push event
    handleLiveEvent({
      type: 'StatusChanged',
      ticketId: 'tkt-test-1',
      status: 'Resolved',
      timestamp: new Date().toISOString()
    })

    const updated = tickets.value.find(t => t.id === 'tkt-test-1')
    expect(updated?.status).toBe('Resolved')
    expect(metrics.value?.openTickets).toBe(0)
    expect(metrics.value?.resolvedToday).toBe(1)
  })

  it('supports 3-state asc-desc-restore sorting on TicketList table headers', async () => {
    const wrapper = mount(TicketList, {
      props: { tickets: mockTickets },
      global: {
        stubs: {
          NuxtLink: { template: '<a><slot /></a>' },
          Icon: { template: '<span></span>' }
        }
      }
    })

    const headers = wrapper.findAll('th')
    // Find Priority header
    const priorityHeader = headers.find(h => h.text().includes('Priority'))
    expect(priorityHeader).toBeDefined()
    expect(priorityHeader?.attributes('aria-sort')).toBe('none')

    // Initially: TKT-1 (Critical) then TKT-2 (High)
    let rows = wrapper.findAll('tbody tr')
    expect(rows[0].text()).toContain('TKT-2026-0001')
    expect(rows[1].text()).toContain('TKT-2026-0002')

    // Click 1: ASC -> Low/Medium/High first (TKT-2 High before TKT-1 Critical)
    await priorityHeader!.trigger('click')
    expect(priorityHeader?.attributes('aria-sort')).toBe('ascending')
    rows = wrapper.findAll('tbody tr')
    expect(rows[0].text()).toContain('TKT-2026-0002')
    expect(rows[1].text()).toContain('TKT-2026-0001')

    // Click 2: DESC -> Critical first (TKT-1 Critical before TKT-2 High)
    await priorityHeader!.trigger('click')
    expect(priorityHeader?.attributes('aria-sort')).toBe('descending')
    rows = wrapper.findAll('tbody tr')
    expect(rows[0].text()).toContain('TKT-2026-0001')
    expect(rows[1].text()).toContain('TKT-2026-0002')

    // Click 3: RESTORE -> returns to natural original order!
    await priorityHeader!.trigger('click')
    expect(priorityHeader?.attributes('aria-sort')).toBe('none')
    rows = wrapper.findAll('tbody tr')
    expect(rows[0].text()).toContain('TKT-2026-0001')
    expect(rows[1].text()).toContain('TKT-2026-0002')
  })

  it('renders PreferredTechniciansModal with a scrollable delegation pop-up card', async () => {
    // Provide $fetch mock if not defined
    if (!globalThis.$fetch) {
      globalThis.$fetch = vi.fn().mockResolvedValue([]) as any
    }

    const { default: PreferredTechniciansModal } = await import('~/components/tickets/PreferredTechniciansModal.vue')
    const wrapper = mount(PreferredTechniciansModal, {
      props: { open: true },
      global: {
        stubs: {
          DialogPortal: { template: '<div class="portal-stub"><slot /></div>' },
          SearchableTargetCombobox: { template: '<div></div>' },
          RbacTooltip: { template: '<div><slot /></div>' },
          Select: { template: '<div><slot /></div>' },
          SelectTrigger: { template: '<div><slot /></div>' },
          SelectValue: { template: '<div><slot /></div>' },
          SelectContent: { template: '<div><slot /></div>' },
          SelectItem: { template: '<div><slot /></div>' }
        }
      }
    })

    // Verify dialog content has overflow-y-auto making the whole pop-up card scrollable
    const dialogContent = wrapper.find('[data-slot="dialog-content"]')
    expect(dialogContent.exists()).toBe(true)
    expect(dialogContent.classes()).toContain('overflow-y-auto')
    expect(dialogContent.classes()).toContain('max-h-[90vh]')

    // Verify DialogHeader is sticky
    const header = wrapper.find('header, [data-slot="dialog-header"]')
    expect(header.exists()).toBe(true)
    expect(header.classes()).toContain('sticky')
    expect(header.classes()).toContain('top-0')
  })
})
