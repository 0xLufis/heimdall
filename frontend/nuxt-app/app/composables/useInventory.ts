import { ref, watch } from 'vue'

export const useInventory = () => {
  const activeTab = ref<'hardware' | 'software'>('hardware')
  const loading = ref(false)
  const items = ref<any[]>([])
  
  const searchQuery = ref('')
  const filterCategory = ref('all')
  const filterMinTorque = ref<number | null>(null)
  const filterInterface = ref('all')

  const fetchData = async () => {
    loading.value = true
    try {
      let url = `/api/proxy/Inventory/${activeTab.value}`
      
      const hasHardwareFilters = activeTab.value === 'hardware' && (
        (filterCategory.value && filterCategory.value !== 'all') || 
        filterMinTorque.value || 
        (filterInterface.value && filterInterface.value !== 'all')
      )
      const hasSearchQuery = searchQuery.value.trim() !== ''

      if (hasHardwareFilters || hasSearchQuery) {
        url = `/api/proxy/Inventory/${activeTab.value}/search?`
        const params = new URLSearchParams()
        if (hasSearchQuery) params.append('query', searchQuery.value)
        
        if (activeTab.value === 'hardware') {
          if (filterCategory.value && filterCategory.value !== 'all') params.append('category', filterCategory.value)
          if (filterMinTorque.value) params.append('minTorque', filterMinTorque.value.toString())
          if (filterInterface.value && filterInterface.value !== 'all') params.append('interfaceType', filterInterface.value)
        }
        url += params.toString()
      }

      const data = await $fetch(url)
      if (data) {
        items.value = data as any[]
      }
    } catch (e) {
      console.error('Error fetching inventory:', e)
    } finally {
      loading.value = false
    }
  }

  const addComponent = async (type: 'hardware' | 'software', formData: any) => {
    try {
      await $fetch(`/api/proxy/Inventory/${type}`, {
        method: 'POST',
        body: formData
      })
      await fetchData()
      return { success: true }
    } catch (e: any) {
      console.error('Error adding component:', e)
      return { success: false, error: e }
    }
  }

  // Auto-fetch on tab change
  watch(activeTab, () => {
    fetchData()
  })

  return {
    activeTab,
    loading,
    items,
    searchQuery,
    filterCategory,
    filterMinTorque,
    filterInterface,
    fetchData,
    addComponent
  }
}
