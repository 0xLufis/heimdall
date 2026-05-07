import { ref, watch, computed } from 'vue'

export const useInventory = () => {
  const activeTab = ref<'hardware' | 'software'>('hardware')
  const loading = ref(false)
  const items = ref<any[]>([])

  // --- Column Visibility State ---
  const columns = ref({
    manufacturer: true,
    modelNumber: false,
    purchaseDate: false,
    cost: true,
    specs: true,
    tags: true,
  })

  // --- Search State & Logic ---
  const searchQuery = ref('')

  const fetchData = async () => {
    loading.value = true
    try {
      let q = searchQuery.value
      // If we're on a specific tab and no type filter is specified, add it
      if (activeTab.value && !q.includes('type:')) {
        q = `${q} type:${activeTab.value}`.trim()
      }

      const url = `/api/proxy/inventory/search?query=${encodeURIComponent(q)}`
      
      const data = await $fetch<any[]>(url)
      if (data) {
        items.value = data
      }
    } catch (e) {
      console.error('Error fetching inventory:', e)
    } finally {
      loading.value = false
    }
  }

  // --- Component Management ---
  const addComponent = async (type: 'hardware' | 'software', formData: any) => {
    try {
      await $fetch('/api/proxy/inventory', {
        method: 'POST',
        body: formData,
      })
      await fetchData()
      return { success: true }
    } catch (e: any) {
      console.error('Error adding component:', e)
      return { success: false, error: e }
    }
  }

  // --- Watchers ---
  watch(activeTab, () => {
    // Reset search when switching tabs and fetch data
    searchQuery.value = ''
    fetchData()
  })

  return {
    activeTab,
    loading,
    items,
    columns,
    searchQuery,
    fetchData,
    addComponent,
  }
}
