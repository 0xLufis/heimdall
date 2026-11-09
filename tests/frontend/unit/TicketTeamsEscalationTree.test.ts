import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import TicketEscalationTreeDrawer from '~/components/tickets/TicketEscalationTreeDrawer.vue'
import type { MaintenanceTicket } from '~/types/maintenance'

// Mock toast
vi.mock('vue-sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn()
  }
}))

// Mock auth client
vi.mock('~/utils/auth-client', () => ({
  authClient: {
    useSession: () => ({
      data: {
        value: {
          user: { name: 'Lead Specialist Orwell', email: 'lead@heimdall.dev' }
        }
      }
    })
  }
}))

// Mock useMaintenance
const mockEscalateTicket = vi.fn().mockResolvedValue({
  id: 'tkt-tree-1',
  isEscalated: true,
  escalationReason: 'PLC interlock emergency',
  escalationTarget: 'Engineer Sally (Lead Controls & CNC Specialist)',
  escalationHandoverState: 'Notification'
})
const mockFetchTickets = vi.fn().mockResolvedValue([])

vi.mock('~/composables/useMaintenance', () => ({
  useMaintenance: () => ({
    escalateTicket: mockEscalateTicket,
    fetchTickets: mockFetchTickets
  })
}))

describe('Escalation Tree Drawer Unit Tests', () => {
  const sampleTicket: MaintenanceTicket = {
    id: 'tkt-tree-1',
    ticketNumber: 'TKT-2026-0888',
    title: 'TwinCAT 3 EtherCAT Master Communication Loss',
    description: 'Line 02 milling robot cell lost fieldbus master synchronization.',
    status: 'InProgress',
    priority: 'Critical',
    stationId: 'STATION-CNC-02',
    stationName: 'Line 02 CNC Milling Cell',
    isLineStop: true,
    lineStopDurationMinutes: 30,
    reportedByUserName: 'Operator János',
    assignedTechnicianName: 'Gábor Varga',
    createdAt: '2026-10-17T10:00:00Z',
    updatedAt: '2026-10-17T10:30:00Z',
    comments: [],
    attachments: []
  }

  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders right slide-out drawer without blocking modal backdrop when open=true', () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const drawer = wrapper.find('[role="dialog"]')
    expect(drawer.exists()).toBe(true)
    // Verify non-blocking slide-out drawer architecture
    expect(drawer.classes()).toContain('fixed')
    expect(drawer.classes()).toContain('right-0')
    expect(drawer.classes()).toContain('inset-y-0')
    expect(drawer.classes()).not.toContain('inset-0')

    // Header verifies ticket context
    expect(wrapper.text()).toContain('TKT-2026-0888')
    expect(wrapper.text()).toContain('Escalation Dispatcher')
    expect(wrapper.text()).toContain('Line 02 CNC Milling Cell')
  })

  it('does not render any DOM elements when open=false', () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: false,
        ticket: sampleTicket
      }
    })

    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('renders all 4 canonical hierarchy departments and on-call channels', () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const text = wrapper.text()
    // Department Groups
    expect(text).toContain('Plant Operations & Command')
    expect(text).toContain('Engineering & Controls (OT / PLC / Vision)')
    expect(text).toContain('Mechanical Maintenance & Robotics')
    expect(text).toContain('Safety & EHS Critical Response')

    // On-Call Channels with #
    expect(text).toContain('#Incident-Command')
    expect(text).toContain('#Controls-Emergency')
    expect(text).toContain('#Mechanical-Floor-Emergency')
    expect(text).toContain('#Safety-Critical-Stop')
  })

  it('renders presence indicators and Out of Office delegation warning for Engineer Orwell', () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const text = wrapper.text()
    // Available presence
    expect(text).toContain('Available')
    // Out of Office presence and return date
    expect(text).toContain('Out of Office')
    expect(text).toContain('OOO until Oct 20, 07:00 AM')
    expect(text).toContain('Delegate:')
    expect(text).toContain('Gábor Varga')
  })

  it('filters hierarchy tree live via search input', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const searchInput = wrapper.find('input[type="text"]')
    expect(searchInput.exists()).toBe(true)

    // Filter by Cognex / Vision
    await searchInput.setValue('Cognex')
    expect(wrapper.text()).toContain('Katalin Nagy')
    expect(wrapper.text()).not.toContain('Bence Horváth')

    // Filter by channel
    await searchInput.setValue('Safety-Critical')
    expect(wrapper.text()).toContain('#Safety-Critical-Stop')
    expect(wrapper.text()).not.toContain('#Incident-Command')

    // Clear search
    await searchInput.setValue('')
    expect(wrapper.text()).toContain('Plant Operations & Command')
  })

  it('selects a target node and displays the target confirmation summary card', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    // Initially defaults to Engineer Sally
    expect(wrapper.text()).toContain('Target Recipient Selected')

    // Find channel #Incident-Command and click it
    const nodes = wrapper.findAll('.group')
    const commandChannelNode = nodes.find(n => n.text().includes('#Incident-Command'))
    expect(commandChannelNode).toBeDefined()

    await commandChannelNode!.trigger('click')

    // Target summary box should reflect channel selection
    expect(wrapper.text()).toContain('#Incident-Command')
    expect(wrapper.text()).toContain('incident-command@heimdall.plant')
  })

  it('supports selecting different Handover Protocols (Notification, Hand-Off, Parallel Work)', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const handoffOption = wrapper.findAll('.cursor-pointer').find(el => el.text().includes('Hand-Off'))
    expect(handoffOption).toBeDefined()
    await handoffOption!.trigger('click')

    const parallelOption = wrapper.findAll('.cursor-pointer').find(el => el.text().includes('Parallel Work'))
    expect(parallelOption).toBeDefined()
    await parallelOption!.trigger('click')
  })

  it('applies 1-click quick template chips into escalation reason textarea', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    const templateChips = wrapper.findAll('button[type="button"]').filter(b => b.text().includes('...'))
    expect(templateChips.length).toBeGreaterThan(0)

    await templateChips[0].trigger('click')

    const textarea = wrapper.find('textarea')
    expect((textarea.element as HTMLTextAreaElement).value).toContain('PLC / TwinCAT 3 motion interlock unresponsive')
  })

  it('toggles Incident Dispatch Card preview card mockup', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    expect(wrapper.text()).toContain('Incident Dispatch Card Preview')
    const previewToggle = wrapper.findAll('button').find(b => b.text().includes('Incident Dispatch Card Preview'))
    expect(previewToggle).toBeDefined()

    await previewToggle!.trigger('click')

    // Preview body visible
    expect(wrapper.text()).toContain('Heimdall Incident Command')
    expect(wrapper.text()).toContain('Acknowledge & Join Bridge')
    expect(wrapper.text()).toContain('View in Heimdall')
  })

  it('dispatches escalation and emits escalated and close events', async () => {
    const wrapper = mount(TicketEscalationTreeDrawer, {
      props: {
        open: true,
        ticket: sampleTicket
      }
    })

    // Fill reason
    const textarea = wrapper.find('textarea')
    await textarea.setValue('TwinCAT EtherCAT line sync lost on Station OP20.')

    // Submit button
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Dispatch Escalation'))
    expect(submitBtn).toBeDefined()
    expect(submitBtn!.attributes('disabled')).toBeUndefined()

    await submitBtn!.trigger('click')

    // Verify composable called
    expect(mockEscalateTicket).toHaveBeenCalledWith(
      'tkt-tree-1',
      'TwinCAT EtherCAT line sync lost on Station OP20.',
      'Lead Specialist Orwell',
      'Notification',
      expect.stringContaining('Engineer Sally')
    )

    // Verify emits
    expect(wrapper.emitted('escalated')).toBeTruthy()
    expect(wrapper.emitted('close')).toBeTruthy()
  })
})
