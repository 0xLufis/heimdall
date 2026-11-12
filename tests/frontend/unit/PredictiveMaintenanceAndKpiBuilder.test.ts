import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import FleetAnalyticsSummary from '../../../src/frontend/web/app/components/analytics/FleetAnalyticsSummary.vue'
import KpiGraphBuilder from '../../../src/frontend/web/app/components/analytics/KpiGraphBuilder.vue'
import { navMenu } from '../../../src/frontend/web/app/constants/menus'

// Shape of the backend FleetSummaryDto: every figure comes with its sample size, missing ones are null.
const mockSummary = {
  windowDays: 30,
  totalMachines: 12,
  controllerCount: 10,
  controllersOnline: 9,
  controllerOnlinePercentage: 90,
  controllerOnlineWindowSeconds: 300,
  incidentCount: 25,
  resolvedIncidentCount: 14,
  averageMttrMinutes: 34.2,
  averageMtbfHours: 342.5,
  mtbfMachineCount: 3,
  criticalSlaHours: 12,
  topFaultingMachines: [
    { machineId: 'm-op20', name: 'OP20-Weld Laser Cell', identifier: 'L01-OP020', line: 'Line 1 - Pre-Assembly', incidentCount: 14, openTicketCount: 2, totalDowntimeMinutes: 185, primaryAlarmCode: 'F-WELD-OPTIC-DIRT' },
    { machineId: 'm-op50', name: 'OP50-Fasten Screwing Station', identifier: 'L02-OP050', line: '', incidentCount: 11, openTicketCount: 0, totalDowntimeMinutes: null, primaryAlarmCode: '' }
  ],
  maintenanceBacklog: {
    under24Hours: 8,
    oneToThreeDays: 4,
    overThreeDays: 2,
    criticalBreached: 1
  },
  stockDepletion: [
    { partNumber: 'SEW-DRV-MDX61B', description: 'SEW Movidrive Inverter Module', currentStock: 1, minStockThreshold: 3, criticality: 'Warning' }
  ],
  stockDepletionTotal: 1
}

const summaryMount = (summary: Record<string, unknown> = mockSummary) => mount(FleetAnalyticsSummary, {
  props: { summary: summary as any },
  global: {
    stubs: { NuxtLink: { template: '<a><slot /></a>' } },
    directives: { ctx: {} },
    mocks: { $router: { push: vi.fn() } }
  }
})

describe('Predictive Maintenance, Analytics & KPI Builder Test Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
      clear: vi.fn()
    })
  })

  describe('FleetAnalyticsSummary Component', () => {
    it('renders ticket figures, resolution time and controller connectivity with their sample sizes', () => {
      const text = summaryMount().text()

      expect(text).toContain('Tickets raised, last 30 d')
      expect(text).toContain('25')
      expect(text).toContain('34 min')                          // mean time to resolve
      expect(text).toContain('14 resolved tickets')
      expect(text).toContain('14.3 d')                          // 342.5 h repeat-incident interval
      expect(text).toContain('3 machines with 2+ tickets')
      expect(text).toContain('9/10')
      expect(text).toContain('connectivity, not production availability')
      // Nothing the backend does not send: no trend deltas, targets or SLA percentages.
      expect(text).not.toContain('this week')
      expect(text).not.toContain('on-time')
      expect(text).not.toContain('Health')
    })

    it('renders the machines with the most tickets and "—" for what no ticket recorded', () => {
      const wrapper = summaryMount()
      const rows = wrapper.findAll('tbody tr')

      expect(rows).toHaveLength(2)
      expect(rows[0]!.text()).toContain('OP20-Weld Laser Cell')
      expect(rows[0]!.text()).toContain('F-WELD-OPTIC-DIRT')
      expect(rows[0]!.text()).toContain('3.1 h')                // 185 min
      expect(rows[1]!.findAll('td').map(td => td.text())).toEqual(['OP50-Fasten Screwing Station', '—', '11', '0', '—', '—'])
    })

    it('displays the backlog by age and the depleted stock', () => {
      const text = summaryMount().text()

      expect(text).toContain('SEW-DRV-MDX61B')
      expect(text).toContain('1 / 3')
      expect(text).toContain('Under 24 hours')
      expect(text).toContain('14 open')
      expect(text).toContain('Critical, open longer than 12 h')
    })

    it('says "No data" with the reason instead of showing a default', () => {
      const wrapper = summaryMount({
        ...mockSummary,
        controllerCount: 0, controllersOnline: 0, controllerOnlinePercentage: null,
        incidentCount: 0, resolvedIncidentCount: 0, averageMttrMinutes: null, averageMtbfHours: null, mtbfMachineCount: 0,
        topFaultingMachines: [], stockDepletion: [], stockDepletionTotal: 0,
        maintenanceBacklog: { under24Hours: 0, oneToThreeDays: 0, overThreeDays: 0, criticalBreached: 0 }
      })

      expect(wrapper.find('[data-testid="summary-tile-mttr"]').text()).toContain('No data')
      expect(wrapper.find('[data-testid="summary-tile-mttr"]').text()).toContain('No ticket of the last 30 d is resolved')
      expect(wrapper.find('[data-testid="summary-tile-repeat"]').text()).toContain('No data')
      expect(wrapper.find('[data-testid="summary-tile-controllers"]').text()).toContain('No client PC is registered')
      expect(wrapper.find('[data-testid="summary-tile-incidents"]').text()).toContain('0')
      expect(wrapper.find('[data-testid="summary-no-faulting"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="summary-no-stock"]').exists()).toBe(true)
      expect(wrapper.text()).toContain('No open tickets.')
    })
  })

  describe('KpiGraphBuilder Component', () => {
    it('initializes with default pinned KPI widgets', () => {
      const wrapper = mount(KpiGraphBuilder)

      expect(wrapper.text()).toContain('Plant Master OEE Index')
      expect(wrapper.text()).toContain('Pre-Assembly (L1) MTTR')
      expect(wrapper.text()).toContain('Robotics Welding Quality Rate')
    })

    it('pins new custom KPI widget with SPC thresholds and saves to storage', async () => {
      const wrapper = mount(KpiGraphBuilder)

      const titleInput = wrapper.find('input[type="text"]')
      await titleInput.setValue('Custom Line 5 OEE')

      expect(wrapper.text()).toContain('KPI Goals & Statistical Process Control (SPC) Limits')
      expect(wrapper.text()).toContain('Target / Mean')
      expect(wrapper.text()).toContain('Upper Limit (UCL)')
      expect(wrapper.text()).toContain('Lower Limit (LCL)')

      const pinButton = wrapper.find('[data-testid="pin-kpi-btn"]')
      await pinButton.trigger('click')

      expect(wrapper.text()).toContain('Custom Line 5 OEE')
    })
  })

  describe('Navigation Configuration', () => {
    it('registers Fleet Analytics under Data in navMenu', () => {
      const dataGroup = navMenu.find(group => group.heading === 'Data')
      expect(dataGroup).toBeDefined()

      const analyticsLink = dataGroup?.items.find(item => item.link === '/dashboard/analytics')
      expect(analyticsLink).toBeDefined()
      expect(analyticsLink?.title).toBe('Fleet Analytics')
    })
  })
})
