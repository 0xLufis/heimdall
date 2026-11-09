import { ref } from 'vue'
import type {
  MachineDedications,
  UserDedications
} from '~/types/maintenance'

export function useDedications() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchMachineDedications = async (machineId: string): Promise<MachineDedications | null> => {
    loading.value = true
    error.value = null
    try {
      const data = await $fetch<MachineDedications>(`/api/proxy/v1/Delegation/machine/${machineId}/dedications`)
      return data
    } catch (err: any) {
      // Return structured default if offline/proxy not reachable
      return null
    } finally {
      loading.value = false
    }
  }

  const dedicateToMachine = async (
    machineId: string,
    targetType: 'person' | 'group',
    targetId: string,
    targetName: string,
    targetEmail?: string,
    technology?: string
  ): Promise<MachineDedications | null> => {
    loading.value = true
    error.value = null
    try {
      const data = await $fetch<MachineDedications>(`/api/proxy/v1/Delegation/machine/${machineId}/dedicate`, {
        method: 'POST',
        body: {
          targetType,
          targetId,
          targetName,
          targetEmail,
          technology
        }
      })
      return data
    } catch (err: any) {
      error.value = err?.message || 'Failed to dedicate to machine'
      return null
    } finally {
      loading.value = false
    }
  }

  const removeMachineDedication = async (
    machineId: string,
    options?: { targetId?: string; technology?: string }
  ): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      const query: Record<string, string> = {}
      if (options?.targetId) query.targetId = options.targetId
      if (options?.technology) query.technology = options.technology

      await $fetch(`/api/proxy/v1/Delegation/machine/${machineId}/dedicate`, {
        method: 'DELETE',
        query
      })
      return true
    } catch (err: any) {
      error.value = err?.message || 'Failed to remove dedication'
      return false
    } finally {
      loading.value = false
    }
  }

  const fetchUserDedications = async (userId: string): Promise<UserDedications | null> => {
    loading.value = true
    error.value = null
    try {
      const data = await $fetch<UserDedications>(`/api/proxy/v1/Delegation/user/${userId}/dedications`)
      return data
    } catch (err: any) {
      return null
    } finally {
      loading.value = false
    }
  }

  const dedicateUserToMachine = async (userId: string, machineId: string, technology?: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      const query: Record<string, string> = {}
      if (technology) query.technology = technology

      await $fetch(`/api/proxy/v1/Delegation/user/${userId}/dedicate-machine/${machineId}`, {
        method: 'POST',
        query
      })
      return true
    } catch (err: any) {
      error.value = err?.message || 'Failed to dedicate user to machine'
      return false
    } finally {
      loading.value = false
    }
  }

  const removeUserDedicationFromMachine = async (userId: string, machineId: string, technology?: string): Promise<boolean> => {
    loading.value = true
    error.value = null
    try {
      const query: Record<string, string> = {}
      if (technology) query.technology = technology

      await $fetch(`/api/proxy/v1/Delegation/user/${userId}/dedicate-machine/${machineId}`, {
        method: 'DELETE',
        query
      })
      return true
    } catch (err: any) {
      error.value = err?.message || 'Failed to remove user dedication from machine'
      return false
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    fetchMachineDedications,
    dedicateToMachine,
    removeMachineDedication,
    fetchUserDedications,
    dedicateUserToMachine,
    removeUserDedicationFromMachine
  }
}
