import { ref, computed, watch, onMounted } from 'vue'
import {
  evaluateTemplate,
  extractVariablesFromTemplate,
  validateJsonString,
  mapTemplateToAssetForm,
  generateUuid,
  type AssetTemplate,
  type AssetCategory,
  type TemplateVariableDefinition,
  type EvaluationResult
} from '~/utils/jsonTemplatingEngine'
import { DEFAULT_ASSET_TEMPLATES } from '~/utils/defaultAssetTemplates'

const STORAGE_KEY_CUSTOM_TEMPLATES = 'heimdall_custom_asset_templates_v1'

export function useJsonTemplateEngine() {
  const builtInTemplates = ref<AssetTemplate[]>([...DEFAULT_ASSET_TEMPLATES])
  const customTemplates = ref<AssetTemplate[]>([])
  const selectedTemplateId = ref<string>('')
  const selectedCategory = ref<string>('all')
  const variableValues = ref<Record<string, any>>({})
  const rawJsonText = ref<string>('')
  const rawJsonError = ref<string | null>(null)
  const isCustomMode = ref(false)

  // Load custom templates from localStorage
  const loadCustomTemplates = () => {
    if (typeof localStorage === 'undefined') return
    try {
      const stored = localStorage.getItem(STORAGE_KEY_CUSTOM_TEMPLATES)
      if (stored) {
        const parsed = JSON.parse(stored)
        if (Array.isArray(parsed)) {
          customTemplates.value = parsed
        }
      }
    } catch (e) {
      console.warn('Failed to load custom templates from localStorage:', e)
    }
  }

  // Save custom templates to localStorage
  const saveCustomTemplatesToStorage = () => {
    if (typeof localStorage === 'undefined') return
    try {
      localStorage.setItem(STORAGE_KEY_CUSTOM_TEMPLATES, JSON.stringify(customTemplates.value))
    } catch (e) {
      console.warn('Failed to persist custom templates:', e)
    }
  }

  onMounted(() => {
    loadCustomTemplates()
  })

  // All templates merged
  const allTemplates = computed<AssetTemplate[]>(() => {
    return [...customTemplates.value, ...builtInTemplates.value]
  })

  // Filtered by category
  const filteredTemplates = computed<AssetTemplate[]>(() => {
    if (selectedCategory.value === 'all') return allTemplates.value
    if (selectedCategory.value === 'custom') return customTemplates.value
    return allTemplates.value.filter(t => t.category.toLowerCase() === selectedCategory.value.toLowerCase())
  })

  // Currently active selected template
  const activeTemplate = computed<AssetTemplate | null>(() => {
    if (!selectedTemplateId.value) return null
    return allTemplates.value.find(t => t.id === selectedTemplateId.value) || null
  })

  // All variable definitions for currently active template
  const activeVariables = computed<TemplateVariableDefinition[]>(() => {
    if (!activeTemplate.value) return []
    const explicitVars = activeTemplate.value.variables || []
    const discoveredVarNames = extractVariablesFromTemplate(activeTemplate.value.template)

    const map = new Map<string, TemplateVariableDefinition>()
    for (const ev of explicitVars) {
      map.set(ev.name, ev)
    }

    for (const dv of discoveredVarNames) {
      if (!map.has(dv)) {
        map.set(dv, {
          name: dv,
          label: dv.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase()),
          type: 'string',
          defaultValue: '',
          required: false
        })
      }
    }

    return Array.from(map.values())
  })

  // Select a template and initialize its variable defaults
  const selectTemplate = (templateId: string) => {
    selectedTemplateId.value = templateId
    const template = allTemplates.value.find(t => t.id === templateId)
    if (template) {
      const initVals: Record<string, any> = {}
      const explicitVars = template.variables || []
      for (const v of explicitVars) {
        initVals[v.name] = v.defaultValue !== undefined ? v.defaultValue : ''
      }
      
      const discoveredVars = extractVariablesFromTemplate(template.template)
      for (const dv of discoveredVars) {
        if (initVals[dv] === undefined) {
          initVals[dv] = ''
        }
      }

      variableValues.value = initVals
      rawJsonText.value = JSON.stringify(template.template, null, 2)
      rawJsonError.value = null
      isCustomMode.value = false
    }
  }

  // Live evaluation of active template with current variableValues
  const evaluationResult = computed<EvaluationResult>(() => {
    if (!activeTemplate.value) {
      return {
        success: true,
        data: {},
        errors: [],
        unresolvedVariables: []
      }
    }

    return evaluateTemplate(activeTemplate.value.template, variableValues.value)
  })

  // Formatted evaluated JSON string for preview / code viewer
  const evaluatedJsonString = computed<string>(() => {
    return JSON.stringify(evaluationResult.value.data, null, 2)
  })

  // Set variable value
  const setVariable = (varName: string, value: any) => {
    variableValues.value = {
      ...variableValues.value,
      [varName]: value
    }
  }

  // Reset variable values to template defaults
  const resetVariables = () => {
    if (activeTemplate.value) {
      selectTemplate(activeTemplate.value.id)
    }
  }

  // Apply raw JSON editing mode
  const updateFromRawJson = (jsonString: string): boolean => {
    rawJsonText.value = jsonString
    const res = validateJsonString(jsonString)
    if (!res.valid) {
      rawJsonError.value = res.error || 'Invalid JSON syntax'
      return false
    }
    rawJsonError.value = null
    return true
  }

  // Create and save custom template
  const saveAsCustomTemplate = (newTemplate: Partial<AssetTemplate>): AssetTemplate => {
    const templateObj = typeof rawJsonText.value === 'string' && rawJsonText.value.trim() 
      ? JSON.parse(rawJsonText.value) 
      : (activeTemplate.value?.template || {})

    const custom: AssetTemplate = {
      id: `custom-${generateUuid().slice(0, 8)}`,
      name: newTemplate.name || 'Custom Asset Template',
      category: (newTemplate.category as AssetCategory) || 'General',
      icon: newTemplate.icon || 'Cpu',
      description: newTemplate.description || 'Custom user-defined asset template.',
      targetType: newTemplate.targetType || 'HardwareComponent',
      tags: ['Custom', ...(newTemplate.tags || [])],
      variables: newTemplate.variables || activeVariables.value,
      template: templateObj,
      isCustom: true,
      createdAt: new Date().toISOString()
    }

    customTemplates.value = [custom, ...customTemplates.value]
    saveCustomTemplatesToStorage()
    selectedTemplateId.value = custom.id
    return custom
  }

  // Delete custom template
  const deleteCustomTemplate = (id: string) => {
    customTemplates.value = customTemplates.value.filter(t => t.id !== id)
    saveCustomTemplatesToStorage()
    if (selectedTemplateId.value === id) {
      selectedTemplateId.value = ''
    }
  }

  // Export all custom templates as JSON string
  const exportTemplates = (): string => {
    return JSON.stringify(customTemplates.value, null, 2)
  }

  // Import custom templates from JSON string
  const importTemplates = (jsonStr: string): { count: number; error?: string } => {
    try {
      const parsed = JSON.parse(jsonStr)
      if (!Array.isArray(parsed)) {
        return { count: 0, error: 'Import payload must be an array of templates.' }
      }
      let count = 0
      for (const item of parsed) {
        if (item && item.name && item.template) {
          const imported: AssetTemplate = {
            id: item.id || `custom-${generateUuid().slice(0, 8)}`,
            name: item.name,
            category: item.category || 'General',
            icon: item.icon || 'Cpu',
            description: item.description || 'Imported template',
            targetType: item.targetType || 'HardwareComponent',
            tags: item.tags || ['Imported'],
            variables: item.variables || [],
            template: item.template,
            isCustom: true,
            createdAt: item.createdAt || new Date().toISOString()
          }
          // Avoid duplicate IDs
          customTemplates.value = customTemplates.value.filter(t => t.id !== imported.id)
          customTemplates.value.push(imported)
          count++
        }
      }
      saveCustomTemplatesToStorage()
      return { count }
    } catch (e: any) {
      return { count: 0, error: e.message }
    }
  }

  return {
    builtInTemplates,
    customTemplates,
    allTemplates,
    filteredTemplates,
    selectedTemplateId,
    selectedCategory,
    activeTemplate,
    activeVariables,
    variableValues,
    rawJsonText,
    rawJsonError,
    isCustomMode,
    evaluationResult,
    evaluatedJsonString,
    selectTemplate,
    setVariable,
    resetVariables,
    updateFromRawJson,
    saveAsCustomTemplate,
    deleteCustomTemplate,
    exportTemplates,
    importTemplates,
    mapTemplateToAssetForm
  }
}
