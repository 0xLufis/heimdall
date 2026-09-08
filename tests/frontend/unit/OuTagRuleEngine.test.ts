import { describe, it, expect } from 'vitest'
import {
  tokenizeText,
  matchPattern,
  extractHostDna,
  evaluateHostAgainstRule,
  evaluateFleetAssignments,
  filterAndPaginateControllers
} from '~/utils/ouTagRuleEngine'
import type { OuTagRecipeRule } from '~/types/telemetry'

describe('OU & Asset Tag Rule Engine with Configurable Pagination', () => {
  const sampleRules: OuTagRecipeRule[] = [
    {
      id: 'rule-beckhoff-motion',
      name: 'Beckhoff IPC & Motion Controllers',
      priority: 1,
      enabled: true,
      targetTemplateId: 'high-freq-motion',
      ouPatterns: [],
      ouMatchMode: 'ANY',
      tags: ['Beckhoff', 'IPC', 'Controller'],
      tagMatchMode: 'ALL'
    },
    {
      id: 'rule-vision-cells',
      name: 'Vision & Optical Inspection Cells',
      priority: 2,
      enabled: true,
      targetTemplateId: 'vision-inspection',
      ouPatterns: [],
      ouMatchMode: 'ANY',
      tags: ['Vision', 'Cognex', 'Inspector'],
      tagMatchMode: 'ANY'
    },
    {
      id: 'rule-line-a',
      name: 'Line-A General Assembly',
      priority: 3,
      enabled: true,
      targetTemplateId: 'standard-factory-baseline',
      ouPatterns: ['*LINE-A*'],
      ouMatchMode: 'ANY',
      tags: [],
      tagMatchMode: 'ALL'
    }
  ]

  it('tokenizes text cleanly and handles industrial delimiters', () => {
    const tokens = tokenizeText('IPC-Beckhoff_Line01.Cell#4; Fastening')
    expect(tokens).toContain('ipc')
    expect(tokens).toContain('beckhoff')
    expect(tokens).toContain('line01')
    expect(tokens).toContain('cell')
    expect(tokens).toContain('4')
    expect(tokens).toContain('fastening')
  })

  it('matches wildcards and glob patterns correctly', () => {
    expect(matchPattern('*LINE-A*', 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp')).toBe(true)
    expect(matchPattern('*LINE-B*', 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp')).toBe(false)
    expect(matchPattern('OU=Fastening*', 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp')).toBe(true)
    expect(matchPattern('*', 'AnyString')).toBe(true)
  })

  it('extracts host DNA tokens from hardware, OU paths, and metadata', () => {
    const mockController = {
      id: 'pc-001',
      hostname: 'IPC-FASTENING-01',
      adOuPath: 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp',
      ouTags: { 'workstation.role': 'Controller' },
      inventoryItems: [
        { name: 'Beckhoff Industrial PC C6920', modelNumber: 'C6920-0050', itemType: 'PcHardware' },
        { name: 'Beckhoff EtherCAT Coupler EK1100', itemType: 'HardwareComponent' }
      ],
      systemMetadata: {
        Manufacturer: 'Beckhoff Automation',
        BeckhoffRT: true
      }
    }

    const dna = extractHostDna(mockController)
    expect(dna.tokens.has('beckhoff')).toBe(true)
    expect(dna.tokens.has('ipc')).toBe(true)
    expect(dna.tokens.has('controller')).toBe(true)
    expect(dna.tokens.has('fastening')).toBe(true)
    expect(dna.tokens.has('line-a') || dna.tokens.has('line')).toBe(true)
    expect(dna.tokens.has('ethercat')).toBe(true)
  })

  it('evaluates rule with ALL match mode: Beckhoff + IPC + Controller matches Beckhoff PC', () => {
    const mockController = {
      id: 'pc-001',
      hostname: 'IPC-WELDING-01',
      adOuPath: 'OU=Welding,DC=factory,DC=corp',
      inventoryItems: [
        { name: 'Beckhoff Industrial PC', itemType: 'PcHardware' }
      ],
      systemMetadata: { BeckhoffRT: true }
    }

    const dna = extractHostDna(mockController)
    const rule = sampleRules[0] // Beckhoff + IPC + Controller (ALL)
    const result = evaluateHostAgainstRule(dna, rule)

    expect(result.isMatch).toBe(true)
    expect(result.matchedTags).toContain('beckhoff')
    expect(result.matchedTags).toContain('ipc')
    expect(result.matchedTags).toContain('controller')
  })

  it('evaluates rule with ALL match mode: fails when a required tag is missing', () => {
    const mockController = {
      id: 'pc-002',
      hostname: 'GENERIC-WORKSTATION',
      adOuPath: 'OU=Office,DC=factory,DC=corp',
      inventoryItems: [
        { name: 'Dell Precision Tower', itemType: 'PcHardware' }
      ]
    }

    const dna = extractHostDna(mockController)
    const rule = sampleRules[0] // Requires Beckhoff, IPC, Controller
    const result = evaluateHostAgainstRule(dna, rule)

    expect(result.isMatch).toBe(false)
  })

  it('evaluates rule with ANY match mode: matches when at least one tag is present', () => {
    const mockController = {
      id: 'pc-003',
      hostname: 'LINE-09-INSPECTOR',
      adOuPath: 'OU=Quality,DC=factory,DC=corp',
      inventoryItems: [
        { name: 'Cognex In-Sight 7000 Camera', itemType: 'HardwareComponent' }
      ]
    }

    const dna = extractHostDna(mockController)
    const rule = sampleRules[1] // Vision, Cognex, Inspector (ANY)
    const result = evaluateHostAgainstRule(dna, rule)

    expect(result.isMatch).toBe(true)
  })

  it('resolves fleet assignments in priority order and falls back to default', () => {
    const fleet = [
      {
        id: 'pc-beckhoff',
        hostname: 'IPC-BECKHOFF-LINE-A',
        adOuPath: 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp',
        inventoryItems: [{ name: 'Beckhoff Industrial PC' }],
        systemMetadata: { BeckhoffRT: true }
      },
      {
        id: 'pc-line-a-standard',
        hostname: 'STD-LINE-A-01',
        adOuPath: 'OU=Assembly,OU=LINE-A,DC=factory,DC=corp',
        inventoryItems: [{ name: 'Standard Box PC' }]
      },
      {
        id: 'pc-unmatched',
        hostname: 'WAREHOUSE-PC-01',
        adOuPath: 'OU=Warehouse,DC=factory,DC=corp',
        inventoryItems: [{ name: 'Generic PC' }]
      }
    ]

    const evals = evaluateFleetAssignments(fleet, sampleRules, {}, 'fallback-default')

    // pc-beckhoff matches Priority #1 (Beckhoff Motion) even though it also matches Line-A OU
    expect(evals['pc-beckhoff'].assignmentMode).toBe('rule')
    expect(evals['pc-beckhoff'].recipeId).toBe('high-freq-motion')
    expect(evals['pc-beckhoff'].matchedRule?.id).toBe('rule-beckhoff-motion')

    // pc-line-a-standard matches Priority #3 (Line-A Baseline)
    expect(evals['pc-line-a-standard'].assignmentMode).toBe('rule')
    expect(evals['pc-line-a-standard'].recipeId).toBe('standard-factory-baseline')

    // pc-unmatched falls back to default
    expect(evals['pc-unmatched'].assignmentMode).toBe('default')
    expect(evals['pc-unmatched'].recipeId).toBe('fallback-default')
  })

  it('honors manual overrides over evaluated rules', () => {
    const fleet = [
      {
        id: 'pc-beckhoff',
        hostname: 'IPC-BECKHOFF-01',
        inventoryItems: [{ name: 'Beckhoff Industrial PC' }],
        systemMetadata: { BeckhoffRT: true }
      }
    ]

    const manual = { 'pc-beckhoff': 'custom-override-recipe' }
    const evals = evaluateFleetAssignments(fleet, sampleRules, manual, 'fallback-default')

    expect(evals['pc-beckhoff'].assignmentMode).toBe('manual')
    expect(evals['pc-beckhoff'].recipeId).toBe('custom-override-recipe')
    expect(evals['pc-beckhoff'].matchedRule?.id).toBe('rule-beckhoff-motion') // Keeps reference to rule it would have matched
  })

  describe('Fleet Omni-Search & High-Scale Configurable Pagination (5, 10, 100, 1000, Custom)', () => {
    // Generate mock fleet of 5,000 computers
    const largeFleet = Array.from({ length: 5000 }, (_, i) => ({
      id: `pc-sim-${i + 1}`,
      hostname: i % 10 === 0 ? `IPC-BECKHOFF-${i + 1}` : `STATION-PC-${i + 1}`,
      ipAddress: `10.10.${Math.floor(i / 250)}.${(i % 250) + 1}`,
      macAddress: `00:1B:44:11:${(i % 99).toString(16).padStart(2, '0')}:${(i % 50).toString(16).padStart(2, '0')}`,
      adOuPath: i % 5 === 0 ? 'OU=Fastening,OU=LINE-A,DC=factory,DC=corp' : 'OU=Production,DC=factory,DC=corp',
      inventoryItems: i % 10 === 0 ? [{ name: 'Beckhoff Industrial PC' }] : [{ name: 'Standard Industrial PC' }],
      systemMetadata: i % 10 === 0 ? { BeckhoffRT: true } : {}
    }))

    const largeEvals = evaluateFleetAssignments(largeFleet, sampleRules, {}, 'standard-factory-baseline')
    const templateNames = {
      'high-freq-motion': 'High-Frequency Motion Controller',
      'vision-inspection': 'Vision Inspection Station',
      'standard-factory-baseline': 'Standard Factory IPC Baseline'
    }

    it('paginates 5,000 nodes correctly for page size 5', () => {
      const res = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 1, 5)
      expect(res.total).toBe(5000)
      expect(res.filteredTotal).toBe(5000)
      expect(res.totalPages).toBe(1000)
      expect(res.currentPage).toBe(1)
      expect(res.pageSize).toBe(5)
      expect(res.items.length).toBe(5)
      expect(res.startItem).toBe(1)
      expect(res.endItem).toBe(5)
    })

    it('paginates 5,000 nodes correctly for page size 10', () => {
      const res = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 2, 10)
      expect(res.totalPages).toBe(500)
      expect(res.currentPage).toBe(2)
      expect(res.pageSize).toBe(10)
      expect(res.items.length).toBe(10)
      expect(res.startItem).toBe(11)
      expect(res.endItem).toBe(20)
    })

    it('paginates 5,000 nodes correctly for page size 100', () => {
      const res = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 1, 100)
      expect(res.totalPages).toBe(50)
      expect(res.pageSize).toBe(100)
      expect(res.items.length).toBe(100)
    })

    it('paginates 5,000 nodes correctly for page size 1000', () => {
      const res = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 1, 1000)
      expect(res.totalPages).toBe(5)
      expect(res.pageSize).toBe(1000)
      expect(res.items.length).toBe(1000)
    })

    it('supports arbitrary custom page size (e.g. 37 or 2500)', () => {
      const res37 = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 1, 37)
      expect(res37.totalPages).toBe(Math.ceil(5000 / 37))
      expect(res37.items.length).toBe(37)

      const res2500 = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 2, 2500)
      expect(res2500.totalPages).toBe(2)
      expect(res2500.currentPage).toBe(2)
      expect(res2500.items.length).toBe(2500)
      expect(res2500.startItem).toBe(2501)
      expect(res2500.endItem).toBe(5000)
    })

    it('clamps page index cleanly if requested page exceeds totalPages', () => {
      const clamped = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'all', 99999, 100)
      expect(clamped.currentPage).toBe(50)
      expect(clamped.items.length).toBe(100)
    })

    it('real-time omni-searches across hostname, IP, OU path, and matched rules', () => {
      // Search by hostname
      const beckhoffSearch = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, 'BECKHOFF', 'all', 1, 10)
      expect(beckhoffSearch.filteredTotal).toBe(500) // 5,000 / 10 = 500
      expect(beckhoffSearch.totalPages).toBe(50)
      expect(beckhoffSearch.items.every(i => i.hostname.includes('BECKHOFF'))).toBe(true)

      // Search by IP subnet
      const ipSearch = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '10.10.1.', 'all', 1, 10)
      expect(ipSearch.filteredTotal).toBeGreaterThan(0)
      expect(ipSearch.items.every(i => i.ipAddress.includes('10.10.1.'))).toBe(true)

      // Filter by Rule mode
      const ruleOnly = filterAndPaginateControllers(largeFleet, largeEvals, templateNames, '', 'rule', 1, 10)
      expect(ruleOnly.filteredTotal).toBeGreaterThan(0)
      expect(ruleOnly.items.every(i => largeEvals[i.id].assignmentMode === 'rule')).toBe(true)
    })

    it('sorts controllers ascending, descending, and restores natural order on 3rd click (null)', () => {
      const sampleControllers = [
        { id: 'pc-c', hostname: 'Charlie-PC', ipAddress: '10.10.1.30', telemetry: { isOnline: false, cpuUsagePercent: 10 } },
        { id: 'pc-a', hostname: 'Alpha-PC', ipAddress: '10.10.1.10', telemetry: { isOnline: true, cpuUsagePercent: 50 } },
        { id: 'pc-b', hostname: 'Bravo-PC', ipAddress: '10.10.1.20', telemetry: { isOnline: true, cpuUsagePercent: 20 } }
      ]
      const evals = evaluateFleetAssignments(sampleControllers, [], {}, 'standard-factory-baseline')

      // Click 1: Ascending sort by hostname
      const ascRes = filterAndPaginateControllers(sampleControllers, evals, templateNames, '', 'all', 1, 10, 'hostname', 'asc')
      expect(ascRes.items.map(i => i.hostname)).toEqual(['Alpha-PC', 'Bravo-PC', 'Charlie-PC'])

      // Click 2: Descending sort by hostname
      const descRes = filterAndPaginateControllers(sampleControllers, evals, templateNames, '', 'all', 1, 10, 'hostname', 'desc')
      expect(descRes.items.map(i => i.hostname)).toEqual(['Charlie-PC', 'Bravo-PC', 'Alpha-PC'])

      // Click 3: Undo sorting (null) -> restores original order
      const resetRes = filterAndPaginateControllers(sampleControllers, evals, templateNames, '', 'all', 1, 10, 'hostname', null)
      expect(resetRes.items.map(i => i.hostname)).toEqual(['Charlie-PC', 'Alpha-PC', 'Bravo-PC'])

      // Sort by Status (Online first in ascending)
      const statusAsc = filterAndPaginateControllers(sampleControllers, evals, templateNames, '', 'all', 1, 10, 'status', 'asc')
      expect(statusAsc.items[0].telemetry.isOnline).toBe(false)
      expect(statusAsc.items[2].telemetry.isOnline).toBe(true)

      const statusDesc = filterAndPaginateControllers(sampleControllers, evals, templateNames, '', 'all', 1, 10, 'status', 'desc')
      expect(statusDesc.items[0].telemetry.isOnline).toBe(true)
      expect(statusDesc.items[2].telemetry.isOnline).toBe(false)
    })
  })
})
