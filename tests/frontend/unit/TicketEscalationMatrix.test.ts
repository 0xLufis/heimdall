import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import TicketEscalationMatrixModal from '../../../src/frontend/web/app/components/tickets/TicketEscalationMatrixModal.vue'
import TicketKanbanBoard from '../../../src/frontend/web/app/components/tickets/TicketKanbanBoard.vue'
import type { MaintenanceTicket } from '../../../src/frontend/web/app/types/maintenance'

// Mock nuxt auth session composable
vi.mock('../../../src/frontend/web/app/composables/useAuthSession', () => ({
  useAuthSession: () => ({
    user: { value: { id: 'u-1', name: 'Shift Lead Alex', email: 'alex@factory.com', role: 'lead' } }
  })
}))

// Mock technician inheritance
vi.mock('../../../src/frontend/web/app/utils/technicianInheritance', () => ({
  resolvePreferredTechnician: vi.fn((stationId: any) => ({
    technicianName: String(stationId).toLowerCase().includes('cnc') ? 'István Kovács' : 'Sally Engineer',
    source: 'Station Rule',
    sourceLabel: 'Station Rule',
    role: 'Lead Electrical Tech'
  }))
}))

const sampleTicket: MaintenanceTicket = {
  id: 'tick-esc-1',
  ticketNumber: 'TICK-9001',
  title: 'Spindle vibration threshold exceeded',
  description: 'Unusual oscillation observed on axis Z during milling cycle',
  priority: 'high',
  status: 'Open',
  stationId: 'st-cnc-1',
  stationName: 'CNC Mill #1',
  controllerId: 'ctrl-101',
  createdAt: '2026-10-07T10:00:00Z',
  updatedAt: '2026-10-07T10:00:00Z',
  isEscalated: false,
  escalationReason: null,
  tags: ['#CNC', '#Spindle']
}

describe('TicketEscalationMatrixModal.vue', () => {
  it('renders 4 escalation matrix levels with SLAs and descriptions', () => {
    const wrapper = mount(TicketEscalationMatrixModal, {
      props: {
        ticket: sampleTicket,
        open: true
      }
    })

    expect(wrapper.text()).toContain('Incident Escalation Matrix')
    expect(wrapper.text()).toContain('Level 1: Shift Response')
    expect(wrapper.text()).toContain('Level 2: Dedicated Station Engineer')
    expect(wrapper.text()).toContain('Level 3: Plant Operations Management')
    expect(wrapper.text()).toContain('Level 4: OEM / Vendor Specialist')

    expect(wrapper.text()).toContain('SLA: 15m')
    expect(wrapper.text()).toContain('SLA: 30m')
    expect(wrapper.text()).toContain('SLA: 60m')
    expect(wrapper.text()).toContain('SLA: 120m')
  })

  it('auto-resolves dedicated engineer target for the station', async () => {
    const wrapper = mount(TicketEscalationMatrixModal, {
      props: {
        ticket: sampleTicket,
        open: true
      }
    })

    // sampleTicket has station 'st-cnc-1', mock returns 'István Kovács'
    expect(wrapper.text()).toContain('István Kovács')
    expect(wrapper.text()).toContain('Station Rule')
  })

  it('supports switching levels and selecting manual override target', async () => {
    const wrapper = mount(TicketEscalationMatrixModal, {
      props: {
        ticket: sampleTicket,
        open: true
      }
    })

    // Click Level 4 (OEM)
    const levelButtons = wrapper.findAll('button').filter(b => b.text().includes('Level 4'))
    expect(levelButtons.length).toBeGreaterThan(0)
    await levelButtons[0].trigger('click')

    expect(wrapper.text()).toContain('OEM / Vendor Specialist')

    // Switch target mode to manual override
    const overrideButton = wrapper.findAll('button').find(b => b.text().includes('Manual Override'))
    expect(overrideButton).toBeDefined()
    await overrideButton!.trigger('click')

    // Select manual override target from select
    const select = wrapper.find('select')
    expect(select.exists()).toBe(true)
    await select.setValue('Engineer Orwell')

    // Enter reason
    const reasonInput = wrapper.find('input[placeholder*="Robot axis"]')
    expect(reasonInput.exists()).toBe(true)
    await reasonInput.setValue('Spindle bearing complete seizure requiring OEM teardown')

    // Submit escalation
    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Confirm Escalation'))
    expect(submitBtn).toBeDefined()
    await submitBtn!.trigger('click')

    const emitted = wrapper.emitted('escalate')
    expect(emitted).toBeDefined()
    expect(emitted![0][0]).toMatchObject({
      level: 4,
      targetMode: 'manual_override',
      targetName: 'Engineer Orwell',
      reason: 'Spindle bearing complete seizure requiring OEM teardown'
    })
  })

  it('validates reason input before allowing submission', async () => {
    const wrapper = mount(TicketEscalationMatrixModal, {
      props: {
        ticket: { ...sampleTicket, title: '' },
        open: true
      }
    })

    const reasonInput = wrapper.find('input[placeholder*="Robot axis"]')
    await reasonInput.setValue('')

    const submitBtn = wrapper.findAll('button').find(b => b.text().includes('Confirm Escalation'))
    expect(submitBtn?.attributes('disabled')).toBeDefined()
  })
})

describe('TicketKanbanBoard Escalation Integration', () => {
  it('opens matrix modal on card escalate click and handles matrix escalation', async () => {
    const tickets = [{ ...sampleTicket }]
    const wrapper = mount(TicketKanbanBoard, {
      props: {
        tickets
      }
    })

    // Click escalate button on the card
    const escalateBtn = wrapper.findAll('button').find(b => b.attributes('title')?.includes('Escalate') || b.text().includes('Escalate'))
    expect(escalateBtn).toBeDefined()
    await escalateBtn!.trigger('click')

    // Matrix modal should be open
    const matrixModal = wrapper.findComponent(TicketEscalationMatrixModal)
    expect(matrixModal.exists()).toBe(true)
    expect(matrixModal.props('open')).toBe(true)

    // Simulate matrix modal escalate event
    await matrixModal.vm.$emit('escalate', {
      level: 2,
      levelTitle: 'Level 2: Dedicated Station Engineer',
      targetMode: 'auto_dedicated',
      targetName: 'István Kovács',
      handoverState: 'HandOff',
      reason: 'Sensor drift on CNC table'
    })

    // Emitted escalateTicket on board
    expect(wrapper.emitted('escalateTicket')).toBeDefined()
    expect(wrapper.emitted('escalateTicket')![0]).toEqual(['tick-esc-1', 'Sensor drift on CNC table'])

    // Ticket object updated
    expect(tickets[0].isEscalated).toBe(true)
    expect(tickets[0].escalationReason).toBe('Sensor drift on CNC table')
    expect(tickets[0].escalationTarget).toBe('István Kovács (Dedicated)')
    expect(tickets[0].escalationHandoverState).toBe('HandOff')
  })

  it('resolves escalation cleanly from the kanban card', async () => {
    const escalatedTicket: MaintenanceTicket = {
      ...sampleTicket,
      isEscalated: true,
      escalationReason: 'Pending OEM specialist',
      escalationTarget: 'OEM (Override)',
      escalationHandoverState: 'Notification'
    }

    const wrapper = mount(TicketKanbanBoard, {
      props: {
        tickets: [escalatedTicket]
      }
    })

    // Find Resolve Escalation button on the card (title "Resolve this escalation" or text "Clear")
    const resolveBtn = wrapper.findAll('button').find(b => b.attributes('title')?.includes('Resolve this escalation') || b.text().includes('Clear'))
    expect(resolveBtn).toBeDefined()
    await resolveBtn!.trigger('click')

    expect(wrapper.emitted('resolveEscalation')).toBeDefined()
    expect(wrapper.emitted('resolveEscalation')![0]).toEqual(['tick-esc-1'])
    expect(escalatedTicket.isEscalated).toBe(false)
    expect(escalatedTicket.escalationReason).toBeNull()
    expect(escalatedTicket.escalationTarget).toBeNull()
  })
})
