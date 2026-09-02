import { ref, computed } from 'vue'

export interface SearchResult {
  id: string
  name: string
  displayName?: string
  technology?: string
  type: 'component' | 'machine' | 'clientpc'
  link: string
}

export const useSearch = () => {
  const query = ref('')
  const searchGroups = useState<GroupedKey[]>('searchGroups', () => [])
  const results = ref<SearchResult[]>([])
  const isLoading = ref(false)
  const isKeysLoading = ref(false)

  const fetchKeys = async () => {
    if (searchGroups.value.length > 0) return
    isKeysLoading.value = true
    try {
      const data = await $fetch<GroupedKey[]>('/api/proxy/inventory/keys')
      searchGroups.value = data
    } catch (e) {
      console.error('Failed to fetch search keys:', e)
    } finally {
      isKeysLoading.value = false
    }
  }

  let activeController: AbortController | null = null

  const performSearch = async (searchQuery: string, isFull: boolean = false) => {
    if (!searchQuery) {
      results.value = []
      return
    }

    // Cancel previous request if any
    if (activeController) {
      activeController.abort()
    }
    activeController = new AbortController()

    isLoading.value = true
    try {
      const limit = isFull ? 100 : 20
      const data = await $fetch<any[]>(`/api/proxy/inventory/search?query=${encodeURIComponent(searchQuery)}&limit=${limit}`, {
        signal: activeController.signal
      })
      results.value = data.map(item => ({
        id: item.id,
        name: item.name,
        displayName: item.displayName,
        technology: item.manufacturerName, // Using manufacturer as technology label
        type: item.itemType.toLowerCase() === 'machine' ? 'machine' : 'component',
        link: `/dashboard/inventory/${item.id}`
      }))
    } catch (e: any) {
      if (e.name === 'AbortError') return
      console.error('Search failed:', e)
    } finally {
      isLoading.value = false
      activeController = null
    }
  }

  // Recommendations based on current query and available keys
  const recommendations = computed(() => {
    const q = query.value || ''
    const parts = q.split(' ')
    const lastPart = parts[parts.length - 1] || ''
    
    if (lastPart.includes(':')) {
      return [] // Already have a key
    }

    const results: { group: string, keys: string[] }[] = []
    
    for (const group of searchGroups.value) {
      const matchedKeys = group.keys.filter(k => 
        k && k.toLowerCase().startsWith(lastPart.toLowerCase())
      ).slice(0, 5)

      if (matchedKeys.length > 0) {
        results.push({
          group: group.group,
          keys: matchedKeys
        })
      }
    }

    return results
  })

  return {
    query,
    searchGroups,
    searchKeys: computed(() => searchGroups.value.flatMap(g => g.keys)),
    results,
    isLoading,
    isKeysLoading,
    recommendations,
    fetchKeys,
    performSearch
  }
}
