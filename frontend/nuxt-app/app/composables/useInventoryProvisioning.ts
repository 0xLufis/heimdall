import { ref } from 'vue'

export const useInventoryProvisioning = () => {
  const manufacturers = ref<any[]>([])
  const suppliers = ref<any[]>([])
  const machines = ref<any[]>([])
  const clientPcs = ref<any[]>([])
  const components = ref<any[]>([])
  const isLoading = ref(false)

  const fetchReferenceData = async () => {
    isLoading.value = true
    try {
      const [mRes, sRes, machRes, pcRes, compRes] = await Promise.all([
        $fetch<any[]>('/api/proxy/inventory/manufacturers'),
        $fetch<any[]>('/api/proxy/inventory/suppliers'),
        $fetch<any[]>('/api/proxy/inventory/machines'),
        $fetch<any[]>('/api/proxy/inventory/client-pcs'),
        $fetch<any[]>('/api/proxy/inventory')
      ])
      
      manufacturers.value = mRes
      suppliers.value = sRes
      machines.value = machRes
      clientPcs.value = pcRes
      
      // Flatten components for parent/lateral linking selection
      const flatten = (items: any[]): any[] => {
        return items.reduce((acc, item) => {
          acc.push(item)
          if (item.children && item.children.length > 0) {
            acc.push(...flatten(item.children))
          }
          return acc
        }, [])
      }
      components.value = flatten(compRes)
    } catch (e) {
      console.error('Failed to fetch provisioning reference data:', e)
    } finally {
      isLoading.value = false
    }
  }

  return {
    manufacturers,
    suppliers,
    machines,
    clientPcs,
    components,
    isLoading,
    fetchReferenceData
  }
}
