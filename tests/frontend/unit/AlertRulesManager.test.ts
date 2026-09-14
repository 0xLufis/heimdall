import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useAlertRules } from '../../../frontend/web/app/composables/useAlertRules'
import AlertRulesManager from '../../../frontend/web/app/components/analytics/AlertRulesManager.vue'

// Mock useMaintenance
const mockCreatedTickets: any[] = []
vi.mock('../../../frontend/web/app/composables/useMaintenance', () => {
  return {
    useMaintenance: () => ({
      createTicket: vi.fn(async (input: any) => {
        const ticket = {
          id: `tkt-mock-${Date.now()}`,
          ticketNumber: `TKT-2026-TEST-${Math.floor(1000 + Math.random() * 9000)}`,
          title: input.title,
          description: input.description,
          status: 'Open',
          priority: input.priority || 'High',
          stationId: input.stationId,
          controllerId: input.controllerId,
          errorCode: input.errorCode,
          errorGroup: input.errorGroup,
          telemetrySnapshot: input.telemetrySnapshot,
          createdAt: new Date().toISOString()
        }
        mockCreatedTickets.push(ticket)
        return ticket
      })
    })
  }
})

describe('AlertRulesManager & useAlertRules Test Suite', () => {
  beforeEach(() => {
    mockCreatedTickets.length = 0
  })

  it('evaluates telemetry sample and triggers alert when threshold is breached', async () => {
    const { evaluateTelemetry, rules } = useAlertRules()

    // Find thermal rule
    const thermalRule = rules.value.find(r => r.id === 'rule-temp-02')
    expect(thermalRule).toBeDefined()
    if (thermalRule) {
      thermalRule.enabled = true
      thermalRule.lastTriggeredAt = undefined // clear cooldown
    }

    // Evaluate sample with high temperature
    const alerts = await evaluateTelemetry({
      targetId: 'm-op20',
      targetName: 'OP20-Weld Laser Cell',
      targetType: 'machine',
      metrics: {
        motor_temp_c: 88.5
      }
    })

    expect(alerts.length).toBeGreaterThanOrEqual(1)
    const thermalAlert = alerts.find(a => a.metricKey === 'motor_temp_c')
    expect(thermalAlert).toBeDefined()
    expect(thermalAlert?.measuredValue).toBe(88.5)
    expect(thermalAlert?.threshold).toBe(72.0)
    expect(thermalAlert?.ticketNumber).toBeDefined()
    expect(mockCreatedTickets.length).toBeGreaterThanOrEqual(1)
  })

  it('suppresses duplicate alert and ticket within cooldown window', async () => {
    const { evaluateTelemetry, rules } = useAlertRules()

    const thermalRule = rules.value.find(r => r.id === 'rule-temp-02')
    if (thermalRule) {
      thermalRule.enabled = true
      thermalRule.lastTriggeredAt = new Date().toISOString() // Just triggered
      thermalRule.cooldownMinutes = 30
    }

    const initialTicketsCount = mockCreatedTickets.length

    // Evaluate again immediately
    const alerts = await evaluateTelemetry({
      targetId: 'm-op20',
      targetName: 'OP20-Weld Laser Cell',
      targetType: 'machine',
      metrics: {
        motor_temp_c: 89.0
      }
    })

    // Should be suppressed by cooldown
    const thermalAlert = alerts.find(a => a.metricKey === 'motor_temp_c')
    expect(thermalAlert).toBeUndefined()
    expect(mockCreatedTickets.length).toBe(initialTicketsCount)
  })

  it('allows acknowledging active alerts', () => {
    const { alerts, acknowledgeAlert } = useAlertRules()
    const active = alerts.value.find(a => a.status === 'active')
    expect(active).toBeDefined()

    if (active) {
      acknowledgeAlert(active.id, 'Test Engineer')
      expect(active.status).toBe('acknowledged')
      expect(active.acknowledgedBy).toBe('Test Engineer')
    }
  })

  it('toggles, creates, and deletes rules', () => {
    const { rules, toggleRule, addRule, deleteRule } = useAlertRules()

    const testRule = addRule({
      name: 'Air Flow Drop Test Rule',
      description: 'Test airflow restriction',
      category: 'pneumatics',
      targetType: 'all',
      metricKey: 'airflow_lpm',
      metricLabel: 'Air Flow Rate',
      condition: '<',
      threshold: 120.0,
      unit: 'L/min',
      severity: 'Medium',
      enabled: true,
      autoCreateTicket: false,
      ticketPriority: 'Medium',
      cooldownMinutes: 10
    })

    expect(rules.value.some(r => r.id === testRule.id)).toBe(true)

    // Toggle rule
    toggleRule(testRule.id)
    const toggled = rules.value.find(r => r.id === testRule.id)
    expect(toggled?.enabled).toBe(false)

    // Delete rule
    deleteRule(testRule.id)
    expect(rules.value.some(r => r.id === testRule.id)).toBe(false)
  })

  it('renders AlertRulesManager component with summary cards and tabs', async () => {
    const wrapper = mount(AlertRulesManager, {
      global: {
        stubs: {
          Dialog: { template: '<div><slot /></div>' },
          DialogContent: { template: '<div><slot /></div>' },
          DialogHeader: { template: '<div><slot /></div>' },
          DialogTitle: { template: '<div><slot /></div>' },
          DialogDescription: { template: '<div><slot /></div>' },
          Card: { template: '<div><slot /></div>' },
          Badge: { template: '<span><slot /></span>' },
          Button: { template: '<button><slot /></button>' }
        }
      }
    })

    expect(wrapper.text()).toContain('Active Rule Alerts')
    expect(wrapper.text()).toContain('Configured Rules')
    expect(wrapper.text()).toContain('Auto-Dispatched Tickets')
    expect(wrapper.text()).toContain('Rule Engine Engine State')

    // Check tabs
    expect(wrapper.text()).toContain('Active Alerts')
    expect(wrapper.text()).toContain('Rule Catalog')
    expect(wrapper.text()).toContain('Trigger Sandbox')
    expect(wrapper.text()).toContain('Auto-Ticket Log')

    // Switch to Sandbox tab
    const sandboxTab = wrapper.findAll('button').find(b => b.text().includes('Trigger Sandbox'))
    expect(sandboxTab).toBeDefined()
    await sandboxTab?.trigger('click')

    expect(wrapper.text()).toContain('Industrial Telemetry Sandbox & Rule Breaker')
    expect(wrapper.text()).toContain('Thermal Spike')
    expect(wrapper.text()).toContain('Soft-PLC Jitter')
    expect(wrapper.text()).toContain('Pressure Sag')
  })
})
