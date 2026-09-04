/**
 * Heimdall Industrial JSON Templating Engine
 * Evaluates template expressions, variables, filters, and dynamic functions for OT/IT asset modeling.
 */

export type VariableType = 'string' | 'number' | 'boolean' | 'select'

export interface TemplateVariableDefinition {
  name: string
  label: string
  type: VariableType
  defaultValue?: any
  description?: string
  options?: string[]
  required?: boolean
  placeholder?: string
}

export type AssetCategory = 
  | 'Controller' 
  | 'Sensor' 
  | 'Vision' 
  | 'Motion' 
  | 'Software' 
  | 'Network' 
  | 'Dispensing' 
  | 'Safety' 
  | 'General'

export interface AssetTemplate {
  id: string
  name: string
  category: AssetCategory
  icon?: string
  description: string
  targetType: 'HardwareComponent' | 'SoftwareComponent' | 'Machine'
  tags?: string[]
  variables: TemplateVariableDefinition[]
  template: Record<string, any>
  isCustom?: boolean
  createdAt?: string
}

export interface EvaluationResult {
  success: boolean
  data: Record<string, any>
  errors: string[]
  unresolvedVariables: string[]
}

/**
 * Generates a standard random UUID v4
 */
export function generateUuid(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

/**
 * Generates random hexadecimal string
 */
export function generateRandomHex(length: number = 8): string {
  const chars = '0123456789ABCDEF'
  let result = ''
  for (let i = 0; i < length; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length))
  }
  return result
}

/**
 * Built-in transform filters
 */
export const TEMPLATE_FILTERS: Record<string, (val: any, arg?: string) => any> = {
  uppercase: (val: any) => String(val ?? '').toUpperCase(),
  lowercase: (val: any) => String(val ?? '').toLowerCase(),
  trim: (val: any) => String(val ?? '').trim(),
  slugify: (val: any) => String(val ?? '')
    .toLowerCase()
    .trim()
    .replace(/[^\w\s-]/g, '')
    .replace(/[\s_-]+/g, '-')
    .replace(/^-+|-+$/g, ''),
  default: (val: any, arg?: string) => (val !== undefined && val !== null && val !== '' ? val : (arg ?? '')),
  prefix: (val: any, arg?: string) => `${arg ?? ''}${val ?? ''}`,
  suffix: (val: any, arg?: string) => `${val ?? ''}${arg ?? ''}`,
  padzero: (val: any, arg?: string) => {
    const len = parseInt(arg || '2', 10)
    return String(val ?? '').padStart(len, '0')
  }
}

/**
 * Dynamic system variables generator
 */
export function getSystemVariables(): Record<string, any> {
  const now = new Date()
  return {
    $uuid: generateUuid(),
    $timestamp: now.toISOString(),
    $date: now.toISOString().split('T')[0],
    $year: now.getFullYear().toString(),
    $randomHex: generateRandomHex(8),
    $randomHex4: generateRandomHex(4),
    $randomHex12: generateRandomHex(12),
    $randomSerial: `SN-${generateRandomHex(8)}`
  }
}

/**
 * Extracts variable names from template strings/objects matching `{{varName | filter:arg}}`
 */
export function extractVariablesFromTemplate(obj: any): string[] {
  const vars = new Set<string>()
  const regex = /\{\{\s*([a-zA-Z0-9_$.]+)(?:\s*\|\s*[^}]+)?\s*\}\}/g

  function scan(val: any) {
    if (typeof val === 'string') {
      let match: RegExpExecArray | null
      while ((match = regex.exec(val)) !== null) {
        const varName = match[1]
        if (!varName.startsWith('$')) {
          vars.add(varName)
        }
      }
    } else if (Array.isArray(val)) {
      for (const item of val) scan(item)
    } else if (val && typeof val === 'object') {
      for (const key of Object.keys(val)) {
        scan(key)
        scan(val[key])
      }
    }
  }

  scan(obj)
  return Array.from(vars)
}

/**
 * Evaluates a single template string expression with given context and filters
 */
export function evaluateExpression(expr: string, context: Record<string, any>): any {
  const sysVars = getSystemVariables()
  const mergedContext = { ...sysVars, ...context }

  // Check if expression is a pure single variable reference: `{{var}}`
  const pureMatch = expr.match(/^\{\{\s*([a-zA-Z0-9_$.]+)(?:\s*\|\s*([^}]+))?\s*\}\}$/)
  if (pureMatch) {
    const varName = pureMatch[1]
    const filterChain = pureMatch[2]
    let value = mergedContext[varName]

    if (filterChain) {
      value = applyFilterChain(value, filterChain)
    }

    return value !== undefined ? value : ''
  }

  // Expression contains interpolation: `PLC-{{station}}-{{ip}}`
  return expr.replace(/\{\{\s*([a-zA-Z0-9_$.]+)(?:\s*\|\s*([^}]+))?\s*\}\}/g, (_, varName, filterChain) => {
    let value = mergedContext[varName]
    if (filterChain) {
      value = applyFilterChain(value, filterChain)
    }
    return value !== undefined && value !== null ? String(value) : ''
  })
}

/**
 * Applies a filter chain (e.g. `default: "24V" | uppercase`) to a value
 */
function applyFilterChain(initialValue: any, filterChain: string): any {
  let result = initialValue
  const filters = filterChain.split('|')

  for (const filterPart of filters) {
    const trimmed = filterPart.trim()
    if (!trimmed) continue

    const [filterName, ...argParts] = trimmed.split(':')
    const cleanFilterName = filterName.trim()
    let filterArg = argParts.join(':').trim()

    // Remove quotes around argument if present
    if (
      (filterArg.startsWith('"') && filterArg.endsWith('"')) ||
      (filterArg.startsWith("'") && filterArg.endsWith("'"))
    ) {
      filterArg = filterArg.slice(1, -1)
    }

    const filterFn = TEMPLATE_FILTERS[cleanFilterName]
    if (filterFn) {
      result = filterFn(result, filterArg)
    }
  }

  return result
}

/**
 * Recursively evaluates an entire template object using variable context
 */
export function evaluateTemplate(templateObj: any, context: Record<string, any>): EvaluationResult {
  const errors: string[] = []
  const unresolvedVars = new Set<string>()

  // Discover missing variables
  const allNeededVars = extractVariablesFromTemplate(templateObj)
  for (const v of allNeededVars) {
    if (context[v] === undefined || context[v] === null || context[v] === '') {
      unresolvedVars.add(v)
    }
  }

  function walk(val: any): any {
    if (typeof val === 'string') {
      try {
        const evalVal = evaluateExpression(val, context)
        // If string resulted in numeric/boolean conversion for single tokens
        if (typeof evalVal === 'string') {
          if (evalVal.trim() === 'true') return true
          if (evalVal.trim() === 'false') return false
        }
        return evalVal
      } catch (err: any) {
        errors.push(`Failed to evaluate string: "${val}": ${err.message}`)
        return val
      }
    } else if (Array.isArray(val)) {
      return val.map(item => walk(item))
    } else if (val !== null && typeof val === 'object') {
      const newObj: Record<string, any> = {}
      for (const [k, v] of Object.entries(val)) {
        const evalKey = typeof k === 'string' && k.includes('{{') ? evaluateExpression(k, context) : k
        newObj[evalKey] = walk(v)
      }
      return newObj
    }
    return val
  }

  const resultData = walk(templateObj)

  return {
    success: errors.length === 0,
    data: resultData,
    errors,
    unresolvedVariables: Array.from(unresolvedVars)
  }
}

/**
 * Validates a JSON string or object
 */
export function validateJsonString(jsonStr: string): { valid: boolean; error?: string; parsed?: any } {
  try {
    const parsed = JSON.parse(jsonStr)
    return { valid: true, parsed }
  } catch (e: any) {
    return { valid: false, error: e.message }
  }
}

/**
 * Standardizes mapping from evaluated template JSON into Heimdall Asset Form Model
 */
export function mapTemplateToAssetForm(evaluated: Record<string, any>) {
  const metadata: Record<string, any> = { ...(evaluated.metadata || evaluated.data || {}) }
  
  // Extract top-level known fields
  const name = evaluated.name || evaluated.Name || ''
  const displayName = evaluated.displayName || evaluated.DisplayName || ''
  const technology = evaluated.technology || evaluated.Technology || metadata.Technology || ''
  const serialNumber = evaluated.serialNumber || evaluated.SerialNumber || metadata.SerialNumber || ''
  const modelNumber = evaluated.modelNumber || evaluated.ModelNumber || metadata.ModelNumber || ''
  const costInHUF = evaluated.costInHUF || evaluated.CostInHUF || evaluated.cost || 0
  const quantity = evaluated.quantity || evaluated.Quantity || 1
  const itemType = evaluated.itemType || evaluated.ItemType || 'HardwareComponent'
  const manufacturerId = evaluated.manufacturerId || evaluated.manufacturer?.id || null
  const supplierId = evaluated.supplierId || evaluated.supplier?.id || null
  const machineId = evaluated.machineId || null
  const clientPcId = evaluated.clientPcId || null
  const parentId = evaluated.parentId || null
  const lateralLinkId = evaluated.lateralLinkId || null

  // Clean metadata from known primary fields
  delete metadata.SerialNumber
  delete metadata.CostInHUF
  delete metadata.ModelNumber
  delete metadata.Technology

  // If top-level object had other custom keys not in known fields, copy to metadata
  const knownKeys = new Set([
    'id', 'name', 'displayName', 'technology', 'serialNumber', 'modelNumber',
    'costInHUF', 'cost', 'quantity', 'itemType', 'manufacturerId', 'supplierId',
    'machineId', 'clientPcId', 'parentId', 'lateralLinkId', 'metadata', 'data',
    'manufacturer', 'supplier', 'responsibleTeams'
  ])

  for (const [k, v] of Object.entries(evaluated)) {
    if (!knownKeys.has(k) && !k.startsWith('$')) {
      metadata[k] = v
    }
  }

  return {
    name,
    displayName,
    technology,
    serialNumber,
    modelNumber,
    costInHUF: typeof costInHUF === 'number' ? costInHUF : parseFloat(costInHUF) || 0,
    quantity: typeof quantity === 'number' ? quantity : parseInt(quantity, 10) || 1,
    itemType,
    manufacturerId,
    supplierId,
    machineId,
    clientPcId,
    parentId,
    lateralLinkId,
    metadata
  }
}
