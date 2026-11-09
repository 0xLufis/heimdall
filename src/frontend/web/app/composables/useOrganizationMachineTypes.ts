import { ref, computed } from 'vue'

export interface OrganizationMachineTypesResponse {
  organizationId: string
  allCategories: string[]
  enabledCategories: string[]
  templates: Array<{
    id: string
    code: string
    name: string
    category: string
    icon?: string
    isEnabled: boolean
  }>
}

const allCategories = ref<string[]>([
  'SMT',
  'Inspection',
  'Soldering',
  'Depaneling',
  'Dispensing',
  'Fastening',
  'Testing',
  'Assembly',
  'Robotics'
])

const enabledCategories = ref<string[]>([...allCategories.value])
const machineTemplates = ref<any[]>([])
const isLoading = ref(false)
const isSaving = ref(false)

export function useOrganizationMachineTypes() {
  const fetchOrganizationTypes = async (orgId?: string) => {
    isLoading.value = true
    try {
      const url = orgId
        ? `/api/proxy/v1/Machine/organization-types?organizationId=${encodeURIComponent(orgId)}`
        : '/api/proxy/v1/Machine/organization-types'

      const data = await $fetch<OrganizationMachineTypesResponse>(url).catch(async () => {
        // Fallback to direct backend route or defaults
        return await $fetch<OrganizationMachineTypesResponse>('/api/v1/machine/organization-types').catch(() => null)
      })

      if (data) {
        if (data.allCategories?.length) allCategories.value = data.allCategories
        if (data.enabledCategories?.length) enabledCategories.value = data.enabledCategories
        if (data.templates?.length) machineTemplates.value = data.templates
      }
    } catch (err) {
      console.warn('[useOrganizationMachineTypes] Could not load organization machine types:', err)
    } finally {
      isLoading.value = false
    }
  }

  const saveOrganizationTypes = async (categories: string[], orgId?: string) => {
    isSaving.value = true
    try {
      const payload = {
        organizationId: orgId || '',
        enabledCategories: categories,
        enabledMachineCodes: []
      }

      await $fetch('/api/proxy/v1/Machine/organization-types', {
        method: 'PUT',
        body: payload
      }).catch(async () => {
        await $fetch('/api/v1/machine/organization-types', {
          method: 'PUT',
          body: payload
        })
      })

      enabledCategories.value = [...categories]
    } catch (err) {
      console.error('[useOrganizationMachineTypes] Failed to save machine types:', err)
      throw err
    } finally {
      isSaving.value = false
    }
  }

  const toggleCategory = async (cat: string) => {
    const current = new Set(enabledCategories.value)
    if (current.has(cat)) {
      current.delete(cat)
    } else {
      current.add(cat)
    }
    const updated = Array.from(current)
    await saveOrganizationTypes(updated)
  }

  const isCategoryEnabled = (cat?: string | null): boolean => {
    if (!cat) return true
    return enabledCategories.value.some(c => c.toLowerCase() === cat.trim().toLowerCase())
  }

  const isMachineTypeVisible = (machineType?: string | null): boolean => {
    if (!machineType) return true
    // If machineType directly matches a category
    if (isCategoryEnabled(machineType)) return true

    // Check if machine belongs to a known template whose category is enabled
    const tmpl = machineTemplates.value.find(
      t => t.code?.toLowerCase() === machineType.toLowerCase() ||
           t.name?.toLowerCase() === machineType.toLowerCase()
    )
    if (tmpl) {
      return isCategoryEnabled(tmpl.category)
    }

    // Default to true if not matching any known disabled category
    return true
  }

  return {
    allCategories,
    enabledCategories,
    machineTemplates,
    isLoading,
    isSaving,
    fetchOrganizationTypes,
    saveOrganizationTypes,
    toggleCategory,
    isCategoryEnabled,
    isMachineTypeVisible
  }
}
