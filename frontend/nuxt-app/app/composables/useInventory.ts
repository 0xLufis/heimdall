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
  const searchQuery = ref({
    logic: 'and',
    conditions: [],
  })

  const buildQueryString = (query: any): string => {
    // This is a simplified serializer. A real implementation would handle
    // nested groups, different operators, and value types more robustly.
    const params = new URLSearchParams()
    
    if (query.conditions && query.conditions.length > 0) {
      // For simplicity, we'll AND all top-level conditions.
      // We'll primarily use a single 'query' for full-text and specific fields for filters.
      const generalSearch = query.conditions.find(c => c.field === 'general_query');
      if (generalSearch?.value) {
        params.append('query', generalSearch.value);
      }
      
      const categorySearch = query.conditions.find(c => c.field === 'categories');
      if (categorySearch?.value) {
        params.append('category', categorySearch.value);
      }
    }
    return params.toString()
  }

  const fetchData = async () => {
    loading.value = true
    try {
      const queryString = buildQueryString(searchQuery.value)
      const url = `/api/proxy/Inventory/${activeTab.value}/search?${queryString}`
      
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

  // --- Component Management ---
  const addComponent = async (type: 'hardware' | 'software', formData: any) => {
    try {
      await $fetch(`/api/proxy/Inventory/${type}`, {
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
    searchQuery.value = { logic: 'and', conditions: [] };
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
