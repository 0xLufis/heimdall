import { ref, watch } from 'vue'

export const useInventory = () => {
  const activeTab = ref<'hardware' | 'software'>('hardware')
  const loading = ref(false)
  const items = ref<any[]>([])
  
  const searchQuery = ref('')
  const filterCategory = ref('')
  const filterMinTorque = ref<number | null>(null)
  const filterInterface = ref('')

  const fetchData = async () => {
    loading.value = true
    try {
      let url = `/api/proxy/Inventory/${activeTab.value}`
      if (activeTab.value === 'hardware' && (filterCategory.value || filterMinTorque.value || filterInterface.value)) {
        url = `/api/proxy/Inventory/hardware/search?`
        const params = new URLSearchParams()
        if (filterCategory.value) params.append('category', filterCategory.value)
        if (filterMinTorque.value) params.append('minTorque', filterMinTorque.value.toString())
        if (filterInterface.value) params.append('interfaceType', filterInterface.value)
        url += params.toString()
      }

      const { data } = await useFetch(url)
      if (data.value) {
        items.value = data.value as any[]
      }
    } catch (e) {
      console.error('Error fetching inventory:', e)
    } finally {
      loading.value = false
    }
  }

  const addComponent = async (type: 'hardware' | 'software', formData: any) => {
    try {
      const { error } = await useFetch(`/api/proxy/Inventory/${type}`, {
        method: 'POST',
        body: formData
      })
      if (!error.value) {
        await fetchData()
        return { success: true }
      }
      return { success: false, error: error.value }
    } catch (e) {
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
