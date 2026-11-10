import { ref, computed } from 'vue'
import type {
  GenericTemplate,
  CreateGenericTemplateInput,
  UpdateGenericTemplateInput,
  DeriveTemplateInput,
  TemplateResolutionResult,
  TemplateInstantiationResult,
  TemplateValidationResult,
  TemplateTypeDefinition
} from '~/types/templates'

export function useGenericTemplates() {
  const templates = ref<GenericTemplate[]>([])
  const templateTypes = ref<TemplateTypeDefinition[]>([])
  const activeTemplateId = ref<string | null>(null)
  const effectiveResolution = ref<TemplateResolutionResult | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<string | null>(null)

  // Filters
  const selectedTypeFilter = ref<string>('all')
  const selectedCategoryFilter = ref<string>('all')
  const searchQuery = ref<string>('')

  const activeTemplate = computed<GenericTemplate | null>(() => {
    if (!activeTemplateId.value) return null
    return templates.value.find(t => t.id === activeTemplateId.value) || null
  })

  const availableCategories = computed<string[]>(() => {
    const set = new Set<string>()
    for (const t of templates.value) {
      if (t.category) set.add(t.category)
    }
    return Array.from(set).sort()
  })

  const filteredTemplates = computed<GenericTemplate[]>(() => {
    return templates.value.filter(t => {
      if (selectedTypeFilter.value !== 'all') {
        if (t.templateType.toLowerCase() !== selectedTypeFilter.value.toLowerCase()) {
          return false
        }
      }
      if (selectedCategoryFilter.value !== 'all') {
        if (t.category.toLowerCase() !== selectedCategoryFilter.value.toLowerCase()) {
          return false
        }
      }
      if (searchQuery.value.trim()) {
        const q = searchQuery.value.trim().toLowerCase()
        const matchesCode = t.code.toLowerCase().includes(q)
        const matchesName = t.name.toLowerCase().includes(q)
        const matchesDesc = t.description?.toLowerCase().includes(q)
        const matchesTag = t.tags?.some(tag => tag.toLowerCase().includes(q))
        if (!matchesCode && !matchesName && !matchesDesc && !matchesTag) {
          return false
        }
      }
      return true
    })
  })

  async function fetchTemplates() {
    isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<GenericTemplate[]>('/api/v1/templates')
      templates.value = Array.isArray(data) ? data : []
      if (!activeTemplateId.value && templates.value.length > 0) {
        activeTemplateId.value = templates.value[0].id
        await loadEffectiveResolution(templates.value[0].id)
      }
    } catch (e: any) {
      error.value = e?.data?.error || e?.message || 'Failed to load templates'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchTemplateTypes() {
    try {
      const data = await $fetch<TemplateTypeDefinition[]>('/api/v1/templates/types')
      if (Array.isArray(data)) {
        templateTypes.value = data
      }
    } catch (e: any) {
      console.warn('Failed to load template types:', e)
    }
  }

  async function selectTemplate(target: GenericTemplate | string) {
    const id = typeof target === 'string' ? target : target.id
    activeTemplateId.value = id
    await loadEffectiveResolution(id)
  }

  async function loadEffectiveResolution(templateId: string) {
    try {
      const res = await $fetch<TemplateResolutionResult>(`/api/v1/templates/${templateId}/resolve`)
      effectiveResolution.value = res
      return res
    } catch (e: any) {
      console.warn(`Failed to resolve effective schema for template ${templateId}:`, e)
      effectiveResolution.value = null
      return null
    }
  }

  async function createTemplate(input: CreateGenericTemplateInput): Promise<GenericTemplate> {
    isSaving.value = true
    error.value = null
    try {
      const created = await $fetch<GenericTemplate>('/api/v1/templates', {
        method: 'POST',
        body: input
      })
      templates.value = [created, ...templates.value]
      activeTemplateId.value = created.id
      await loadEffectiveResolution(created.id)
      return created
    } catch (e: any) {
      const msg = e?.data?.error || e?.message || 'Failed to create template'
      error.value = msg
      throw new Error(msg)
    } finally {
      isSaving.value = false
    }
  }

  async function updateTemplate(id: string, input: UpdateGenericTemplateInput): Promise<GenericTemplate> {
    isSaving.value = true
    error.value = null
    try {
      const updated = await $fetch<GenericTemplate>(`/api/v1/templates/${id}`, {
        method: 'PUT',
        body: input
      })
      const idx = templates.value.findIndex(t => t.id === id)
      if (idx !== -1) {
        templates.value[idx] = updated
      }
      if (activeTemplateId.value === id) {
        await loadEffectiveResolution(id)
      }
      return updated
    } catch (e: any) {
      const msg = e?.data?.error || e?.message || 'Failed to update template'
      error.value = msg
      throw new Error(msg)
    } finally {
      isSaving.value = false
    }
  }

  async function deleteTemplate(id: string): Promise<boolean> {
    isSaving.value = true
    error.value = null
    try {
      await $fetch(`/api/v1/templates/${id}`, {
        method: 'DELETE'
      })
      templates.value = templates.value.filter(t => t.id !== id)
      if (activeTemplateId.value === id) {
        activeTemplateId.value = templates.value[0]?.id || null
        if (activeTemplateId.value) {
          await loadEffectiveResolution(activeTemplateId.value)
        } else {
          effectiveResolution.value = null
        }
      }
      return true
    } catch (e: any) {
      const msg = e?.data?.error || e?.message || 'Failed to delete template'
      error.value = msg
      return false
    } finally {
      isSaving.value = false
    }
  }

  async function deriveTemplate(sourceId: string, input: DeriveTemplateInput): Promise<GenericTemplate> {
    isSaving.value = true
    error.value = null
    try {
      const derived = await $fetch<GenericTemplate>(`/api/v1/templates/${sourceId}/derive`, {
        method: 'POST',
        body: input
      })
      templates.value = [derived, ...templates.value]
      activeTemplateId.value = derived.id
      await loadEffectiveResolution(derived.id)
      return derived
    } catch (e: any) {
      const msg = e?.data?.error || e?.message || 'Failed to derive template'
      error.value = msg
      throw new Error(msg)
    } finally {
      isSaving.value = false
    }
  }

  async function validateInstanceData(templateId: string, values: Record<string, any>): Promise<TemplateValidationResult> {
    try {
      return await $fetch<TemplateValidationResult>(`/api/v1/templates/${templateId}/validate`, {
        method: 'POST',
        body: values
      })
    } catch (e: any) {
      return {
        isValid: false,
        errors: [e?.data?.error || e?.message || 'Validation request failed'],
        warnings: []
      }
    }
  }

  async function instantiateTemplate(
    templateId: string,
    values: Record<string, any>,
    contextParameters?: Record<string, any>
  ): Promise<TemplateInstantiationResult> {
    try {
      return await $fetch<TemplateInstantiationResult>(`/api/v1/templates/${templateId}/instantiate`, {
        method: 'POST',
        body: {
          values,
          contextParameters
        }
      })
    } catch (e: any) {
      return {
        success: false,
        instanceData: {},
        errors: [e?.data?.error || e?.message || 'Instantiation request failed'],
        appliedSchema: []
      }
    }
  }

  function exportTemplateAsJson(template: GenericTemplate): string {
    const payload = {
      $schema: 'https://heimdall.industrial/schemas/v1/generic-template.json',
      templateType: template.templateType,
      code: template.code,
      name: template.name,
      description: template.description,
      category: template.category,
      icon: template.icon,
      version: template.version,
      tags: template.tags,
      schema: template.schema,
      defaultValues: template.defaultValues,
      uiConfig: template.uiConfig
    }
    return JSON.stringify(payload, null, 2)
  }

  function parseImportedJson(jsonString: string): CreateGenericTemplateInput {
    const parsed = JSON.parse(jsonString)
    if (!parsed.code || !parsed.name) {
      throw new Error('Imported JSON must specify at least "code" and "name".')
    }
    return {
      templateType: parsed.templateType || 'component',
      code: String(parsed.code).toUpperCase().trim(),
      name: String(parsed.name).trim(),
      description: parsed.description,
      category: parsed.category || 'General',
      icon: parsed.icon || 'Layers',
      version: parsed.version || '1.0.0',
      tags: Array.isArray(parsed.tags) ? parsed.tags : [],
      schema: Array.isArray(parsed.schema) ? parsed.schema : [],
      defaultValues: parsed.defaultValues && typeof parsed.defaultValues === 'object' ? parsed.defaultValues : {},
      uiConfig: parsed.uiConfig,
      isActive: true
    }
  }

  return {
    templates,
    templateTypes,
    activeTemplateId,
    activeTemplate,
    effectiveResolution,
    isLoading,
    isSaving,
    error,
    selectedTypeFilter,
    selectedCategoryFilter,
    searchQuery,
    availableCategories,
    filteredTemplates,
    fetchTemplates,
    fetchTemplateTypes,
    selectTemplate,
    loadEffectiveResolution,
    createTemplate,
    updateTemplate,
    deleteTemplate,
    deriveTemplate,
    validateInstanceData,
    instantiateTemplate,
    exportTemplateAsJson,
    parseImportedJson
  }
}
