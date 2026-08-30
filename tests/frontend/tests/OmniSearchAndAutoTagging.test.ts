import { describe, it, expect } from 'vitest'
import { AutoTagEngine, calculateLevenshteinDistance } from '~/utils/search/AutoTagEngine'

describe('OmniSearch AutoTagEngine & Intelligence Pipeline', () => {
  const engine = new AutoTagEngine()

  describe('Damerau-Levenshtein Edit Distance', () => {
    it('calculates exact matches as distance 0', () => {
      expect(calculateLevenshteinDistance('Siemens', 'Siemens')).toBe(0)
      expect(calculateLevenshteinDistance('Beckhoff', 'beckhoff')).toBe(0)
    })

    it('calculates substitution distance correctly', () => {
      expect(calculateLevenshteinDistance('Siemns', 'Siemens')).toBe(1)
      expect(calculateLevenshteinDistance('Bekhoff', 'Beckhoff')).toBe(1)
    })

    it('handles transpositions (adjacent character swaps)', () => {
      expect(calculateLevenshteinDistance('Seimens', 'Siemens')).toBe(1)
    })
  })

  describe('Deterministic Regular Expression Entity Extraction', () => {
    it('extracts valid IPv4 addresses', () => {
      const results = engine.extractRegexEntities('Controller at 192.168.1.101 reporting offline')
      expect(results.length).toBe(1)
      expect(results[0].tag.key).toBe('ip')
      expect(results[0].tag.value).toBe('192.168.1.101')
      expect(results[0].confidence).toBeGreaterThanOrEqual(0.95)
    })

    it('extracts MAC addresses in standard notation', () => {
      const results = engine.extractRegexEntities('Device with MAC 02:65:54:CE:AE:FC configured')
      expect(results.length).toBe(1)
      expect(results[0].tag.key).toBe('mac')
      expect(results[0].tag.value).toBe('02:65:54:CE:AE:FC')
    })

    it('extracts industrial Station and Cell codes', () => {
      const results = engine.extractRegexEntities('Failure on LINE-A-OP10 station and OP20')
      expect(results.length).toBe(2)
      expect(results[0].tag.key).toBe('station')
      expect(results[0].tag.value).toBe('LINE-A-OP10')
      expect(results[1].tag.value).toBe('OP20')
    })

    it('extracts technical engineering specs with units', () => {
      const results = engine.extractRegexEntities('Spindle motor rated at 15kW and 400V 12000RPM')
      expect(results.length).toBe(3)
      const specValues = results.map(r => r.tag.value)
      expect(specValues).toContain('15KW')
      expect(specValues).toContain('400V')
      expect(specValues).toContain('12000RPM')
    })

    it('extracts price/cost constraints', () => {
      const results = engine.extractRegexEntities('Filter hardware with cost:>100k')
      expect(results.length).toBe(1)
      expect(results[0].tag.key).toBe('cost')
      expect(results[0].tag.value).toBe('100k')
    })
  })

  describe('Fuzzy Dictionary Matching & Typo Tolerance', () => {
    it('matches exact manufacturer names', () => {
      const results = engine.matchFuzzyEntities(['Siemens', 'KUKA'])
      expect(results.length).toBe(2)
      expect(results[0].tag.value).toBe('Siemens')
      expect(results[0].confidence).toBe(1.0)
    })

    it('tolerates typos in manufacturer names', () => {
      const results = engine.matchFuzzyEntities(['siemns', 'beckof'])
      expect(results.length).toBe(2)
      expect(results[0].tag.value).toBe('Siemens')
      expect(results[1].tag.value).toBe('Beckhoff')
      expect(results[0].confidence).toBeGreaterThanOrEqual(0.75)
    })

    it('matches equipment categories', () => {
      const results = engine.matchFuzzyEntities(['servo', 'plc', 'sensors'])
      expect(results.some(r => r.tag.value === 'Servo')).toBe(true)
      expect(results.some(r => r.tag.value === 'PLC')).toBe(true)
    })
  })

  describe('Explicit Tag Parsing', () => {
    it('parses explicit key:value tags from user query', () => {
      const { tags, remainingText } = engine.parseExplicitTags('manufacturer:Siemens OP10 type:hardware')
      expect(tags.length).toBe(2)
      expect(tags[0].key).toBe('manufacturer')
      expect(tags[0].value).toBe('Siemens')
      expect(tags[1].key).toBe('type')
      expect(tags[1].value).toBe('hardware')
      expect(remainingText).toBe('OP10')
    })
  })
})
