import { ref, computed, onMounted } from 'vue'
import type { TelemetryTemplate, DataPointDefinition } from '~/types/telemetry'
import { BUILTIN_TELEMETRY_TEMPLATES } from '~/utils/defaultTelemetryTemplates'

const customTemplates = ref<TelemetryTemplate[]>([])

export const useTelemetryTemplates = () => {
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isSaving = ref(false)

  const allTemplates = computed<TelemetryTemplate[]>(() => {
    return [...BUILTIN_TELEMETRY_TEMPLATES, ...customTemplates.value]
  })

  const fetchTemplates = async () => {
    isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<TelemetryTemplate[]>('/api/telemetry/templates')
      if (Array.isArray(data)) {
        customTemplates.value = data
      }
    } catch (e: any) {
      error.value = e?.message || 'Failed to fetch telemetry templates'
    } finally {
      isLoading.value = false
    }
  }

  const saveCustomTemplates = async () => {
    isSaving.value = true
    try {
      await $fetch('/api/telemetry/templates', {
        method: 'PUT',
        body: customTemplates.value
      })
      return true
    } catch (e: any) {
      error.value = e?.message || 'Failed to persist templates'
      return false
    } finally {
      isSaving.value = false
    }
  }

  const addTemplate = async (template: Omit<TelemetryTemplate, 'recipeId' | 'createdAt'>) => {
    const newTemplate: TelemetryTemplate = {
      ...template,
      recipeId: `custom-tpl-${Date.now()}`,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      isBuiltin: false
    }
    customTemplates.value = [newTemplate, ...customTemplates.value]
    await saveCustomTemplates()
    return newTemplate
  }

  const updateTemplate = async (recipeId: string, updates: Partial<TelemetryTemplate>) => {
    const idx = customTemplates.value.findIndex(t => t.recipeId === recipeId)
    if (idx !== -1) {
      customTemplates.value[idx] = {
        ...customTemplates.value[idx],
        ...updates,
        updatedAt: new Date().toISOString()
      }
      await saveCustomTemplates()
      return customTemplates.value[idx]
    }
    // If it was a built-in, convert to custom copy
    const builtin = BUILTIN_TELEMETRY_TEMPLATES.find(t => t.recipeId === recipeId)
    if (builtin) {
      const cloned: TelemetryTemplate = {
        ...builtin,
        ...updates,
        recipeId: `custom-tpl-${Date.now()}`,
        name: `${updates.name || builtin.name} (Custom)`,
        isBuiltin: false,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }
      customTemplates.value = [cloned, ...customTemplates.value]
      await saveCustomTemplates()
      return cloned
    }
    return null
  }

  const deleteTemplate = async (recipeId: string) => {
    customTemplates.value = customTemplates.value.filter(t => t.recipeId !== recipeId)
    await saveCustomTemplates()
  }

  const duplicateTemplate = async (template: TelemetryTemplate) => {
    const copy: TelemetryTemplate = {
      ...template,
      recipeId: `custom-tpl-${Date.now()}`,
      name: `${template.name} (Copy)`,
      isBuiltin: false,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
    customTemplates.value = [copy, ...customTemplates.value]
    await saveCustomTemplates()
    return copy
  }

  const simulateEvaluation = (template: TelemetryTemplate): Record<string, any> => {
    const timestamp = new Date().toISOString()
    const simulatedPoints: Record<string, any> = {}

    for (const dp of template.dataPoints) {
      let val: any
      switch (dp.sourceType) {
        case 'BeckhoffAds':
          val = dp.dataCategory === 'DeviceState' ? 'Run' : (Math.random() * 50 + 10).toFixed(2) + ' Nm'
          break
        case 'BeckhoffEtherCat':
          val = { crcErrors: 0, state: 'OP', cycleJitterUs: (Math.random() * 10 + 2).toFixed(1) }
          break
        case 'SystemCim':
          val = dp.pointId.includes('cpu') ? (Math.random() * 25 + 10).toFixed(1) + '%' : (Math.random() * 30 + 35).toFixed(1) + '%'
          break
        case 'SystemDisk':
          val = { 'C:': { freeGb: 48.2, totalGb: 256.0 }, 'D:': { freeGb: 82.5, totalGb: 512.0 } }
          break
        case 'OpcUaSubscription':
          val = { node: dp.pathOrSymbol, value: 'Operational', quality: 'Good_192' }
          break
        case 'ModbusTcp':
          val = [230.5, 231.2, 229.8, 14.8, 50.01]
          break
        default:
          val = 'OK'
      }
      simulatedPoints[dp.pointId] = {
        name: dp.name,
        category: dp.dataCategory,
        priority: dp.egressPriority,
        value: val,
        timestamp
      }
    }

    return {
      recipeId: template.recipeId,
      version: template.version,
      name: template.name,
      evaluatedAt: timestamp,
      targetPlatform: template.targetSelector.osPlatform,
      probesExecuted: template.dataPoints.length,
      payload: simulatedPoints
    }
  }

  return {
    allTemplates,
    customTemplates,
    isLoading,
    isSaving,
    error,
    fetchTemplates,
    addTemplate,
    updateTemplate,
    deleteTemplate,
    duplicateTemplate,
    simulateEvaluation
  }
}
