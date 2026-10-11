/**
 * OU & Asset Tag Telemetry Configuration Rule Engine
 * Handles Host DNA token extraction, priority rule evaluation, and high-scale fleet filtering & pagination.
 */
import type { OuTagRecipeRule } from '~/types/telemetry'

export interface HostDna {
  id: string
  hostname: string
  adOuPath: string
  ouTokens: string[]
  tokens: Set<string>
  displayTags: string[]
  tagKeyValues: Record<string, string>
}

/**
 * Tokenizes arbitrary strings into lowercase word tokens, handling punctuation, dashes, and underscores.
 */
export function tokenizeText(text: string | null | undefined): string[] {
  if (!text) return []
  return text
    .toLowerCase()
    .split(/[\s,_\-./\\;:=+*#@!%^&|()\[\]{}]+/)
    .map(t => t.trim())
    .filter(t => t.length > 0)
}

/**
 * Normalizes wildcard glob patterns (e.g. "*LINE-A*", "OU=Fastening*") into regex matching.
 */
export function matchPattern(pattern: string, target: string): boolean {
  if (!pattern || pattern === '*') return true
  if (!target) return false

  const normalizedPattern = pattern.trim().toLowerCase()
  const normalizedTarget = target.trim().toLowerCase()

  // Simple substring match if no wildcards
  if (!normalizedPattern.includes('*') && !normalizedPattern.includes('?')) {
    return normalizedTarget.includes(normalizedPattern)
  }

  // Convert glob to regex
  const regexString = '^' + normalizedPattern
    .replace(/[.+^${}()|[\]\\]/g, '\\$&')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.') + '$'

  try {
    const regex = new RegExp(regexString, 'i')
    return regex.test(normalizedTarget)
  } catch {
    return normalizedTarget.includes(normalizedPattern.replace(/\*/g, ''))
  }
}

/**
 * Extracts searchable Host DNA (tokens, key-values, detected tags) from a controller object.
 */
export function extractHostDna(controller: any): HostDna {
  const tokens = new Set<string>()
  const tagKeyValues: Record<string, string> = {}
  const displayTagsSet = new Set<string>()

  const id = controller.id || controller.Id || ''
  const hostname = controller.hostname || controller.name || 'Unknown'
  const adOuPath = controller.adOuPath || controller.AdOuPath || controller.systemMetadata?.AdOuPath || ''

  // 1. Hostname and identifiers
  tokenizeText(hostname).forEach(t => tokens.add(t))
  tokenizeText(controller.displayName).forEach(t => tokens.add(t))
  tokenizeText(controller.machineIdentifier).forEach(t => tokens.add(t))

  // 2. Active Directory OU tokens
  const ouTokens: string[] = []
  if (adOuPath) {
    // Extract values inside OU=...
    const ouMatches = adOuPath.match(/OU=([^,]+)/gi) || []
    ouMatches.forEach((m: string) => {
      const val = m.replace(/^OU=/i, '').trim()
      tokenizeText(val).forEach(t => {
        tokens.add(t)
        ouTokens.push(t)
        displayTagsSet.add(`OU:${val}`)
      })
    })
    tokenizeText(adOuPath).forEach(t => tokens.add(t))
  }

  // 3. Structured OU tags
  const rawOuTags = controller.ouTags || controller.OuTags || {}
  const ouTagsObj = typeof rawOuTags === 'string' ? (() => {
    try { return JSON.parse(rawOuTags) } catch { return {} }
  })() : rawOuTags

  if (ouTagsObj && typeof ouTagsObj === 'object') {
    Object.entries(ouTagsObj).forEach(([k, v]) => {
      const valStr = String(v).trim()
      const keyStr = String(k).trim()
      tagKeyValues[keyStr.toLowerCase()] = valStr.toLowerCase()

      tokenizeText(keyStr).forEach(t => tokens.add(t))
      tokenizeText(valStr).forEach(t => tokens.add(t))
      displayTagsSet.add(`${keyStr}: ${valStr}`)
    })
  }

  // 4. Reported Hardware Components & Inventory Items
  const items = controller.inventoryItems || controller.InventoryItems || []
  if (Array.isArray(items)) {
    items.forEach((item: any) => {
      tokenizeText(item.name).forEach(t => tokens.add(t))
      tokenizeText(item.displayName).forEach(t => tokens.add(t))
      tokenizeText(item.modelNumber).forEach(t => tokens.add(t))
      tokenizeText(item.itemType).forEach(t => tokens.add(t))
      tokenizeText(item.manufacturer?.name).forEach(t => tokens.add(t))

      if (item.name) displayTagsSet.add(item.name)
    })
  }

  // 5. Controlled production stations & machines
  const machines = controller.controlledMachines || controller.machines || []
  if (Array.isArray(machines)) {
    machines.forEach((m: any) => {
      tokenizeText(m.name).forEach(t => tokens.add(t))
      tokenizeText(m.machineType).forEach(t => tokens.add(t))
      tokenizeText(m.customIdentifier).forEach(t => tokens.add(t))
      if (m.machineType) displayTagsSet.add(m.machineType)
    })
  }

  // 6. System Metadata (OS, Manufacturer, BeckhoffRT, etc.)
  const meta = controller.systemMetadata || {}
  if (typeof meta === 'object') {
    Object.entries(meta).forEach(([k, v]) => {
      if (v) {
        const valStr = typeof v === 'object' ? JSON.stringify(v) : String(v)
        tagKeyValues[k.toLowerCase()] = valStr.toLowerCase()
        tokenizeText(k).forEach(t => tokens.add(t))
        tokenizeText(valStr).forEach(t => tokens.add(t))
      }
    })

    if (meta.BeckhoffRT) {
      tokens.add('beckhoff')
      tokens.add('beckhoffrt')
      tokens.add('rt')
      tokens.add('ethercat')
      tokens.add('ads')
      displayTagsSet.add('Beckhoff RT')
    }
  }

  // Common industrial heuristics & synonyms
  if (tokens.has('beckhoff') || tokens.has('twincat')) {
    tokens.add('ipc')
    tokens.add('controller')
    displayTagsSet.add('Beckhoff')
  }
  if (tokens.has('cognex') || tokens.has('camera') || tokens.has('vision')) {
    tokens.add('vision')
    tokens.add('inspector')
    displayTagsSet.add('Vision')
  }
  if (tokens.has('opc') || tokens.has('opcua') || tokens.has('modbus') || tokens.has('scada')) {
    tokens.add('scada')
    tokens.add('gateway')
    displayTagsSet.add('SCADA')
  }

  return {
    id,
    hostname,
    adOuPath,
    ouTokens,
    tokens,
    displayTags: Array.from(displayTagsSet).slice(0, 8),
    tagKeyValues
  }
}

/**
 * Checks whether a host matches an individual OU & Tag Recipe Rule.
 */
export function evaluateHostAgainstRule(
  host: HostDna,
  rule: OuTagRecipeRule
): { isMatch: boolean; matchedTags: string[]; matchedOuPattern?: string } {
  if (!rule.enabled) {
    return { isMatch: false, matchedTags: [] }
  }

  // 1. Evaluate Active Directory OU Patterns
  let ouMatched = true
  let matchedOuPattern: string | undefined = undefined

  if (rule.ouPatterns && rule.ouPatterns.length > 0) {
    const patterns = rule.ouPatterns.filter(p => p.trim().length > 0)
    if (patterns.length > 0) {
      if (rule.ouMatchMode === 'ALL') {
        ouMatched = patterns.every(pat => matchPattern(pat, host.adOuPath))
        if (ouMatched) matchedOuPattern = patterns.join(' & ')
      } else {
        // ANY mode
        const found = patterns.find(pat => matchPattern(pat, host.adOuPath))
        ouMatched = Boolean(found)
        matchedOuPattern = found
      }
    }
  }

  if (!ouMatched) {
    return { isMatch: false, matchedTags: [] }
  }

  // 2. Evaluate Tag / Asset Criteria
  const matchedTags: string[] = []

  if (rule.tags && rule.tags.length > 0) {
    const cleanedRuleTags = rule.tags.map(t => t.trim().toLowerCase()).filter(t => t.length > 0)

    for (const tag of cleanedRuleTags) {
      let tagFound = false

      // Direct token exact match
      if (host.tokens.has(tag)) {
        tagFound = true
      } else {
        // Substring check across tokens
        for (const hostToken of host.tokens) {
          if (hostToken.includes(tag) || tag.includes(hostToken)) {
            tagFound = true
            break
          }
        }
      }

      // Check key-value pairs (e.g. tag: "role:controller")
      if (!tagFound && tag.includes(':')) {
        const [k, v] = tag.split(':').map(s => s.trim())
        if (host.tagKeyValues[k] && host.tagKeyValues[k].includes(v)) {
          tagFound = true
        }
      }

      if (tagFound) {
        matchedTags.push(tag)
      }
    }

    if (rule.tagMatchMode === 'ALL') {
      if (matchedTags.length < cleanedRuleTags.length) {
        return { isMatch: false, matchedTags: [] }
      }
    } else {
      // ANY mode
      if (matchedTags.length === 0) {
        return { isMatch: false, matchedTags: [] }
      }
    }
  }

  return {
    isMatch: true,
    matchedTags,
    matchedOuPattern
  }
}

export interface HostAssignmentEvaluation {
  controllerId: string
  recipeId: string
  assignmentMode: 'rule' | 'manual' | 'default'
  matchedRule: OuTagRecipeRule | null
  matchedTags: string[]
  detectedTags: string[]
  adOuPath: string
  hostDna: HostDna
}

/**
 * Resolves recipe assignments across the fleet:
 * Evaluates rules in priority order (Rank 1, 2, 3...).
 * Preserves explicit manual overrides.
 * Falls back to default template.
 */
export function evaluateFleetAssignments(
  controllers: any[],
  rules: OuTagRecipeRule[],
  manualOverrides: Record<string, string> = {},
  defaultTemplateId: string = 'standard-factory-baseline'
): Record<string, HostAssignmentEvaluation> {
  const sortedRules = [...rules]
    .filter(r => r.enabled)
    .sort((a, b) => a.priority - b.priority)

  const results: Record<string, HostAssignmentEvaluation> = {}

  controllers.forEach(ctrl => {
    const dna = extractHostDna(ctrl)
    const ctrlId = ctrl.id

    // Check manual override first
    const manualRecipe = manualOverrides[ctrlId]
    if (manualRecipe) {
      // Check if a rule would have matched for badge info
      let matchedRule: OuTagRecipeRule | null = null
      let matchedTags: string[] = []

      for (const rule of sortedRules) {
        const evalRes = evaluateHostAgainstRule(dna, rule)
        if (evalRes.isMatch) {
          matchedRule = rule
          matchedTags = evalRes.matchedTags
          break
        }
      }

      results[ctrlId] = {
        controllerId: ctrlId,
        recipeId: manualRecipe,
        assignmentMode: 'manual',
        matchedRule,
        matchedTags,
        detectedTags: dna.displayTags,
        adOuPath: dna.adOuPath,
        hostDna: dna
      }
      return
    }

    // Evaluate rules in priority order
    let ruleMatchFound = false
    for (const rule of sortedRules) {
      const evalRes = evaluateHostAgainstRule(dna, rule)
      if (evalRes.isMatch) {
        results[ctrlId] = {
          controllerId: ctrlId,
          recipeId: rule.targetTemplateId,
          assignmentMode: 'rule',
          matchedRule: rule,
          matchedTags: evalRes.matchedTags,
          detectedTags: dna.displayTags,
          adOuPath: dna.adOuPath,
          hostDna: dna
        }
        ruleMatchFound = true
        break
      }
    }

    // Fall back to fleet default
    if (!ruleMatchFound) {
      results[ctrlId] = {
        controllerId: ctrlId,
        recipeId: defaultTemplateId,
        assignmentMode: 'default',
        matchedRule: null,
        matchedTags: [],
        detectedTags: dna.displayTags,
        adOuPath: dna.adOuPath,
        hostDna: dna
      }
    }
  })

  return results
}

export interface PaginationResult<T> {
  total: number
  filteredTotal: number
  totalPages: number
  currentPage: number
  pageSize: number
  startItem: number
  endItem: number
  items: T[]
}

/**
 * Real-time omni-search filter and configurable pagination.
 * Supports page sizes: 5, 10, 100, 1000, or any custom positive integer.
 */
export function filterAndPaginateControllers(
  controllers: any[],
  evaluations: Record<string, HostAssignmentEvaluation>,
  templateNamesById: Record<string, string>,
  query: string = '',
  filterMode: 'all' | 'rule' | 'manual' | 'default' = 'all',
  page: number = 1,
  pageSize: number = 10,
  sortKey?: string | null,
  sortOrder?: 'asc' | 'desc' | null
): PaginationResult<any> {
  const normalizedQuery = query.trim().toLowerCase()
  const effectivePageSize = Math.max(1, Number(pageSize) || 10)

  const filtered = controllers.filter(ctrl => {
    const evalData = evaluations[ctrl.id]
    if (!evalData) return false

    // Filter mode check
    if (filterMode !== 'all' && evalData.assignmentMode !== filterMode) {
      return false
    }

    // Query omni-search check
    if (!normalizedQuery) return true

    const host = ctrl.hostname || ''
    const ip = ctrl.ipAddress || ''
    const mac = ctrl.macAddress || ''
    const ou = evalData.adOuPath || ''
    const ruleName = evalData.matchedRule?.name || ''
    const templateName = templateNamesById[evalData.recipeId] || ''
    const tagsJoined = (evalData.detectedTags || []).join(' ')
    const tokensJoined = Array.from(evalData.hostDna?.tokens || []).join(' ')

    const searchableText = `${host} ${ip} ${mac} ${ou} ${ruleName} ${templateName} ${tagsJoined} ${tokensJoined}`.toLowerCase()
    return searchableText.includes(normalizedQuery)
  })

  // Optional 3-State Column Sorting
  if (sortKey && (sortOrder === 'asc' || sortOrder === 'desc')) {
    const modifier = sortOrder === 'desc' ? -1 : 1
    filtered.sort((a, b) => {
      const evalA = evaluations[a.id]
      const evalB = evaluations[b.id]

      let valA: any = ''
      let valB: any = ''

      switch (sortKey) {
        case 'hostname':
          valA = (a.hostname || a.id || '').toLowerCase()
          valB = (b.hostname || b.id || '').toLowerCase()
          break
        case 'ou':
          valA = (evalA?.adOuPath || (evalA?.detectedTags || []).join(' ') || '').toLowerCase()
          valB = (evalB?.adOuPath || (evalB?.detectedTags || []).join(' ') || '').toLowerCase()
          break
        case 'network':
          valA = (a.ipAddress || a.macAddress || '').toLowerCase()
          valB = (b.ipAddress || b.macAddress || '').toLowerCase()
          break
        case 'status': {
          const onlineA = a.telemetry?.isOnline ? 1 : 0
          const onlineB = b.telemetry?.isOnline ? 1 : 0
          if (onlineA !== onlineB) {
            return (onlineA - onlineB) * modifier
          }
          const cpuA = a.telemetry?.cpuUsagePercent ?? 0
          const cpuB = b.telemetry?.cpuUsagePercent ?? 0
          return (cpuA - cpuB) * modifier
        }
        case 'recipe': {
          const nameA = templateNamesById[evalA?.recipeId || ''] || evalA?.recipeId || ''
          const nameB = templateNamesById[evalB?.recipeId || ''] || evalB?.recipeId || ''
          valA = nameA.toLowerCase()
          valB = nameB.toLowerCase()
          break
        }
        default:
          valA = (a[sortKey] || '').toString().toLowerCase()
          valB = (b[sortKey] || '').toString().toLowerCase()
      }

      if (typeof valA === 'number' && typeof valB === 'number') {
        return (valA - valB) * modifier
      }
      return valA.toString().localeCompare(valB.toString()) * modifier
    })
  }

  const filteredTotal = filtered.length
  const totalPages = Math.max(1, Math.ceil(filteredTotal / effectivePageSize))
  const safePage = Math.min(Math.max(1, Number(page) || 1), totalPages)

  const startIndex = (safePage - 1) * effectivePageSize
  const endIndex = Math.min(startIndex + effectivePageSize, filteredTotal)
  const items = filtered.slice(startIndex, endIndex)

  return {
    total: controllers.length,
    filteredTotal,
    totalPages,
    currentPage: safePage,
    pageSize: effectivePageSize,
    startItem: filteredTotal === 0 ? 0 : startIndex + 1,
    endItem: endIndex,
    items
  }
}
