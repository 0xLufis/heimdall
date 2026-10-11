import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import {
  parseSearchQuery,
  shrinkContext,
  normalizeTagModifier
} from '~/utils/search/searchContext'
import {
  indexTable,
  matchesTagFilters
} from '~/utils/search/searchIndexer'
import {
  searchRegistry,
  SearchRegistry
} from '~/utils/search/searchRegistry'
import { useConfigurableSearch } from '~/composables/useConfigurableSearch'
import { useTagContextSearch } from '~/composables/useTagContextSearch'
import TagContextSearch from '~/components/search/TagContextSearch.vue'
import {
  defaultMachinesSource,
  GLOBAL_SEARCH_CONTEXT
} from '~/utils/search/defaultContexts'

describe('Registry-Based Multi-Entity Search & Tagging Syntax Suite', () => {
  describe('1. Tagging Search Syntax {modifier}{tag}#{value1},{valueN}{delimiter}', () => {
    it('parses standard tag with hash separator and multi-values delimited by space', () => {
      const parsed = parseSearchQuery('status#online,warning line#Line-1 KUKA')
      expect(parsed.tags).toHaveLength(2)

      expect(parsed.tags[0].key).toBe('status')
      expect(parsed.tags[0].values).toEqual(['online', 'warning'])
      expect(parsed.tags[0].separator).toBe('#')
      expect(parsed.tags[0].modifier).toBe('NONE')
      expect(parsed.tags[0].inverted).toBe(false)

      expect(parsed.tags[1].key).toBe('line')
      expect(parsed.tags[1].values).toEqual(['Line-1'])
      expect(parsed.tags[1].separator).toBe('#')

      expect(parsed.freeText).toBe('KUKA')
    })

    it('parses negating modifiers (!, -, NOT)', () => {
      const p1 = parseSearchQuery('!status#offline,error')
      expect(p1.tags[0].key).toBe('status')
      expect(p1.tags[0].modifier).toBe('NOT')
      expect(p1.tags[0].inverted).toBe(true)
      expect(p1.tags[0].values).toEqual(['offline', 'error'])

      const p2 = parseSearchQuery('-mfr#Siemens')
      expect(p2.tags[0].key).toBe('mfr')
      expect(p2.tags[0].modifier).toBe('NOT')
      expect(p2.tags[0].inverted).toBe(true)

      const p3 = parseSearchQuery('NOTpriority#Critical,High')
      expect(p3.tags[0].key).toBe('priority')
      expect(p3.tags[0].modifier).toBe('NOT')
      expect(p3.tags[0].inverted).toBe(true)
    })

    it('parses OR relation modifiers (|, OR)', () => {
      const p1 = parseSearchQuery('|status#online,warning')
      expect(p1.tags[0].key).toBe('status')
      expect(p1.tags[0].modifier).toBe('OR')
      expect(p1.tags[0].values).toEqual(['online', 'warning'])

      const p2 = parseSearchQuery('ORtech#PLC,Robotics')
      expect(p2.tags[0].key).toBe('tech')
      expect(p2.tags[0].modifier).toBe('OR')
      expect(p2.tags[0].values).toEqual(['PLC', 'Robotics'])
    })

    it('parses XOR relation modifiers (^, XOR)', () => {
      const p1 = parseSearchQuery('^line#Line-1,Line-2')
      expect(p1.tags[0].key).toBe('line')
      expect(p1.tags[0].modifier).toBe('XOR')
      expect(p1.tags[0].values).toEqual(['Line-1', 'Line-2'])

      const p2 = parseSearchQuery('XORstatus#online,warning')
      expect(p2.tags[0].key).toBe('status')
      expect(p2.tags[0].modifier).toBe('XOR')
      expect(p2.tags[0].values).toEqual(['online', 'warning'])
    })

    it('parses AND relation modifiers (+, &, AND)', () => {
      const p1 = parseSearchQuery('+tech#PLC,Robotics')
      expect(p1.tags[0].key).toBe('tech')
      expect(p1.tags[0].modifier).toBe('AND')

      const p2 = parseSearchQuery('&status#online')
      expect(p2.tags[0].key).toBe('status')
      expect(p2.tags[0].modifier).toBe('AND')

      const p3 = parseSearchQuery('ANDmfr#KUKA')
      expect(p3.tags[0].key).toBe('mfr')
      expect(p3.tags[0].modifier).toBe('AND')
    })

    it('detects active incomplete tag when cursor is editing a tag', () => {
      const query = 'status#online,w'
      const parsed = parseSearchQuery(query, query.length)

      expect(parsed.activeIncompleteTag).toBeDefined()
      expect(parsed.activeIncompleteTag?.key).toBe('status')
      expect(parsed.activeIncompleteTag?.separator).toBe('#')
      expect(parsed.activeIncompleteTag?.completedValues).toEqual(['online'])
      expect(parsed.activeIncompleteTag?.partialValue).toBe('w')
    })

    it('detects incomplete tag with modifier', () => {
      const query = '!mfr#sie'
      const parsed = parseSearchQuery(query, query.length)

      expect(parsed.activeIncompleteTag).toBeDefined()
      expect(parsed.activeIncompleteTag?.key).toBe('mfr')
      expect(parsed.activeIncompleteTag?.rawModifier).toBe('!')
      expect(parsed.activeIncompleteTag?.modifier).toBe('NOT')
      expect(parsed.activeIncompleteTag?.inverted).toBe(true)
      expect(parsed.activeIncompleteTag?.partialValue).toBe('sie')
    })

    it('does not trigger incomplete tag if cursor is past the space delimiter', () => {
      const query = 'status#online '
      // Cursor is at index 14 (after the space delimiter)
      const parsed = parseSearchQuery(query, query.length)
      expect(parsed.activeIncompleteTag).toBeUndefined()
    })
  })

  describe('2. Relational Matching Semantics (AND, OR, XOR, NOT)', () => {
    const table = indexTable(defaultMachinesSource)
    // Sample machines:
    // mch-1: KUKA KR-210 (KUKA, status: online, tech: Robotics, line: Line 1)
    // mch-2: Siemens S7-1500 (Siemens, status: online, tech: PLC, line: Line 1)
    // mch-3: Beckhoff CX2040 (Beckhoff, status: warning, tech: IPC, line: Line 2)
    // mch-5: Advantech MIC-770 (Advantech, status: offline, tech: IPC, line: Line 3)
    const kuka = table.records.get('mch-1')
    const siemens = table.records.get('mch-2')
    const beckhoff = table.records.get('mch-3')
    const advantech = table.records.get('mch-5')

    it('evaluates OR modifier: matches if item matches ANY of the values', () => {
      const orQuery = parseSearchQuery('|status#online,warning')
      expect(matchesTagFilters(kuka, orQuery.tags, table)).toBe(true) // online
      expect(matchesTagFilters(beckhoff, orQuery.tags, table)).toBe(true) // warning
      expect(matchesTagFilters(advantech, orQuery.tags, table)).toBe(false) // offline
    })

    it('evaluates NOT modifier: matches only if item matches NONE of the values', () => {
      const notQuery = parseSearchQuery('!status#offline,warning')
      expect(matchesTagFilters(kuka, notQuery.tags, table)).toBe(true) // online passes
      expect(matchesTagFilters(beckhoff, notQuery.tags, table)).toBe(false) // warning fails
      expect(matchesTagFilters(advantech, notQuery.tags, table)).toBe(false) // offline fails
    })

    it('evaluates XOR modifier: matches if item matches EXACTLY ONE of the values', () => {
      const xorQuery = parseSearchQuery('^status#online,warning')
      expect(matchesTagFilters(kuka, xorQuery.tags, table)).toBe(true) // 1 match: online
      expect(matchesTagFilters(beckhoff, xorQuery.tags, table)).toBe(true) // 1 match: warning
      expect(matchesTagFilters(advantech, xorQuery.tags, table)).toBe(false) // 0 matches: offline
    })

    it('evaluates AND modifier: requires all specified values to match', () => {
      // Single value under AND
      const andQuery = parseSearchQuery('+mfr#KUKA')
      expect(matchesTagFilters(kuka, andQuery.tags, table)).toBe(true)
      expect(matchesTagFilters(siemens, andQuery.tags, table)).toBe(false)
    })
  })

  describe('3. Search Registry & Tag Metadata Harvesting with Indicators', () => {
    it('registers native Heimdall entities with correct indicators', () => {
      const providers = searchRegistry.getAllProviders()
      const labels = providers.map(p => p.indicatorLabel)

      expect(labels).toContain('MCH')
      expect(labels).toContain('PRT')
      expect(labels).toContain('IPC')
      expect(labels).toContain('TCK')
      expect(labels).toContain('USR')
      expect(labels).toContain('TEL')
    })

    it('harvests scoped metadata for a tag key and associates entity indicators', () => {
      const statusMeta = searchRegistry.harvestTagMetadata('status')
      expect(statusMeta.length).toBeGreaterThan(0)

      const onlineItem = statusMeta.find(m => m.value.toLowerCase() === 'online')
      expect(onlineItem).toBeDefined()
      // Online is used by Machine (MCH) and ClientPc (IPC)
      expect(onlineItem?.indicatorLabels).toContain('MCH')
      expect(onlineItem?.indicatorLabels).toContain('IPC')
      expect(onlineItem?.indicators.some(i => i.label === 'MCH')).toBe(true)
    })

    it('scopes metadata recommendations by provider context', () => {
      // Ticketing workspace only
      const ticketingMeta = searchRegistry.harvestTagMetadata('status', ['tickets'])
      const labels = ticketingMeta.map(m => m.value.toLowerCase())

      expect(labels).toContain('open')
      expect(labels).toContain('inprogress')
      expect(labels).toContain('resolved')
      // Should not contain InStorage which belongs to inventory
      expect(labels).not.toContain('instorage')
    })

    it('scopes providers for telemetry workspace context and retrieves telemetry metrics', () => {
      const providers = searchRegistry.getProvidersForContext('telemetry')
      const providerIds = providers.map(p => p.id)
      expect(providerIds).toEqual(['telemetry'])
      expect(providerIds).not.toContain('client_pcs')
      expect(providerIds).not.toContain('machines')

      const telProvider = searchRegistry.getProvider('telemetry')
      expect(telProvider).toBeDefined()
      expect(telProvider?.indicatorLabel).toBe('TEL')
      expect(telProvider?.entityType).toBe('TelemetryMetric')
    })
  })

  describe('4. useConfigurableSearch Composable', () => {
    it('initializes and scopes tag recommendations when typing a tag', () => {
      const search = useConfigurableSearch()

      // User types status#
      search.searchQuery.value = 'status#'
      search.cursorPosition.value = 7

      expect(search.isEditingTag.value).toBe(true)
      expect(search.activeTag.value?.key).toBe('status')

      const recs = search.scopedTagRecommendations.value
      expect(recs.length).toBeGreaterThan(0)
      expect(recs.some(r => r.value.toLowerCase() === 'online')).toBe(true)
    })

    it('applies tag recommendation with delimiter space', () => {
      const search = useConfigurableSearch()

      search.searchQuery.value = 'status#on'
      search.cursorPosition.value = 9

      const recs = search.scopedTagRecommendations.value
      const onlineRec = recs.find(r => r.value.toLowerCase() === 'online')
      expect(onlineRec).toBeDefined()

      search.applyTagRecommendation(onlineRec!)
      // Should append space delimiter at the end
      expect(search.searchQuery.value).toBe('status#online ')
      expect(search.isEditingTag.value).toBe(false)
    })

    it('filters entity results and populates indicators', () => {
      const search = useConfigurableSearch()
      search.searchQuery.value = 'KUKA'

      const res = search.results.value
      expect(res.length).toBeGreaterThan(0)
      expect(res[0].indicatorLabel).toBe('MCH')
      expect(res[0].entityType).toBe('Machine')
    })
  })

  describe('5. TagContextSearch Vue Component UI with New Syntax', () => {
    it('renders scoped tag metadata recommendations with entity indicator pills', async () => {
      const wrapper = mount(TagContextSearch, {
        props: {
          mode: 'docked'
        }
      })

      const input = wrapper.find('[data-testid="tag-search-input"]')
      // Type incomplete tag to open scoped metadata popup
      await input.setValue('status#')
      await input.trigger('keyup')

      const popup = wrapper.find('[data-testid="tag-completion-popup"]')
      expect(popup.exists()).toBe(true)
      expect(popup.text()).toContain('status#')

      // Check entity indicator pills in recommendation feed
      expect(popup.text()).toContain('[MCH]')
      expect(popup.text()).toContain('[IPC]')
    })

    it('renders active tag filter chips for hash syntax with modifiers', async () => {
      const wrapper = mount(TagContextSearch, {
        props: {
          mode: 'docked'
        }
      })

      const input = wrapper.find('[data-testid="tag-search-input"]')
      await input.setValue('!status#offline,error ^line#Line-1,Line-2')

      const chips = wrapper.find('[data-testid="active-tag-chips"]')
      expect(chips.exists()).toBe(true)
      expect(chips.text()).toContain('!status#')
      expect(chips.text()).toContain('offline,error')
      expect(chips.text()).toContain('^line#')
      expect(chips.text()).toContain('Line-1,Line-2')
    })
  })

  describe('6. Search Registry Routing & Navigation Redirect Targets', () => {
    it('generates direct entity detail link for inventory components', () => {
      const provider = searchRegistry.getProvider('inventory')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'comp-uuid-999' })
      expect(link).toBe('/dashboard/inventory/comp-uuid-999')
    })

    it('generates direct entity detail link for staff & users', () => {
      const provider = searchRegistry.getProvider('users')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'user-alice' })
      expect(link).toBe('/dashboard/users/user-alice')
    })

    it('generates machines route with ID and search query for auto-filter & focus', () => {
      const provider = searchRegistry.getProvider('machines')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'm-123', name: 'Laser Welder' })
      expect(link).toBe('/dashboard/machines?id=m-123&search=Laser%20Welder')
    })

    it('generates client IPC route with selected ID and hostname', () => {
      const provider = searchRegistry.getProvider('client_pcs')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'ipc-456', hostname: 'IPC-LINE1-01' })
      expect(link).toBe('/dashboard/clients?selected=ipc-456&hostname=IPC-LINE1-01')
    })

    it('generates ticket route with ticketId for incident drawer opening', () => {
      const provider = searchRegistry.getProvider('tickets')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'ticket-789' })
      expect(link).toBe('/dashboard/tickets?ticketId=ticket-789')
    })

    it('generates templates route with section=templates and templateId', () => {
      const provider = searchRegistry.getProvider('templates')
      expect(provider).toBeDefined()
      const link = provider?.getLink?.({ id: 'tmpl-42' })
      expect(link).toBe('/dashboard/inventory?section=templates&templateId=tmpl-42')
    })
  })
})
