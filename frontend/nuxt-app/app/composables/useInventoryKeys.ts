import { ref } from 'vue'

export interface GroupedKey {
  group: string
  keys: string[]
}

export const useInventoryKeys = () => {
  const groups = ref<GroupedKey[]>([])
  const loading = ref(false)

  const fetchKeys = async () => {
    loading.value = true
    try {
      const data = await $fetch<GroupedKey[]>('/api/proxy/inventory/keys')
      if (data) {
        groups.value = data
      }
    } catch (e) {
      console.error('Error fetching inventory keys:', e)
    } finally {
      loading.value = false
    }
  }

  return {
    groups,
    keys: computed(() => groups.value.flatMap(g => g.keys)),
    loading,
    fetchKeys
  }
}
