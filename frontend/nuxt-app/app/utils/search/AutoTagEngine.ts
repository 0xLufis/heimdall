import type { TagPill, AutoTagResult } from '~/types/search'

export interface KnownEntityDictionary {
  manufacturers: string[]
  categories: string[]
  statuses: string[]
  roles: string[]
  protocols: string[]
}

export const DEFAULT_DICTIONARY: KnownEntityDictionary = {
  manufacturers: [
    'Siemens',
    'Beckhoff',
    'KUKA',
    'Cognex',
    'IFM Electronic',
    'Festo',
    'Omron',
    'ABB',
    'WAGO',
    'Phoenix Contact',
    'Balluff',
    'Schneider Electric',
    'Fanuc',
    'Yaskawa',
    'Dell',
    'Advantech'
  ],
  categories: [
    'Servo',
    'PLC',
    'IPC',
    'SoftPLC',
    'Robot',
    'Motor',
    'Sensor',
    'Driver',
    'Camera',
    'Valve',
    'NIC',
    'CPU',
    'RAM',
    'Storage',
    'FieldbusCoupler',
    'DispenserHead'
  ],
  statuses: ['online', 'offline', 'critical', 'warning', 'resolved', 'open', 'in_progress', 'pending_parts'],
  roles: ['admin', 'engineer', 'technician', 'manager', 'operator', 'primary', 'secondary', 'safety'],
  protocols: ['EtherCAT', 'PROFINET', 'OPC_UA', 'ModbusTCP', 'EtherNet_IP']
}

/**
 * Calculates Damerau-Levenshtein edit distance between two strings.
 */
export function calculateLevenshteinDistance(a: string, b: string): number {
  const al = a.length
  const bl = b.length
  if (al === 0) return bl
  if (bl === 0) return al

  const matrix: number[][] = []
  for (let i = 0; i <= al; i++) {
    matrix[i] = [i]
  }
  for (let j = 0; j <= bl; j++) {
    matrix[0][j] = j
  }

  for (let i = 1; i <= al; i++) {
    for (let j = 1; j <= bl; j++) {
      const cost = a[i - 1].toLowerCase() === b[j - 1].toLowerCase() ? 0 : 1
      matrix[i][j] = Math.min(
        matrix[i - 1][j] + 1, // deletion
        matrix[i][j - 1] + 1, // insertion
        matrix[i - 1][j - 1] + cost // substitution
      )
      if (i > 1 && j > 1 && a[i - 1].toLowerCase() === b[j - 2].toLowerCase() && a[i - 2].toLowerCase() === b[j - 1].toLowerCase()) {
        matrix[i][j] = Math.min(matrix[i][j], matrix[i - 2][j - 2] + cost) // transposition
      }
    }
  }
  return matrix[al][bl]
}

export class AutoTagEngine {
  private dictionary: KnownEntityDictionary

  constructor(dictionary: KnownEntityDictionary = DEFAULT_DICTIONARY) {
    this.dictionary = dictionary
  }

  /**
   * Extracts deterministic entities matching regex specifications.
   */
  public extractRegexEntities(text: string): AutoTagResult[] {
    const results: AutoTagResult[] = []
    if (!text || text.trim().length === 0) return results

    // 1. IP Address pattern
    const ipRegex = /\b((?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b/g
    let match: RegExpExecArray | null
    while ((match = ipRegex.exec(text)) !== null) {
      results.push({
        tag: {
          id: `tag-ip-${match[0]}`,
          key: 'ip',
          value: match[0],
          label: `IP: ${match[0]}`,
          color: 'indigo',
          isAutoDetected: true
        },
        confidence: 0.98,
        matchedSubstring: match[0],
        source: 'regex'
      })
    }

    // 2. MAC Address pattern
    const macRegex = /\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b/g
    while ((match = macRegex.exec(text)) !== null) {
      results.push({
        tag: {
          id: `tag-mac-${match[0]}`,
          key: 'mac',
          value: match[0],
          label: `MAC: ${match[0]}`,
          color: 'slate',
          isAutoDetected: true
        },
        confidence: 0.99,
        matchedSubstring: match[0],
        source: 'regex'
      })
    }

    // 3. Station / Cell Identifiers (e.g. LINE-A-OP10, OP10, STATION-01)
    const stationRegex = /\b(?:LINE-[A-Z0-9]+(?:-[A-Z0-9]+)?|OP\d{1,3}|STATION-[A-Z0-9-]+)\b/gi
    while ((match = stationRegex.exec(text)) !== null) {
      const val = match[0].toUpperCase()
      results.push({
        tag: {
          id: `tag-station-${val}`,
          key: 'station',
          value: val,
          label: `Station: ${val}`,
          color: 'emerald',
          isAutoDetected: true
        },
        confidence: 0.95,
        matchedSubstring: match[0],
        source: 'regex'
      })
    }

    // 4. Numeric Specs with Units (e.g. 15kW, 400V, 30A, 12MP, 10Bar, 1000Nm, 12000RPM)
    const specRegex = /\b(\d+(?:\.\d+)?)\s*(kW|MW|V|kV|A|mA|MP|GB|TB|Nm|Bar|RPM|Hz|kHz|ms)\b/gi
    while ((match = specRegex.exec(text)) !== null) {
      const fullUnit = `${match[1]}${match[2].toUpperCase()}`
      results.push({
        tag: {
          id: `tag-spec-${fullUnit}`,
          key: 'spec',
          value: fullUnit,
          label: `Spec: ${fullUnit}`,
          color: 'amber',
          isAutoDetected: true
        },
        confidence: 0.92,
        matchedSubstring: match[0],
        source: 'regex'
      })
    }

    // 5. Price / Cost constraints (e.g. cost:>100k, cost:500k, >500000HUF, <2M)
    const costRegex = /\b(?:cost|price|huf)(?::|:>=|:<=|:>|:<|:=|>=|<=|=|>|<)\s*(\d+(?:\.\d+)?(?:k|m|huf)?)\b/gi
    while ((match = costRegex.exec(text)) !== null) {
      results.push({
        tag: {
          id: `tag-cost-${match[1]}`,
          key: 'cost',
          value: match[1],
          label: `Cost: ${match[1]}`,
          color: 'rose',
          isAutoDetected: true
        },
        confidence: 0.9,
        matchedSubstring: match[0],
        source: 'regex'
      })
    }

    return results
  }

  /**
   * Matches tokens against known entity dictionaries using fuzzy edit distance.
   */
  public matchFuzzyEntities(tokens: string[]): AutoTagResult[] {
    const results: AutoTagResult[] = []

    for (const token of tokens) {
      if (token.length < 3) continue
      const lower = token.toLowerCase()

      // Check Manufacturers
      for (const mfr of this.dictionary.manufacturers) {
        const mfrLower = mfr.toLowerCase()
        if (mfrLower === lower) {
          results.push({
            tag: {
              id: `tag-mfr-${mfr}`,
              key: 'manufacturer',
              value: mfr,
              label: `Manufacturer: ${mfr}`,
              color: 'blue',
              isAutoDetected: true
            },
            confidence: 1.0,
            matchedSubstring: token,
            source: 'fuzzy_dict'
          })
          break
        }
        // Allow typo of 1-2 edit distance depending on length
        const maxDist = mfrLower.length > 5 ? 2 : 1
        const dist = calculateLevenshteinDistance(lower, mfrLower)
        if (dist <= maxDist) {
          const confidence = 1 - dist / Math.max(lower.length, mfrLower.length)
          if (confidence >= 0.75) {
            results.push({
              tag: {
                id: `tag-mfr-${mfr}`,
                key: 'manufacturer',
                value: mfr,
                label: `Manufacturer: ${mfr}`,
                color: 'blue',
                isAutoDetected: true
              },
              confidence,
              matchedSubstring: token,
              source: 'fuzzy_dict'
            })
            break
          }
        }
      }

      // Check Categories
      for (const cat of this.dictionary.categories) {
        const catLower = cat.toLowerCase()
        if (catLower === lower) {
          results.push({
            tag: {
              id: `tag-cat-${cat}`,
              key: 'category',
              value: cat,
              label: `Category: ${cat}`,
              color: 'purple',
              isAutoDetected: true
            },
            confidence: 1.0,
            matchedSubstring: token,
            source: 'fuzzy_dict'
          })
          break
        }
        const maxDist = catLower.length > 5 ? 2 : 1
        const dist = calculateLevenshteinDistance(lower, catLower)
        if (dist <= maxDist) {
          const confidence = 1 - dist / Math.max(lower.length, catLower.length)
          if (confidence >= 0.75) {
            results.push({
              tag: {
                id: `tag-cat-${cat}`,
                key: 'category',
                value: cat,
                label: `Category: ${cat}`,
                color: 'purple',
                isAutoDetected: true
              },
              confidence,
              matchedSubstring: token,
              source: 'fuzzy_dict'
            })
            break
          }
        }
      }

      // Check Statuses
      for (const status of this.dictionary.statuses) {
        if (status === lower) {
          results.push({
            tag: {
              id: `tag-status-${status}`,
              key: 'status',
              value: status,
              label: `Status: ${status}`,
              color: status === 'online' ? 'emerald' : status === 'critical' ? 'rose' : 'amber',
              isAutoDetected: true
            },
            confidence: 1.0,
            matchedSubstring: token,
            source: 'fuzzy_dict'
          })
          break
        }
      }
    }

    return results
  }

  /**
   * Main pipeline function: analyzes free text and returns auto-tag recommendations.
   */
  public analyzeText(text: string): { freeTextRemaining: string; autoTags: AutoTagResult[] } {
    if (!text || text.trim().length === 0) {
      return { freeTextRemaining: '', autoTags: [] }
    }

    const regexTags = this.extractRegexEntities(text)
    const tokens = text.split(/\s+/).filter(t => t.length > 0)
    const fuzzyTags = this.matchFuzzyEntities(tokens)

    // Combine and deduplicate
    const combined = [...regexTags, ...fuzzyTags]
    const seen = new Set<string>()
    const uniqueTags: AutoTagResult[] = []

    for (const item of combined) {
      const key = `${item.tag.key}:${item.tag.value}`
      if (!seen.has(key)) {
        seen.add(key)
        uniqueTags.push(item)
      }
    }

    return {
      freeTextRemaining: text,
      autoTags: uniqueTags
    }
  }

  /**
   * Parses explicit key:value tags from input (e.g. `manufacturer:Siemens OP10 type:hardware`).
   */
  public parseExplicitTags(input: string): { tags: TagPill[]; remainingText: string } {
    const tags: TagPill[] = []
    if (!input) return { tags, remainingText: '' }

    // Match patterns like key:"quoted value" or key:value
    const tagRegex = /(\b[a-zA-Z0-9_-]+):(?:"([^"]+)"|([^\s]+))/g
    let remainingText = input
    let match: RegExpExecArray | null

    while ((match = tagRegex.exec(input)) !== null) {
      const key = match[1]
      const value = match[2] || match[3]
      tags.push({
        id: `tag-${key}-${value}`,
        key,
        value,
        label: `${key}: ${value}`,
        removable: true
      })
      remainingText = remainingText.replace(match[0], ' ')
    }

    return {
      tags,
      remainingText: remainingText.replace(/\s+/g, ' ').trim()
    }
  }
}

export const autoTagEngine = new AutoTagEngine()
