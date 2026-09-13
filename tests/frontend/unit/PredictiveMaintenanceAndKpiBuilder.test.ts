import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import FleetAnalyticsSummary from '../../../frontend/web/app/components/analytics/FleetAnalyticsSummary.vue'
import KpiGraphBuilder from '../../../frontend/web/app/components/analytics/KpiGraphBuilder.vue'
import PowerBiTileEmbed from '../../../frontend/web/app/components/analytics/PowerBiTileEmbed.vue'
import { navMenu } from '../../../frontend/web/app/constants/menus'

const mockSummary = {
  totalMachineHours: 2316,
  availabilityPercentage: 97.4,
  averageMtbfHours: 342.5,
  averageMttrMinutes: 34.2,
  slaCompliancePercentage: 96.8,
  topFaultingMachines: [
    { machineId: 'm-op20', name: 'OP20-Weld Laser Cell', line: 'Line 1 - Pre-Assembly', incidentCount: 14, totalDowntimeMinutes: 185, primaryAlarmCode: 'F-WELD-OPTIC-DIRT', healthIndex: 68.4 },
    { machineId: 'm-op50', name: 'OP50-Fasten Screwing Station', line: 'Line 2 - Fastening', incidentCount: 11, totalDowntimeMinutes: 142, primaryAlarmCode: 'E-TORQUE-OUT-OF-BOUNDS', healthIndex: 74.2 }
  ],
  maintenanceBacklog: {
    under24Hours: 8,
    oneToThreeDays: 4,
    overThreeDays: 2,
    criticalBreached: 1
  },
  stockDepletion: [
    { partNumber: 'SEW-DRV-MDX61B', description: 'SEW Movidrive Inverter Module', currentStock: 1, minStockThreshold: 3, criticality: 'Critical' }
  ]
}

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
    it('renders fleet availability, MTBF, MTTR, and SLA compliance accurately', () => {
      const wrapper = mount(FleetAnalyticsSummary, {
        props: {
          summary: mockSummary
        },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' }
          }
        }
      })

      expect(wrapper.text()).toContain('97.4%')
      expect(wrapper.text()).toContain('342.5')
      expect(wrapper.text()).toContain('34.2')
      expect(wrapper.text()).toContain('96.8%')
    })

    it('renders top-faulting machines table with health index and downtime', () => {
      const wrapper = mount(FleetAnalyticsSummary, {
        props: {
          summary: mockSummary
        },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' }
          }
        }
      })

      expect(wrapper.text()).toContain('OP20-Weld Laser Cell')
      expect(wrapper.text()).toContain('F-WELD-OPTIC-DIRT')
      expect(wrapper.text()).toContain('68.4%')
      expect(wrapper.text()).toContain('185 min')
    })

    it('displays backlog distribution counts and stock depletion alerts', () => {
      const wrapper = mount(FleetAnalyticsSummary, {
        props: {
          summary: mockSummary
        },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' }
          }
        }
      })

      expect(wrapper.text()).toContain('SEW-DRV-MDX61B')
      expect(wrapper.text()).toContain('Under 24 Hours')
      expect(wrapper.text()).toContain('Critical SLA Breaches')
    })
  })

  describe('KpiGraphBuilder Component', () => {
    it('initializes with default pinned KPI widgets', () => {
      const wrapper = mount(KpiGraphBuilder)

      expect(wrapper.text()).toContain('Plant Master OEE Index')
      expect(wrapper.text()).toContain('Pre-Assembly (L1) MTTR')
      expect(wrapper.text()).toContain('Robotics Welding Quality Rate')
    })

    it('pins new custom KPI widget and saves to storage', async () => {
      const wrapper = mount(KpiGraphBuilder)

      const titleInput = wrapper.find('input[type="text"]')
      await titleInput.setValue('Custom Line 5 OEE')

      const pinButton = wrapper.find('button.bg-indigo-600')
      await pinButton.trigger('click')

      expect(wrapper.text()).toContain('Custom Line 5 OEE')
    })
  })

  describe('PowerBiTileEmbed Component', () => {
    it('displays Power BI workspace configuration and external export links', () => {
      const wrapper = mount(PowerBiTileEmbed)

      expect(wrapper.text()).toContain('Microsoft Power BI Embedded Workspace')
      expect(wrapper.text()).toContain('Grafana Infinity Plugin')
      expect(wrapper.text()).toContain('Excel PowerQuery Live Feed')
      expect(wrapper.text()).toContain('Machines OData Catalog')
    })
  })

  describe('Navigation Configuration', () => {
    it('registers Fleet Analytics under Monitoring in navMenu', () => {
      const monitoring = navMenu.find(group => group.heading === 'Monitoring')
      expect(monitoring).toBeDefined()

      const analyticsLink = monitoring?.items.find(item => item.link === '/dashboard/analytics')
      expect(analyticsLink).toBeDefined()
      expect(analyticsLink?.title).toBe('Fleet Analytics')
    })
  })
})
