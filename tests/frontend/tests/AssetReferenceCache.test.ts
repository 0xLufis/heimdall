import { describe, it, expect, beforeEach } from 'vitest'
import { useAssetReferenceCache } from '../../../frontend/nuxt-app/app/composables/useAssetReferenceCache'
import { useInventoryProvisioning } from '../../../frontend/nuxt-app/app/composables/useInventoryProvisioning'

describe('Asset Reference Cache & Unified Search Source Suite', () => {
  const mockManufacturers = [
    { id: 'm-siemens', name: 'Siemens AG', country: 'Germany' },
    { id: 'm-beckhoff', name: 'Beckhoff Automation', country: 'Germany' }
  ]

  const mockSuppliers = [
    { id: 's-distri1', name: 'EuroIndustrial Distro' },
    { id: 's-distri2', name: 'Direct Factory Supply' }
  ]

  const mockMachines = [
    { id: 'mach-cell1', name: 'Assembly Station OP10', customIdentifier: 'STATION-OP10' },
    { id: 'mach-cell2', name: 'Packaging Station OP20', customIdentifier: 'STATION-OP20' }
  ]

  const mockPcs = [
    { id: 'pc-1', hostname: 'LINE01-IPC-01', ipAddress: '192.168.1.10', os: 'Windows 10 IoT', status: 'online' },
    { id: 'pc-2', hostname: 'LINE01-IPC-02', ipAddress: '192.168.1.11', os: 'Ubuntu Core 22.04', status: 'online' }
  ]

  const mockInventoryTree = [
    {
      id: 'comp-1',
      name: 'Main S7-1500 Controller',
      displayName: 'Line PLC Master',
      itemType: 'HardwareComponent',
      technology: 'Siemens SIMATIC / PROFINET',
      modelNumber: '6ES7516-3AN02-0AB0',
      manufacturer: { id: 'm-siemens', name: 'Siemens AG' },
      supplier: { id: 's-distri1', name: 'EuroIndustrial Distro' },
      metadata: {
        Voltage: '24V DC',
        Power: '15KW',
        Protocol: 'PROFINET',
        IPAddress: '192.168.1.100'
      },
      children: [
        {
          id: 'comp-2',
          name: 'SINAMICS S120 Drive',
          displayName: 'Spindle Servo',
          itemType: 'HardwareComponent',
          technology: 'Siemens SINAMICS DRIVE-CLiQ',
          modelNumber: '6SL3120-1TE21-8AA3',
          manufacturer: { id: 'm-siemens', name: 'Siemens AG' },
          metadata: {
            Voltage: '400V 3-Phase',
            Power: '18.5KW',
            RatedCurrent: '38A'
          }
        }
      ]
    },
    {
      id: 'comp-3',
      name: 'TwinCAT 3 Runtime License',
      displayName: 'TC3 Core',
      itemType: 'SoftwareComponent',
      technology: 'Beckhoff TwinCAT 3 / EtherCAT',
      modelNumber: 'TC1200',
      manufacturer: { id: 'm-beckhoff', name: 'Beckhoff Automation' },
      metadata: {
        Version: '3.1.4024.35',
        Protocol: 'EtherCAT',
        CoreCount: '4'
      }
    }
  ]

  const mockTeams = [
    { id: 'team-elec', name: 'Electrical Engineering' },
    { id: 'team-mech', name: 'Mechanical Maintenance' }
  ]

  beforeEach(() => {
    const cache = useAssetReferenceCache()
    cache.ingestInventoryData(
      mockManufacturers,
      mockSuppliers,
      mockMachines,
      mockPcs,
      mockInventoryTree,
      mockTeams
    )
  })

  it('correctly ingests and deduplicates OEMs and Importers with usage counts', () => {
    const cache = useAssetReferenceCache()

    expect(cache.oems.value.length).toBeGreaterThanOrEqual(2)
    const siemens = cache.oems.value.find(o => o.label === 'Siemens AG')
    expect(siemens).toBeDefined()
    expect(siemens?.count).toBeGreaterThan(1) // from manufacturer list and inventory items

    const euroDistro = cache.importers.value.find(s => s.label === 'EuroIndustrial Distro')
    expect(euroDistro).toBeDefined()
  })

  it('correctly extracts Reporting Host PCs, Production Stations, and Components', () => {
    const cache = useAssetReferenceCache()

    // Host PCs
    expect(cache.parentPcs.value.length).toBe(2)
    expect(cache.parentPcs.value[0].hostname).toBe('LINE01-IPC-01')
    expect(cache.parentPcs.value[0].label).toContain('LINE01-IPC-01')

    // Stations
    expect(cache.stations.value.length).toBe(2)
    expect(cache.stations.value.some(s => s.customIdentifier === 'STATION-OP10')).toBe(true)

    // Components (flattened tree)
    expect(cache.components.value.length).toBe(3)
    expect(cache.components.value.some(c => c.id === 'comp-2')).toBe(true)
  })

  it('harvests unique technology stacks and model numbers across HW and SW assets', () => {
    const cache = useAssetReferenceCache()

    const profinet = cache.technologies.value.find(t => t.id === 'Siemens SIMATIC / PROFINET')
    expect(profinet).toBeDefined()

    const twincat = cache.technologies.value.find(t => t.id === 'Beckhoff TwinCAT 3 / EtherCAT')
    expect(twincat).toBeDefined()

    expect(cache.modelNumbers.value.some(m => m.id === '6ES7516-3AN02-0AB0')).toBe(true)
    expect(cache.modelNumbers.value.some(m => m.id === 'TC1200')).toBe(true)
  })

  it('harvests unique metadata specification keys and provides value suggestions', () => {
    const cache = useAssetReferenceCache()

    const keys = cache.metadataKeys.value.map(k => k.key)
    expect(keys).toContain('Voltage')
    expect(keys).toContain('Power')
    expect(keys).toContain('Protocol')
    expect(keys).toContain('RatedCurrent')
    expect(keys).toContain('Version')

    // Test value suggestions by key
    const voltageSuggestions = cache.getSuggestionsForKey('Voltage')
    expect(voltageSuggestions).toContain('24V DC')
    expect(voltageSuggestions).toContain('400V 3-Phase')

    const protocolSuggestions = cache.getSuggestionsForKey('Protocol')
    expect(protocolSuggestions).toContain('PROFINET')
    expect(protocolSuggestions).toContain('EtherCAT')
  })

  it('dynamically registers new custom asset values into the reactive cache on save', () => {
    const cache = useAssetReferenceCache()

    // Register a newly typed asset
    cache.registerAssetValues({
      manufacturer: 'Cognex Corporation',
      supplier: 'Omron Direct',
      technology: 'Cognex Deep Learning Suite',
      modelNumber: 'IS-9912M',
      metadata: {
        Resolution: '12MP',
        FPS: '60fps',
        LensMount: 'C-Mount'
      }
    })

    // Verify immediate reactive update in cache
    expect(cache.oems.value.some(o => o.label === 'Cognex Corporation')).toBe(true)
    expect(cache.importers.value.some(s => s.label === 'Omron Direct')).toBe(true)
    expect(cache.technologies.value.some(t => t.id === 'Cognex Deep Learning Suite')).toBe(true)
    expect(cache.modelNumbers.value.some(m => m.id === 'IS-9912M')).toBe(true)

    const resolutionSuggestions = cache.getSuggestionsForKey('Resolution')
    expect(resolutionSuggestions).toContain('12MP')

    const lensSuggestions = cache.getSuggestionsForKey('LensMount')
    expect(lensSuggestions).toContain('C-Mount')
  })

  it('maintains full compatibility with useInventoryProvisioning', () => {
    const provisioning = useInventoryProvisioning()

    expect(provisioning.manufacturers.value.length).toBeGreaterThanOrEqual(2)
    expect(provisioning.suppliers.value.length).toBeGreaterThanOrEqual(2)
    expect(provisioning.machines.value.length).toBe(2)
    expect(provisioning.clientPcs.value.length).toBe(2)
    expect(provisioning.components.value.length).toBe(3)
    expect(typeof provisioning.getSuggestionsForKey).toBe('function')
  })
})
