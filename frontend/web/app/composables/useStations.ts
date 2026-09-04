import { ref, onMounted, getCurrentInstance } from 'vue'
import type { ProductionStation } from '~/types/domain'

export const useStations = () => {
  const stations = useState<ProductionStation[]>('global_stations', () => [])
  const selectedStation = useState<ProductionStation | null>('global_selected_station', () => null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const fetchStations = async () => {
    isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<any[]>('/api/proxy/Machine')
      stations.value = (data || []).map((m: any) => ({
        id: m.id || m.Id,
        name: m.name || m.Name || '',
        displayName: m.displayName || m.DisplayName,
        customIdentifier: m.customIdentifier || m.CustomIdentifier || '',
        pinnedObjectHandle: m.pinnedObjectHandle || m.PinnedObjectHandle,
        organizationId: m.organizationId || m.OrganizationId,
        controllers: (m.controllers || []).map((c: any) => ({
          id: c.id,
          stationId: m.id,
          controllerId: c.id,
          hostname: c.hostname,
          name: c.name,
          pinnedObjectHandle: c.pinnedObjectHandle
        })),
        responsibleTeams: m.responsibleTeams || [],
        hardwareComponents: m.children || []
      }))
    } catch (e: any) {
      error.value = e?.message || 'Failed to fetch stations'
    } finally {
      isLoading.value = false
    }
  }

  const createStation = async (station: Partial<ProductionStation>) => {
    const payload = {
      name: station.name,
      customIdentifier: station.customIdentifier,
      pinnedObjectHandle: station.pinnedObjectHandle,
      organizationId: station.organizationId
    }
    const created = await $fetch<any>('/api/proxy/Machine', {
      method: 'POST',
      body: payload
    })
    await fetchStations()
    return created
  }

  const updateStationPin = async (stationId: string, handle: string, controllerIds: string[] = []) => {
    const station = stations.value.find(s => s.id === stationId)
    if (!station) return

    const payload = {
      id: station.id,
      name: station.name,
      customIdentifier: station.customIdentifier,
      pinnedObjectHandle: handle,
      organizationId: station.organizationId,
      controllerIds
    }

    await $fetch(`/api/proxy/Machine/${stationId}`, {
      method: 'PUT',
      body: payload
    })
    await fetchStations()
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      if (stations.value.length === 0) {
        fetchStations()
      }
    })
  }

  return {
    stations,
    selectedStation,
    isLoading,
    error,
    fetchStations,
    createStation,
    updateStationPin
  }
}
