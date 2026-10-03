import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import {
  parseSearchQuery,
  shrinkContext,
  type SearchContext,
  type SearchSource
} from '~/utils/search/searchContext'
import {
  indexTable,
  getIndexedTagValues,
  matchesTagFilters
} from '~/utils/search/searchIndexer'
import { fzfMatch, highlightFzfMatches } from '~/utils/search/fzfSearch'
import { useTagContextSearch } from '~/composables/useTagContextSearch'
import TagContextSearch from '~/components/search/TagContextSearch.vue'
import {
  GLOBAL_SEARCH_CONTEXT,
  defaultMachinesSource,
  defaultClientPcsSource,
  defaultTicketsSource,
  defaultInventorySource
} from '~/utils/search/defaultContexts'

describe('Omnipresent Tag-Context OmniSearch & FZF Engine Suite', () => {
  describe('1. Search Query Parser & Negation Engine', () => {
    it('parses positive key-value tags and free-text queries', () => {
      const parsed = parseSearchQuery('status:online line:Line-1 KUKA robot')
      expect(parsed.tags).toHaveLength(2)
      expect(parsed.tags[0]).toEqual({
        key: 'status',
        value: 'online',
        inverted: false,
        raw: 'status:online'
      })
      expect(parsed.tags[1]).toEqual({
        key: 'line',
        value: 'Line-1',
        inverted: false,
        raw: 'line:Line-1'
      })
      expect(parsed.freeText).toBe('KUKA robot')
    })

    it('parses inverted negation tags with "!" prefix', () => {
      const parsed = parseSearchQuery('!manufacturer:Siemens !status:offline drive')
      expect(parsed.tags).toHaveLength(2)
      expect(parsed.tags[0]).toEqual({
        key: 'manufacturer',
        value: 'Siemens',
        inverted: true,
        raw: '!manufacturer:Siemens'
      })
      expect(parsed.tags[1]).toEqual({
        key: 'status',
        value: 'offline',
        inverted: true,
        raw: '!status:offline'
      })
      expect(parsed.freeText).toBe('drive')
    })

    it('detects active incomplete tags for code-completion popup', () => {
      const query = 'status:active mfr:sie'
      // Cursor at the end of 'mfr:sie'
      const parsed = parseSearchQuery(query, query.length)
      expect(parsed.activeIncompleteTag).toBeDefined()
      expect(parsed.activeIncompleteTag?.key).toBe('mfr')
      expect(parsed.activeIncompleteTag?.partialValue).toBe('sie')
      expect(parsed.activeIncompleteTag?.inverted).toBe(false)
    })

    it('detects inverted incomplete tags with empty substring for popup', () => {
      const query = '!status:'
      const parsed = parseSearchQuery(query, query.length)
      expect(parsed.activeIncompleteTag).toBeDefined()
      expect(parsed.activeIncompleteTag?.key).toBe('status')
      expect(parsed.activeIncompleteTag?.partialValue).toBe('')
      expect(parsed.activeIncompleteTag?.inverted).toBe(true)
    })
  })

  describe('2. Context Scoping & Dynamic Context Shrinkage', () => {
    it('shrinks active sources when type tag is applied', () => {
      const parsed = parseSearchQuery('type:machine')
      const shrunk = shrinkContext(GLOBAL_SEARCH_CONTEXT, parsed.tags)
      expect(shrunk.sources).toHaveLength(1)
      expect(shrunk.sources[0].entityType).toBe('Machine')
      expect(shrunk.allowedTags).toContain('tech')
      expect(shrunk.allowedTags).toContain('manufacturer')
    })

    it('excludes specified source when inverted type tag is applied', () => {
      const parsed = parseSearchQuery('!type:machine')
      const shrunk = shrinkContext(GLOBAL_SEARCH_CONTEXT, parsed.tags)
      expect(shrunk.sources.some(s => s.entityType === 'Machine')).toBe(false)
      expect(shrunk.sources.length).toBe(GLOBAL_SEARCH_CONTEXT.sources.length - 1)
    })
  })

  describe('3. Table Indexing & Tag Value Extraction', () => {
    it('indexes table columns and builds unique value dictionary', () => {
      const index = indexTable(defaultMachinesSource)
      expect(index.records.size).toBeGreaterThan(0)
      expect(index.tagValues.has('manufacturer')).toBe(true)

      const mfrValues = getIndexedTagValues([index], 'manufacturer')
      expect(mfrValues).toContain('KUKA')
      expect(mfrValues).toContain('Siemens')
      expect(mfrValues).toContain('Beckhoff')
    })

    it('filters records correctly with positive and negative tag criteria', () => {
      const index = indexTable(defaultMachinesSource)
      const kuka = index.records.get('mch-1')
      const siemens = index.records.get('mch-2')

      // Positive match
      expect(matchesTagFilters(kuka, [{ key: 'manufacturer', value: 'KUKA', inverted: false, raw: '' }], index)).toBe(true)
      expect(matchesTagFilters(siemens, [{ key: 'manufacturer', value: 'KUKA', inverted: false, raw: '' }], index)).toBe(false)

      // Inverted negation match
      expect(matchesTagFilters(siemens, [{ key: 'manufacturer', value: 'KUKA', inverted: true, raw: '' }], index)).toBe(true)
      expect(matchesTagFilters(kuka, [{ key: 'manufacturer', value: 'KUKA', inverted: true, raw: '' }], index)).toBe(false)
    })
  })

  describe('4. FZF Fuzzy Matching & Highlighting Algorithm', () => {
    it('scores exact prefix higher than interior matches', () => {
      const prefixMatch = fzfMatch('kuka', 'KUKA KR-210 R2700')
      const middleMatch = fzfMatch('210', 'KUKA KR-210 R2700')
      expect(prefixMatch.matches).toBe(true)
      expect(middleMatch.matches).toBe(true)
      expect(prefixMatch.score).toBeGreaterThan(middleMatch.score)
    })

    it('rewards delimiter word boundaries (CamelCase and punctuation)', () => {
      const boundaryMatch = fzfMatch('kr', 'KUKA KR-210')
      expect(boundaryMatch.matches).toBe(true)
      expect(boundaryMatch.indices).toEqual([5, 6])
    })

    it('generates HTML highlighted spans from matched indices', () => {
      const html = highlightFzfMatches('Beckhoff CX2040', [0, 1, 2, 3], 'highlight')
      expect(html).toContain('<span class="highlight">Beck</span>hoff CX2040')
    })
  })

  describe('5. TagContextSearch Vue Component UI', () => {
    it('mounts in docked mode and displays search input with default placeholder', () => {
      const wrapper = mount(TagContextSearch, {
        props: {
          mode: 'docked'
        }
      })

      expect(wrapper.exists()).toBe(true)
      const input = wrapper.find('[data-testid="tag-search-input"]')
      expect(input.exists()).toBe(true)
    })

    it('renders entity indicator badges next to search results', async () => {
      const wrapper = mount(TagContextSearch, {
        props: {
          mode: 'docked'
        }
      })

      const input = wrapper.find('[data-testid="tag-search-input"]')
      await input.setValue('KUKA')

      const resultsContainer = wrapper.find('[data-testid="search-results-container"]')
      expect(resultsContainer.exists()).toBe(true)

      const indicator = wrapper.find('[data-testid="entity-indicator"]')
      expect(indicator.exists()).toBe(true)
      expect(indicator.text()).toBe('MCH')
    })

    it('renders active tag filter chips when tag:value is present in query', async () => {
      const wrapper = mount(TagContextSearch, {
        props: {
          mode: 'docked'
        }
      })

      const input = wrapper.find('[data-testid="tag-search-input"]')
      await input.setValue('status:online !mfr:Siemens')

      const chips = wrapper.find('[data-testid="active-tag-chips"]')
      expect(chips.exists()).toBe(true)
      expect(chips.text()).toContain('status:')
      expect(chips.text()).toContain('online')
      expect(chips.text()).toContain('!mfr:')
      expect(chips.text()).toContain('Siemens')
    })
  })
})
