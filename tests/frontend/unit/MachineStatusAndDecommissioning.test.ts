import { describe, it, expect, beforeEach } from 'vitest'
import { machinesStore } from '../../../frontend/web/server/utils/machinesStore'
import { partsInventoryStore } from '../../../frontend/web/server/utils/partsInventoryStore'

describe('Machine Operational Status & Decommissioning (docs/TODO/machines.TODO.md)', () => {
  const actor = {
    id: 'u-eng-01',
    name: 'Lead Controls Engineer'
  }

  beforeEach(() => {
    machinesStore.reset()
    partsInventoryStore.resetToSeed()
  })

  // -------------------------------------------------------------------------
  // 1. Machine Operational Status Hierarchy
  // -------------------------------------------------------------------------
  describe('Machine Operational Statuses and Filtering', () => {
    it('seeds machines with diverse operational statuses: active, inactive, maintenance, decommissioned', () => {
      const all = machinesStore.getAllMachines({ includeDecommissioned: true })
      expect(all.length).toBeGreaterThanOrEqual(7)

      const statuses = new Set(all.map(m => m.status))
      expect(statuses.has('active')).toBe(true)
      expect(statuses.has('inactive')).toBe(true)
      expect(statuses.has('maintenance')).toBe(true)
      expect(statuses.has('decommissioned')).toBe(true)
    })

    it('defaults to active floor view by excluding decommissioned and dismantled machines', () => {
      const activeFloor = machinesStore.getAllMachines()
      expect(activeFloor.every(m => m.status !== 'decommissioned' && m.status !== 'dismantled')).toBe(true)

      const all = machinesStore.getAllMachines({ includeDecommissioned: true })
      expect(all.length).toBeGreaterThan(activeFloor.length)
      expect(all.some(m => m.status === 'decommissioned')).toBe(true)
    })

    it('filters machines accurately by specific status', () => {
      const maintenanceMachines = machinesStore.getAllMachines({ status: 'maintenance' })
      expect(maintenanceMachines.length).toBeGreaterThan(0)
      expect(maintenanceMachines.every(m => m.status === 'maintenance')).toBe(true)

      const decommissionedMachines = machinesStore.getAllMachines({ status: 'decommissioned' })
      expect(decommissionedMachines.length).toBeGreaterThan(0)
      expect(decommissionedMachines.every(m => m.status === 'decommissioned')).toBe(true)
    })

    it('updates operational status between valid floor states', () => {
      const machineId = '11111111-1111-1111-1111-111111111111'
      const updated = machinesStore.updateMachineStatus(machineId, 'maintenance', actor)
      expect(updated.status).toBe('maintenance')

      const fetched = machinesStore.getMachineById(machineId)
      expect(fetched?.status).toBe('maintenance')

      // Can transition to inactive
      const inactive = machinesStore.updateMachineStatus(machineId, 'inactive', actor)
      expect(inactive.status).toBe('inactive')

      // Can transition back to active
      const active = machinesStore.updateMachineStatus(machineId, 'active', actor)
      expect(active.status).toBe('active')
    })

    it('throws error when attempting to update status of non-existent machine', () => {
      expect(() => {
        machinesStore.updateMachineStatus('non-existent-id', 'active', actor)
      }).toThrow('Machine with ID "non-existent-id" was not found.')
    })
  })

  // -------------------------------------------------------------------------
  // 2. Machine Decommissioning & Dismantling
  // -------------------------------------------------------------------------
  describe('Machine Decommissioning Workflow & Validation', () => {
    it('requires mandatory justification reason to decommission a machine', () => {
      const machineId = '11111111-1111-1111-1111-111111111111'
      expect(() => {
        machinesStore.decommissionMachine(
          machineId,
          {
            reason: '',
            targetDisposition: 'decommissioned',
            partsHandling: 'salvage_donor'
          },
          actor
        )
      }).toThrow('Mandatory justification reason required to decommission a production machine.')
    })

    it('sets machine operational status to decommissioned with reason and disposition date', () => {
      const machineId = '22222222-2222-2222-2222-222222222222'
      const decommDate = '2026-10-02T12:00:00.000Z'
      const justification = 'Replaced by next-gen laser weld cell PRJ-448'

      const result = machinesStore.decommissionMachine(
        machineId,
        {
          reason: justification,
          targetDisposition: 'decommissioned',
          partsHandling: 'retain',
          decommissionDate: decommDate
        },
        actor
      )

      expect(result.machine.status).toBe('decommissioned')
      expect(result.machine.decommissionReason).toBe(justification)
      expect(result.machine.decommissionDate).toBe(decommDate)
      expect(result.machine.decommissionDisposition).toBe('retain')

      // It should now be excluded from the default active floor query
      const activeFloor = machinesStore.getAllMachines()
      expect(activeFloor.some(m => m.id === machineId)).toBe(false)

      // But present in full query
      const all = machinesStore.getAllMachines({ includeDecommissioned: true })
      expect(all.some(m => m.id === machineId)).toBe(true)
    })

    it('supports dismantling machine disposition with parts salvage to warehouse donor spares', () => {
      const machineId = '11111111-1111-1111-1111-111111111111'
      // First ensure a part in partsInventoryStore is linked to this machine
      const { parts } = partsInventoryStore.getAllParts()
      const partToLink = parts.find(p => p.trackingType === 'serialized' && p.condition === 'used') || parts[0]
      partsInventoryStore.editPart(partToLink.id, {
        linkedMachineId: machineId,
        stationId: 'LINE1-OP10'
      })

      const decommResult = machinesStore.decommissionMachine(
        machineId,
        {
          reason: 'Cell dismantled for production retooling to Model-Z platform',
          targetDisposition: 'dismantled',
          partsHandling: 'salvage_donor'
        },
        actor
      )

      expect(decommResult.machine.status).toBe('dismantled')
      expect(decommResult.salvagedPartsCount).toBeGreaterThan(0)

      // The linked part should now be transitioned to donor condition
      const updatedPart = partsInventoryStore.getPartById(partToLink.id)
      expect(updatedPart?.condition).toBe('donor')
      const audit = partsInventoryStore.getAuditLog(partToLink.id)
      expect(audit.some(h => h.notes?.includes('Salvaged as donor part'))).toBe(true)
    })

    it('scraps installed parts when machine is decommissioned with scrap parts handling', () => {
      const machineId = '44444444-4444-4444-4444-444444444444'
      const { parts } = partsInventoryStore.getAllParts()
      const partToLink = parts[1]
      partsInventoryStore.editPart(partToLink.id, {
        linkedMachineId: machineId,
        stationId: 'LINE1-OP40'
      })

      const decommResult = machinesStore.decommissionMachine(
        machineId,
        {
          reason: 'Irreparable structural stress cracking after continuous duty',
          targetDisposition: 'decommissioned',
          partsHandling: 'scrap'
        },
        actor
      )

      expect(decommResult.machine.status).toBe('decommissioned')
      expect(decommResult.scrappedPartsCount).toBeGreaterThan(0)

      const updatedPart = partsInventoryStore.getPartById(partToLink.id)
      expect(updatedPart?.condition).toBe('scrap')
      const audit = partsInventoryStore.getAuditLog(partToLink.id)
      expect(audit.some(h => h.notes?.includes('Scrapped during decommissioning'))).toBe(true)
    })
  })
})
