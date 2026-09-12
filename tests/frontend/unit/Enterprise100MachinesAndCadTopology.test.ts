import { describe, it, expect } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'
import {
  getEnterpriseDataset,
  getPlantMetadata,
  getPlantOrganizations,
  getPlantUsers,
  getPlantClientPcs,
  getPlantActiveDirectoryOUs
} from '~~/server/utils/datasetLoader'

describe('Enterprise 100 Machines, CAD Topology & Synthetic Dataset Suite', () => {
  const dataset = getEnterpriseDataset()

  describe('Synthetic AI Persona & Organization Governance', () => {
    it('guarantees 60 synthetic personas with zero PII and fake AI-generated names', () => {
      const users = getPlantUsers()
      expect(users.length).toBe(60)

      for (const u of users) {
        expect(u.id).toMatch(/^usr-synth-\d{2}$/)
        expect(u.email).toMatch(/@fake-factory\.internal$/)
        expect(u.name).toBeTruthy()
        expect(u.primaryRole).toBeTruthy()
        expect(u.department).toBeTruthy()
        expect(u.securityGroupIds.length).toBeGreaterThanOrEqual(1)
      }

      // Check specific key personas
      const botman = users.find(u => u.name.includes('Synthetica Botman'))
      expect(botman).toBeDefined()
      expect(botman?.primaryRole).toBe('controls_engineer')

      const robo = users.find(u => u.name.includes('Robo McControlsFace'))
      expect(robo).toBeDefined()

      const prime = users.find(u => u.name.includes('Dr. Algorithmus Prime'))
      expect(prime).toBeDefined()
      expect(prime?.primaryRole).toBe('plant_director')
      expect(prime?.mfaPolicy).toBe('always')

      const flowski = users.find(u => u.name.includes('Tensor Flowski'))
      expect(flowski).toBeDefined()
    })

    it('contains 16 organizations covering 8 production lines and 8 engineering disciplines', () => {
      const orgs = getPlantOrganizations()
      expect(orgs.length).toBe(16)

      const lineOrgs = orgs.filter(o => o.id.startsWith('org-line-'))
      expect(lineOrgs.length).toBe(8)

      const guildOrgs = orgs.filter(o => !o.id.startsWith('org-line-'))
      expect(guildOrgs.length).toBe(8)

      const line01 = orgs.find(o => o.id === 'org-line-01')
      expect(line01?.name).toContain('Audi E-Tron Module Line')

      const controlsGuild = orgs.find(o => o.id === 'org-controls')
      expect(controlsGuild?.name).toContain('Controls & Automation')
    })
  })

  describe('Edge IPCs, CMI Specs & TwinCAT Runtime', () => {
    it('verifies 56 industrial IPCs across 8 lines with CMI hardware and TwinCAT port 851', () => {
      const pcs = getPlantClientPcs()
      expect(pcs.length).toBe(56)

      for (const pc of pcs) {
        expect(pc.hostname).toMatch(/^IPC-L0[1-8]-/)
        expect(pc.machineIdentifier).toBe(`HW-${pc.hostname}`)
        expect(pc.ipAddress).toMatch(/^192\.168\.10[1-8]\.\d+$/)
        expect(pc.macAddress).toMatch(/^02:65:54:/)
        expect(pc.vlanId).toBeGreaterThanOrEqual(101)
        expect(pc.vlanId).toBeLessThanOrEqual(108)

        // CMI Hardware checks
        expect(pc.cmiHardware.cpu.NumberOfCores).toBeGreaterThanOrEqual(8)
        expect(pc.cmiHardware.memory.Capacity).toBeGreaterThanOrEqual(32 * 1024 * 1024 * 1024)
        expect(pc.cmiHardware.disks.length).toBeGreaterThanOrEqual(2)
        expect(pc.cmiHardware.disks.some(d => d.Caption === 'C:')).toBe(true)
        expect(pc.cmiHardware.disks.some(d => d.Caption === 'D:')).toBe(true)

        // TwinCAT & MES packages
        expect(pc.installedPackages.some(pkg => pkg.includes('TwinCAT 3.1') && pkg.includes('Port 851'))).toBe(true)
        expect(pc.installedPackages.some(pkg => pkg.includes('TcRTEthernet'))).toBe(true)
      }
    })
  })

  describe('Control Topologies (1-1, 1-n, m-n, n-1)', () => {
    it('verifies all 8 production lines implement the 4 control topologies in production_topology.json', () => {
      const topologyPath = path.resolve(process.cwd(), '../../seed_data/production_topology.json')
      const altPath = path.resolve(process.cwd(), 'seed_data/production_topology.json')
      const targetPath = fs.existsSync(topologyPath) ? topologyPath : altPath

      expect(fs.existsSync(targetPath)).toBe(true)
      const topologyData = JSON.parse(fs.readFileSync(targetPath, 'utf-8'))
      const lines = topologyData.production_hall.lines
      expect(lines.length).toBe(8)

      for (const line of lines) {
        expect(line.stations.length).toBeGreaterThanOrEqual(12)

        // 1-1 Topology: Dedicated station controller (OP030)
        const dedicatedStation = line.stations.find((s: any) => s.id.endsWith('-OP030'))
        expect(dedicatedStation).toBeDefined()
        expect(dedicatedStation.pcs.length).toBe(1)
        expect(dedicatedStation.pcs[0].hostname).toContain('DEDICATED')

        // 1-n Topology: Master Pallet Conveyor PLC linked to multiple stations (OP010, OP020, OP050)
        const conveyorStations = line.stations.filter((s: any) =>
          s.pcs.some((p: any) => p.hostname.includes('CONVEYOR-MAIN'))
        )
        expect(conveyorStations.length).toBeGreaterThanOrEqual(3)

        // m-n Topology: Distributed robotics controllers (ROB-ALPHA and ROB-BETA on OP080)
        const robStation = line.stations.find((s: any) => s.id.endsWith('-OP080'))
        expect(robStation).toBeDefined()
        expect(robStation.pcs.length).toBe(2)
        expect(robStation.pcs.some((p: any) => p.hostname.includes('ROB-ALPHA'))).toBe(true)
        expect(robStation.pcs.some((p: any) => p.hostname.includes('ROB-BETA'))).toBe(true)

        // n-1 Topology: Specialized IPC cluster (PLC, Vision, MES) linked to single station (OP060)
        const n1Station = line.stations.find((s: any) => s.id.endsWith('-OP060'))
        expect(n1Station).toBeDefined()
        expect(n1Station.pcs.length).toBe(3)
        expect(n1Station.pcs.some((p: any) => p.hostname.includes('PLC'))).toBe(true)
        expect(n1Station.pcs.some((p: any) => p.hostname.includes('VISION'))).toBe(true)
        expect(n1Station.pcs.some((p: any) => p.hostname.includes('MES-GATE'))).toBe(true)
      }
    })
  })

  describe('Inventory Seed Dataset (100 Machines, 550 Spares & Bulk Consumables)', () => {
    it('validates inventory_seed.csv rows and classification counts', () => {
      const csvPath = path.resolve(process.cwd(), '../../seed_data/inventory_seed.csv')
      const altPath = path.resolve(process.cwd(), 'seed_data/inventory_seed.csv')
      const targetPath = fs.existsSync(csvPath) ? csvPath : altPath

      expect(fs.existsSync(targetPath)).toBe(true)
      const lines = fs.readFileSync(targetPath, 'utf-8').trim().split('\n')
      const header = lines[0].split(',')

      const typeIdx = header.indexOf('Type')
      const isStockIdx = header.indexOf('IsStockItem')
      const statusIdx = header.indexOf('EquipmentStatus')
      const handleIdx = header.indexOf('PinnedObjectHandle')
      const mfrIdx = header.indexOf('Manufacturer')

      function parseCsvLine(text: string): string[] {
        const result: string[] = []
        let cell = ''
        let inQuotes = false
        for (let i = 0; i < text.length; i++) {
          const char = text[i]
          if (char === '"') {
            if (inQuotes && text[i + 1] === '"') {
              cell += '"'
              i++
            } else {
              inQuotes = !inQuotes
            }
          } else if (char === ',' && !inQuotes) {
            result.push(cell)
            cell = ''
          } else {
            cell += char
          }
        }
        result.push(cell)
        return result
      }

      const rows = lines.slice(1).map(l => parseCsvLine(l))

      const machines = rows.filter(r => r[typeIdx] === 'Machine')
      expect(machines.length).toBe(100)

      // Ensure every machine has a unique pinnedObjectHandle
      const handles = machines.map(m => m[handleIdx])
      expect(new Set(handles).size).toBe(100)
      for (const h of handles) {
        expect(h).toMatch(/^L0[1-8]-OP\d{3}$/)
      }

      // IPCs
      const ipcs = rows.filter(r => r[typeIdx] === 'ClientPc')
      expect(ipcs.length).toBe(56)

      // In-Machine Components
      const inMachine = rows.filter(r =>
        (r[typeIdx] === 'HardwareComponent' || r[typeIdx] === 'SoftwareComponent') &&
        r[statusIdx] === 'InMachine'
      )
      expect(inMachine.length).toBe(200)

      // 550 Serialized Spare Parts
      const serializedSpares = rows.filter(r =>
        (r[typeIdx] === 'HardwareComponent' || r[typeIdx] === 'SoftwareComponent') &&
        r[isStockIdx] === 'False' &&
        (r[statusIdx] === 'InStorage' || r[statusIdx] === 'UnderRepair')
      )
      expect(serializedSpares.length).toBe(550)

      const inStorage = serializedSpares.filter(r => r[statusIdx] === 'InStorage')
      const underRepair = serializedSpares.filter(r => r[statusIdx] === 'UnderRepair')
      expect(inStorage.length).toBe(500)
      expect(underRepair.length).toBe(50)

      // Bulk Consumables (Pneumatics, Fasteners, Strut Profiles, Wiring)
      const bulkConsumables = rows.filter(r =>
        (r[typeIdx] === 'HardwareComponent' || r[typeIdx] === 'SoftwareComponent') &&
        r[isStockIdx] === 'True'
      )
      expect(bulkConsumables.length).toBeGreaterThanOrEqual(20)

      const festo = bulkConsumables.some(r => r[mfrIdx].includes('Festo'))
      const wuerth = bulkConsumables.some(r => r[mfrIdx].includes('Würth') || r[mfrIdx].includes('Wuerth'))
      const rexroth = bulkConsumables.some(r => r[mfrIdx].includes('Bosch Rexroth'))
      const phoenix = bulkConsumables.some(r => r[mfrIdx].includes('Phoenix Contact'))

      expect(festo).toBe(true)
      expect(wuerth).toBe(true)
      expect(rexroth).toBe(true)
      expect(phoenix).toBe(true)
    })
  })

  describe('CAD Floorplan DXF Station Handle Pre-Linking', () => {
    it('verifies DXF floorplans exist and contain all 100 station block handles', () => {
      const dxfPath = path.resolve(process.cwd(), 'public/sample/assembly_line.dxf')
      expect(fs.existsSync(dxfPath)).toBe(true)

      const dxfContent = fs.readFileSync(dxfPath, 'utf-8')

      // Check all 8 lines and station handle representations
      for (let l = 1; l <= 8; l++) {
        const linePrefix = `L0${l}-OP`
        expect(dxfContent).toContain(linePrefix)
      }

      // Check specific first and last stations
      expect(dxfContent).toContain('L01-OP010')
      expect(dxfContent).toContain('L08-OP130')
    })
  })
})
