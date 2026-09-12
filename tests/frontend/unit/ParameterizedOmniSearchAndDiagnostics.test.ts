import { describe, it, expect, beforeEach } from 'vitest'
import { ref } from 'vue'
import { useOmniSearch } from '../../../frontend/web/app/composables/useOmniSearch'
import { 
  resolveSearchTemplate, 
  machineTemplate, 
  endpointTemplate, 
  inventoryTemplate, 
  userTemplate, 
  telemetryTemplate 
} from '../../../frontend/web/app/utils/search/searchTemplates'
import { 
  harvestDynamicKeyValues, 
  BASELINE_KNOWN_KEY_VALUES, 
  type RankedIndexingTable 
} from '../../../frontend/web/app/utils/search/indexingTables'
import { calculateFuzzyScore, isFuzzyMatch, rankByFuzzyScore } from '../../../frontend/web/app/utils/search/fuzzy'
import { SearchDiagnosticsService } from '../../../frontend/web/app/utils/search/searchDiagnostics'

describe('Parameterized useOmniSearch & GDPR Diagnostics Suite', () => {
  describe('Named Templates & Protobuf Schema Normalization', () => {
    it('normalizes machine station objects into uniform SearchResultItem', () => {
      const rawMachine = {
        id: 'st-disp-01',
        name: 'Dispensing Station 1',
        displayName: 'Glue Dispenser Cell',
        customIdentifier: 'LINE-A-DISP01',
        machineType: 'Dispenser',
        groupId: 'Line 1',
        controllers: [{ id: 'ctrl-1' }, { id: 'ctrl-2' }],
        isOnline: true
      }

      const item = machineTemplate.extractSearchItem(rawMachine)
      expect(item.id).toBe('st-disp-01')
      expect(item.name).toBe('Dispensing Station 1')
      expect(item.displayName).toBe('Glue Dispenser Cell')
      expect(item.itemType).toBe('Machine')
      expect(item.typeLabel).toBe('Dispenser')
      expect(item.subtitle).toContain('Line: Line 1')
      expect(item.subtitle).toContain('Controllers: 2')
      expect(item.status).toBe('online')
      expect(item.link).toBe('/dashboard/machines?id=st-disp-01')
    })

    it('normalizes endpoint objects matching system_info.proto SystemInfoRequest', () => {
      const rawEndpoint = {
        hostname: 'IPC-STATION-01',
        machine_identifier: 'MACH-9021',
        mac_address: '02:65:54:CE:AE:FC',
        ipAddress: '192.168.1.120',
        controllerType: 'IPC',
        last_online: '2026-09-12T10:00:00Z'
      }

      const item = endpointTemplate.extractSearchItem(rawEndpoint)
      expect(item.id).toBe('MACH-9021')
      expect(item.name).toBe('IPC-STATION-01')
      expect(item.itemType).toBe('ClientPc')
      expect(item.typeLabel).toBe('IPC')
      expect(item.subtitle).toContain('IP: 192.168.1.120')
      expect(item.subtitle).toContain('MAC: 02:65:54:CE:AE:FC')
      expect(item.status).toBe('online')
      expect(item.link).toBe('/dashboard/clients')
    })

    it('normalizes inventory components with serialized and stock metadata', () => {
      const rawInventory = {
        id: 'inv-srv-01',
        name: 'Beckhoff AX5000 Servo',
        displayName: 'Z-Axis Servo Drive',
        itemType: 'hardware',
        serialNumber: 'SN-BECK-987654',
        manufacturer: { name: 'Beckhoff' },
        equipmentStatus: 'InMachine',
        isStockItem: false
      }

      const item = inventoryTemplate.extractSearchItem(rawInventory)
      expect(item.id).toBe('inv-srv-01')
      expect(item.name).toBe('Beckhoff AX5000 Servo')
      expect(item.manufacturerName).toBe('Beckhoff')
      expect(item.subtitle).toBe('SN: SN-BECK-987654')
      expect(item.status).toBe('InMachine')
      expect(item.typeLabel).toBe('hardware')
    })

    it('normalizes telemetry data points matching telemetry.proto TelemetryDataPoint', () => {
      const rawTelemetry = {
        point_id: 'pt-spindle-rpm',
        canonical_key: 'Beckhoff.Ads:Main.Spindle.Speed',
        quality: 0,
        type_descriptor: {
          classifier: 10,
          unit: 'RPM',
          description: 'Spindle Rotational Speed'
        }
      }

      const item = telemetryTemplate.extractSearchItem(rawTelemetry)
      expect(item.id).toBe('pt-spindle-rpm')
      expect(item.name).toBe('Beckhoff.Ads:Main.Spindle.Speed')
      expect(item.displayName).toBe('Spindle Rotational Speed')
      expect(item.itemType).toBe('Telemetry')
      expect(item.subtitle).toContain('Unit: RPM')
      expect(item.subtitle).toContain('Quality: 0')
      expect(item.status).toBe('online')
    })

    it('resolves named templates and aliases flexibly', () => {
      expect(resolveSearchTemplate('machines').name).toBe('machine')
      expect(resolveSearchTemplate('client-pc').name).toBe('endpoint')
      expect(resolveSearchTemplate('nodes').name).toBe('endpoint')
      expect(resolveSearchTemplate('inventory').name).toBe('inventory')
      expect(resolveSearchTemplate('users').name).toBe('user')
      expect(resolveSearchTemplate('telemetry').name).toBe('telemetry')
    })
  })

  describe('Ranked Indexing Tables & Dynamic KV Harvester', () => {
    it('harvests unique values from ranked indexing tables to enrich known key-values', () => {
      const mockMachines = [
        { id: 'm1', name: 'Cell-1', groupId: 'Battery-Module-01', technology: 'Dispensing', status: 'online' },
        { id: 'm2', name: 'Cell-2', groupId: 'Pack-Assembly-02', technology: 'LaserWelding', status: 'InStorage' }
      ]

      const mockInventory = [
        { id: 'i1', name: 'Camera', manufacturer: { name: 'Basler' }, technology: 'VisionInspection' },
        { id: 'i2', name: 'Gripper', manufacturer: { name: 'Schunk' }, technology: 'Pneumatics' }
      ]

      const tables: RankedIndexingTable[] = [
        {
          name: 'machines',
          rank: 1,
          data: mockMachines,
          kvFields: {
            line: 'groupId',
            tech: 'technology',
            status: 'status'
          }
        },
        {
          name: 'inventory',
          rank: 2,
          data: mockInventory,
          kvFields: {
            mfr: 'manufacturer.name',
            tech: 'technology'
          }
        }
      ]

      const harvested = harvestDynamicKeyValues(tables, BASELINE_KNOWN_KEY_VALUES)

      // Harvested new manufacturers Basler and Schunk
      expect(harvested.mfr.values).toContain('Basler')
      expect(harvested.mfr.values).toContain('Schunk')
      // Baseline manufacturers still preserved
      expect(harvested.mfr.values).toContain('Siemens')

      // Harvested new production lines
      expect(harvested.line.values).toContain('Battery-Module-01')
      expect(harvested.line.values).toContain('Pack-Assembly-02')

      // Harvested new technologies
      expect(harvested.tech.values).toContain('LaserWelding')
      expect(harvested.tech.values).toContain('VisionInspection')
      expect(harvested.tech.values).toContain('Pneumatics')
    })
  })

  describe('Parameterized Ingestion via useOmniSearch Composable', () => {
    it('executes in-memory search over JSON data with template normalization', async () => {
      const dataset = [
        { id: 'm-1', name: 'Robotic Welder 1', machineType: 'Robot', groupId: 'Weld-Line', status: 'online' },
        { id: 'm-2', name: 'Vision QA Cell', machineType: 'Camera', groupId: 'Quality-Line', status: 'online' },
        { id: 'm-3', name: 'Laser Marker', machineType: 'Laser', groupId: 'Marking-Line', status: 'offline' }
      ]

      const { results, executeSearch, rawInput } = useOmniSearch({
        template: 'machine',
        data: dataset,
        config: { debounceMs: 10 }
      })

      rawInput.value = 'welder'
      await executeSearch('welder')

      expect(results.value.length).toBe(1)
      expect(results.value[0].name).toBe('Robotic Welder 1')
      expect(results.value[0].itemType).toBe('Machine')
      expect(results.value[0].subtitle).toContain('Line: Weld-Line')
    })

    it('filters JSON items by active tags and free text simultaneously', async () => {
      const dataset = [
        { id: 'i-1', name: 'Servo Motor A', itemType: 'hardware', technology: 'Motion', equipmentStatus: 'InMachine' },
        { id: 'i-2', name: 'Servo Motor B', itemType: 'hardware', technology: 'Motion', equipmentStatus: 'InStorage' },
        { id: 'i-3', name: 'Screw M6', itemType: 'stock', isStockItem: true, equipmentStatus: 'InStorage' }
      ]

      const { results, addTag, rawInput, executeSearch } = useOmniSearch({
        template: 'inventory',
        data: dataset
      })

      addTag({ key: 'status', value: 'InStorage' })
      rawInput.value = 'servo'
      await executeSearch()

      expect(results.value.length).toBe(1)
      expect(results.value[0].id).toBe('i-2')
      expect(results.value[0].name).toBe('Servo Motor B')
    })

    it('supports custom dataSource handler for specialized backend connectors', async () => {
      const customHandler = async (query: string) => {
        return [
          { id: 'custom-1', name: `Result for ${query}`, itemType: 'Custom' }
        ]
      }

      const { results, executeSearch } = useOmniSearch({
        dataSource: { type: 'custom', handler: customHandler },
        template: 'inventory'
      })

      await executeSearch('plc-query')
      expect(results.value.length).toBe(1)
      expect(results.value[0].name).toBe('Result for plc-query')
    })
  })

  describe('Fuzzy Search & Typo-Tolerant Autocomplete', () => {
    it('computes high similarity scores for small typos in industrial terms', () => {
      // 1-edit typo in manufacturer
      const scoreSiemens = calculateFuzzyScore('siemns', 'Siemens')
      expect(scoreSiemens).toBeGreaterThanOrEqual(0.8)

      // Exact substring
      const scoreAdvantech = calculateFuzzyScore('advan', 'Advantech IPC')
      expect(scoreAdvantech).toBeGreaterThanOrEqual(0.8)

      // Distant mismatch
      const scoreMismatch = calculateFuzzyScore('xyz123', 'Beckhoff')
      expect(scoreMismatch).toBe(0.0)
    })

    it('ranks candidate items by fuzzy similarity score', () => {
      const items = ['Siemens S7-1500', 'Schneider Electric PLC', 'Siemens S7-1200', 'Beckhoff CX5140']
      const ranked = rankByFuzzyScore('siemns', items, item => [item], 0.7)

      expect(ranked.length).toBe(2)
      expect(ranked[0].item).toContain('Siemens')
      expect(ranked[1].item).toContain('Siemens')
    })

    it('suggests values using fuzzy matching when typing typos in key:value inputs', () => {
      const {
        valueSuggestions,
        handleInputChange,
        activePendingKey
      } = useOmniSearch({
        template: 'inventory',
        config: { fuzzy: { enabled: true, threshold: 0.7 } }
      })

      // User types `mfr:siemns` with a typo
      handleInputChange('mfr:siemns')
      expect(activePendingKey.value).toBe('mfr')
      expect(valueSuggestions.value.some(v => v.value === 'Siemens')).toBe(true)
    })
  })

  describe('GDPR-Compliant Diagnostics & Telemetry Collector', () => {
    const diagnostics = new SearchDiagnosticsService({ enabled: true, maxBufferSize: 5 })

    beforeEach(() => {
      diagnostics.clear()
    })

    it('scrubs email addresses from search queries (GDPR Art. 5/6)', () => {
      const query = 'Find user john.doe@enterprise-factory.corp on line 2'
      const sanitized = diagnostics.sanitizeQuery(query)
      expect(sanitized).toBe('Find user [REDACTED_EMAIL] on line 2')
      expect(sanitized).not.toContain('john.doe@enterprise-factory.corp')
    })

    it('scrubs IPv4 and IPv6 network addresses from diagnostics (GDPR Recital 49/Art. 25)', () => {
      const queryIpv4 = 'status of IPC at 192.168.1.105'
      const sanitizedIpv4 = diagnostics.sanitizeQuery(queryIpv4)
      expect(sanitizedIpv4).toBe('status of IPC at [REDACTED_IP]')
      expect(sanitizedIpv4).not.toContain('192.168.1.105')

      const queryIpv6 = 'lookup 2001:0db8:85a3:0000:0000:8a2e:0370:7334'
      const sanitizedIpv6 = diagnostics.sanitizeQuery(queryIpv6)
      expect(sanitizedIpv6).toBe('lookup [REDACTED_IPV6]')
    })

    it('scrubs credentials, passwords, and bearer tokens', () => {
      const query = 'connect token=eyJh1234567890.secret.token bearer xyzSecretToken99'
      const sanitized = diagnostics.sanitizeQuery(query)
      expect(sanitized).toContain('[REDACTED_CREDENTIAL]')
      expect(sanitized).not.toContain('xyzSecretToken99')
    })

    it('generates daily-salted ephemeral client ID without persistent user identifiers', () => {
      const clientId1 = diagnostics.getEphemeralClientId('usr-session-42')
      const clientId2 = diagnostics.getEphemeralClientId('usr-session-42')
      const clientIdOther = diagnostics.getEphemeralClientId('usr-session-99')

      // Stable within same session today
      expect(clientId1).toBe(clientId2)
      expect(clientId1).not.toBe(clientIdOther)
      // Never contains the original user identifier
      expect(clientId1).not.toContain('usr-session-42')
      expect(clientId1.startsWith('client_')).toBe(true)
    })

    it('records telemetry into a bounded ring buffer, evicting oldest entries', () => {
      for (let i = 1; i <= 7; i++) {
        diagnostics.recordSearch({
          rawQuery: `query ${i}`,
          durationMs: 12.5,
          resultCount: i,
          dataSourceType: 'inMemory',
          status: 'success'
        })
      }

      // Max buffer size is 5
      expect(diagnostics.getBufferLength()).toBe(5)
      const records = diagnostics.getRecords()
      // Oldest (query 1 and 2) were evicted
      expect(records[0].sanitizedQuerySignature).toBe('query 3')
      expect(records[4].sanitizedQuerySignature).toBe('query 7')
    })

    it('can be completely disabled for strict opt-out', () => {
      diagnostics.setEnabled(false)
      const record = diagnostics.recordSearch({
        rawQuery: 'test query',
        durationMs: 5,
        resultCount: 1
      })
      expect(record).toBeNull()
      expect(diagnostics.getBufferLength()).toBe(0)
    })
  })
})
