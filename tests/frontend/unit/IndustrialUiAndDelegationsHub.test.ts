import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import ComponentPartHistoryDrawer from '../../../src/frontend/web/app/components/machines/ComponentPartHistoryDrawer.vue'
import AssignPartModal from '../../../src/frontend/web/app/components/machines/AssignPartModal.vue'
import TicketTelemetryProbeVisualizer from '../../../src/frontend/web/app/components/tickets/TicketTelemetryProbeVisualizer.vue'
import DelegationTestResolverModal from '../../../src/frontend/web/app/components/delegations/DelegationTestResolverModal.vue'
import { componentsAndDelegationsStore } from '../../../src/frontend/web/server/utils/componentsAndDelegationsStore'
import type { TicketTelemetryProbe } from '../../../src/frontend/web/app/types/maintenance'

vi.stubGlobal('useAuthSession', () => ({
  user: { value: { id: 'usr-orwell', name: 'Engineer Orwell', email: 'orwell@factory.corp' } },
  userRole: { value: 'system_admin' },
  dedicationTier: { value: 'manager' }
}))

describe('Industrial Data Model UI & Delegations Hub', () => {
  beforeEach(() => {
    componentsAndDelegationsStore.reset()
  })

  describe('Components & Delegations Store Operations', () => {
    it('retrieves seeded recipes, skills catalog, and personnel qualifications', () => {
      const recipes = componentsAndDelegationsStore.getDelegationRecipes()
      expect(recipes.length).toBeGreaterThanOrEqual(4)
      expect(recipes.some(r => r.discipline === 'Safety')).toBe(true)
      expect(recipes.some(r => r.discipline === 'Controls')).toBe(true)

      const skills = componentsAndDelegationsStore.getSkillsCatalog()
      expect(skills.length).toBeGreaterThanOrEqual(4)
      expect(skills.some(s => s.code === 'CERT-LASER-4')).toBe(true)
      expect(skills.some(s => s.code === 'SKILL-TWINCAT3')).toBe(true)

      const personnelSkills = componentsAndDelegationsStore.getPersonnelSkills()
      expect(personnelSkills.length).toBeGreaterThanOrEqual(4)
      expect(personnelSkills.some(ps => ps.skillCode === 'CERT-LASER-4')).toBe(true)

      const groups = componentsAndDelegationsStore.getPersonnelGroups()
      expect(groups.length).toBeGreaterThanOrEqual(4)
      expect(groups.some(g => g.groupType === 'ShiftCrew')).toBe(true)
      expect(groups.some(g => g.groupType === 'OnCallRoster')).toBe(true)
    })

    it('resolves responder dynamically based on bound recipes, skills, and shift vs on-call schedule', () => {
      // OP20 machine bound to Safety (Laser) and Controls (TwinCAT)
      const safetyShift = componentsAndDelegationsStore.resolveResponder(
        '22222222-2222-2222-2222-222222222222',
        'Safety',
        'Shift'
      )
      expect(safetyShift.assigneeId).toBe('usr-orwell')
      expect(safetyShift.isOnCallFallback).toBe(false)
      expect(safetyShift.reason).toContain('CERT-LASER-4')

      // Off-hours on-call resolution
      const safetyOnCall = componentsAndDelegationsStore.resolveResponder(
        '22222222-2222-2222-2222-222222222222',
        'Safety',
        'OnCall'
      )
      expect(safetyOnCall.assigneeId).toBe('usr-dr-standby')
      expect(safetyOnCall.isOnCallFallback).toBe(true)
      expect(safetyOnCall.reason.toLowerCase()).toContain('on-call')
    })

    it('creates and deletes personnel groups and skills dynamically', () => {
      const createdGroup = componentsAndDelegationsStore.createPersonnelGroup({
        name: 'Night Shift Rapid Response',
        code: 'GRP-NIGHT-RESP',
        groupType: 'ShiftCrew',
        discipline: 'Mechanical',
        shiftPattern: 'Night',
        memberUserIds: ['usr-alex'],
        memberUserNames: ['Technician Alex']
      })
      expect(createdGroup.id).toBeDefined()
      expect(componentsAndDelegationsStore.getPersonnelGroups().some(g => g.id === createdGroup.id)).toBe(true)

      const deleted = componentsAndDelegationsStore.deletePersonnelGroup(createdGroup.id)
      expect(deleted).toBe(true)
      expect(componentsAndDelegationsStore.getPersonnelGroups().some(g => g.id === createdGroup.id)).toBe(false)
    })
  })

  describe('TicketTelemetryProbeVisualizer Component', () => {
    it('renders telemetry probes, sparkline buffer, and nominal range indicators', () => {
      const probes: TicketTelemetryProbe[] = [
        {
          probeId: 'probe-hyd-01',
          datapointKey: 'press_tonnage_force_kn',
          metricName: 'Hydraulic Pressing Tonnage',
          unit: 'kN',
          nominalLow: 100,
          nominalHigh: 200,
          numericValue: 245.5,
          triggerStatus: 'ThresholdExceeded',
          sampledAt: '2026-10-10T14:32:00Z',
          pointsBuffer: [
            { timestamp: '2026-10-10T14:31:00Z', value: 150 },
            { timestamp: '2026-10-10T14:31:30Z', value: 180 },
            { timestamp: '2026-10-10T14:32:00Z', value: 245.5 }
          ]
        }
      ]

      const wrapper = mount(TicketTelemetryProbeVisualizer, {
        props: {
          probes,
          alarmCode: 'ALM-HYD-OVERPRESSURE',
          controllerSource: 'TwinCAT-NC-Axis1',
          targetComponentBmk: '=LINE1+OP20-HYD1'
        }
      })

      expect(wrapper.text()).toContain('Hydraulic Pressing Tonnage')
      expect(wrapper.text()).toContain('245.50')
      expect(wrapper.text()).toContain('kN')
      expect(wrapper.text()).toContain('Threshold Exceeded')
      expect(wrapper.text()).toContain('ALM-HYD-OVERPRESSURE')
      expect(wrapper.text()).toContain('=LINE1+OP20-HYD1')
    })
  })

  describe('ComponentPartHistoryDrawer Component', () => {
    it('renders parts replacement lineage drawer with swap reason and disposition', async () => {
      vi.stubGlobal('$fetch', vi.fn().mockResolvedValue([
        {
          id: 'hist-1',
          componentId: 'comp-slot-1',
          swappedAt: '2026-09-10T10:00:00Z',
          swappedByUserName: 'Engineer Orwell',
          previousPartSerialNumber: 'SN-OLD-001',
          newPartSerialNumber: 'SN-NEW-002',
          swapReason: 'WearAndTear',
          previousPartDisposition: 'SentForRepair',
          notes: 'Laser lens degradation spatter contamination'
        }
      ]))

      const mockSlot = {
        id: 'comp-slot-1',
        machineId: 'm-1',
        functionalSlotIdentifier: '=LINE1+OP20-OPT1',
        bmkTag: '-1B1',
        name: 'Laser Focus Collimator Head',
        componentType: 'Hardware',
        currentPartSerialNumber: 'SN-NEW-002',
        status: 'Commissioned'
      }

      const wrapper = mount(ComponentPartHistoryDrawer, {
        props: {
          open: true,
          slot: mockSlot as any
        }
      })

      // Wait for fetch
      await new Promise(r => setTimeout(r, 10))

      expect(wrapper.text()).toContain('Component Lineage Ledger')
      expect(wrapper.text()).toContain('=LINE1+OP20-OPT1')
      expect(wrapper.text()).toContain('SN-OLD-001')
      expect(wrapper.text()).toContain('SN-NEW-002')
      expect(wrapper.text()).toContain('SentForRepair')
    })
  })

  describe('AssignPartModal Component', () => {
    it('renders assign part modal with EPLAN/CAD reference and inventory stock selection', () => {
      const mockSlot = {
        id: 'comp-slot-1',
        machineId: 'm-1',
        functionalSlotIdentifier: '=LINE1+OP20-OPT1',
        bmkTag: '-1B1',
        name: 'Laser Focus Collimator Head',
        componentType: 'Hardware',
        currentPartSerialNumber: 'SN-OLD-001',
        status: 'Commissioned'
      }

      const wrapper = mount(AssignPartModal, {
        props: {
          open: true,
          machineId: 'm-1',
          slot: mockSlot as any
        }
      })

      expect(wrapper.text()).toContain('Commission Component Slot')
      expect(wrapper.text()).toContain('Functional Slot Identifier')
      expect(wrapper.text()).toContain('Swap Justification / Reason')
      expect(wrapper.text()).toContain('Removed Part Disposition')
      expect(wrapper.text()).toContain('EPLAN Schematics & CAD Layout Mapping')
    })
  })

  describe('DelegationTestResolverModal Component', () => {
    it('renders dispatch resolver simulator modal with schedule profiles', () => {
      const wrapper = mount(DelegationTestResolverModal, {
        props: {
          open: true,
          machines: [
            { id: '22222222-2222-2222-2222-222222222222', name: 'OP20-Weld', customIdentifier: 'LINE1-OP20' }
          ]
        }
      })

      expect(wrapper.text()).toContain('Delegation Recipe Router Simulator')
      expect(wrapper.text()).toContain('Target Machine')
      expect(wrapper.text()).toContain('Discipline Context')
      expect(wrapper.text()).toContain('Active Shift Roster')
      expect(wrapper.text()).toContain('On-Call Off-Hours')
    })
  })
})
