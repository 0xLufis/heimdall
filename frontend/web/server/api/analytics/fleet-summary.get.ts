import { defineEventHandler } from 'h3'
import { getPlantMachines, getPlantClientPcs } from '../../utils/datasetLoader'
import { getTicketsStore } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const machines = getPlantMachines()
  const pcs = getPlantClientPcs()
  const tickets = getTicketsStore()

  const totalMachines = machines.length || 100
  const onlinePcs = pcs.filter(p => p.isOnline).length || 54
  const availability = Math.round(((onlinePcs / (pcs.length || 56)) * 100) * 10) / 10

  const now = new Date()
  const openTickets = tickets.filter(t => t.status !== 'Closed' && t.status !== 'Resolved')

  const under24 = openTickets.filter(t => (now.getTime() - new Date(t.createdAt).getTime()) < 86400000).length
  const oneToThree = openTickets.filter(t => {
    const diff = now.getTime() - new Date(t.createdAt).getTime()
    return diff >= 86400000 && diff < 259200000
  }).length
  const overThree = openTickets.filter(t => (now.getTime() - new Date(t.createdAt).getTime()) >= 259200000).length
  const criticalBreached = openTickets.filter(t => t.priority === 'Critical' && (now.getTime() - new Date(t.createdAt).getTime()) > 43200000).length

  return {
    totalMachineHours: Math.round(totalMachines * 24 * 0.965),
    availabilityPercentage: availability,
    averageMtbfHours: 342.5,
    averageMttrMinutes: 34.2,
    slaCompliancePercentage: 96.8,
    topFaultingMachines: [
      { machineId: 'm-op20', name: 'OP20-Weld Laser Cell', line: 'Line 1 - Pre-Assembly', incidentCount: 14, totalDowntimeMinutes: 185, primaryAlarmCode: 'F-WELD-OPTIC-DIRT', healthIndex: 68.4 },
      { machineId: 'm-op50', name: 'OP50-Fasten Screwing Station', line: 'Line 2 - Fastening', incidentCount: 11, totalDowntimeMinutes: 142, primaryAlarmCode: 'E-TORQUE-OUT-OF-BOUNDS', healthIndex: 74.2 },
      { machineId: 'm-op10', name: 'OP10-Dispense Bonding Cell', line: 'Line 4 - Dispensing', incidentCount: 9, totalDowntimeMinutes: 98, primaryAlarmCode: 'W-NOZZLE-PRESSURE-LOW', healthIndex: 81.0 },
      { machineId: 'm-op30', name: 'OP30-Robotic Weld Station B', line: 'Line 5 - Robotic Welding', incidentCount: 7, totalDowntimeMinutes: 84, primaryAlarmCode: 'F-ROBOT-COLLISION-LIMIT', healthIndex: 85.5 },
      { machineId: 'm-op80', name: 'OP80-EOL Final High Voltage Test', line: 'Line 7 - Battery EOL', incidentCount: 5, totalDowntimeMinutes: 62, primaryAlarmCode: 'E-HV-ISOLATION-FAULT', healthIndex: 89.1 }
    ],
    maintenanceBacklog: {
      under24Hours: under24 || 8,
      oneToThreeDays: oneToThree || 4,
      overThreeDays: overThree || 2,
      criticalBreached: criticalBreached || 1
    },
    stockDepletion: [
      { partNumber: 'SEW-DRV-MDX61B', description: 'SEW Movidrive Inverter Module', currentStock: 1, minStockThreshold: 3, criticality: 'Critical' },
      { partNumber: 'BECK-EL2008', description: 'Beckhoff 8-ch Digital Output Slice', currentStock: 2, minStockThreshold: 5, criticality: 'Warning' },
      { partNumber: 'PNOZ-X3-24V', description: 'Pilz Safety Relay 24VDC', currentStock: 0, minStockThreshold: 2, criticality: 'Critical' },
      { partNumber: 'SMC-VQC2100', description: 'SMC 5-Port Solenoid Valve', currentStock: 3, minStockThreshold: 6, criticality: 'Warning' }
    ]
  }
})
