import { ref, onMounted } from 'vue'
import type { BaseInventoryItem, Manufacturer, Supplier } from '~/types/domain'

export const useInventoryAssets = () => {
  const items = ref<BaseInventoryItem[]>([])
  const manufacturers = ref<Manufacturer[]>([])
  const suppliers = ref<Supplier[]>([])
  const selectedItem = ref<BaseInventoryItem | null>(null)
  const isLoading = ref(false)
  const activeTab = ref<'hardware' | 'software' | 'hierarchy'>('hardware')

  const fetchItems = async (type: 'hardware' | 'software' | 'all' = activeTab.value === 'hierarchy' ? 'all' : activeTab.value) => {
    isLoading.value = true
    try {
      const res = await $fetch<{ items: any[]; totalCount: number }>('/api/inventory/filter', {
        method: 'POST',
        body: { type }
      })
      if (res && res.items) {
        items.value = res.items
      }
    } catch {
      // Direct proxy fallback
      try {
        const raw = await $fetch<any[]>('/api/proxy/inventory')
        items.value = (raw || []).map(r => ({
          id: r.id,
          name: r.name,
          displayName: r.displayName,
          itemType: r.itemType || 'HardwareComponent',
          serialNumber: r.serialNumber,
          costInHUF: r.costInHUF,
          purchaseDate: r.purchaseDate,
          manufacturer: r.manufacturer,
          responsibleTeams: r.responsibleTeams || [],
          metadata: r.metadata || {}
        }))
      } catch {
        items.value = []
      }
    } finally {
      isLoading.value = false
    }
  }

  const fetchMetadata = async () => {
    try {
      const [mfrs, supps] = await Promise.all([
        $fetch<Manufacturer[]>('/api/proxy/inventory/manufacturers'),
        $fetch<Supplier[]>('/api/proxy/inventory/suppliers')
      ])
      manufacturers.value = mfrs || []
      suppliers.value = supps || []
    } catch {
      // Ignore reference fetch errors in dev
    }
  }

  const getItemById = async (id: string): Promise<BaseInventoryItem | null> => {
    try {
      const item = await $fetch<any>(`/api/proxy/inventory/${id}`)
      selectedItem.value = item
      return item
    } catch {
      return null
    }
  }

  const provisionAsset = async (payload: any) => {
    const res = await $fetch('/api/proxy/inventory', {
      method: 'POST',
      body: payload
    })
    await fetchItems()
    return res
  }

  onMounted(() => {
    fetchItems()
    fetchMetadata()
  })

  return {
    items,
    manufacturers,
    suppliers,
    selectedItem,
    isLoading,
    activeTab,
    fetchItems,
    getItemById,
    provisionAsset
  }
}
