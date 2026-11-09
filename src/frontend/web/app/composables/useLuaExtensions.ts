import { ref, computed } from 'vue'
import type {
  LuaScriptRecord,
  LuaUiExtension,
  LuaUiTargetZone,
  LuaExecutionResult
} from '~/types/lua'

export function useLuaExtensions() {
  const extensions = ref<LuaUiExtension[]>([])
  const scripts = ref<LuaScriptRecord[]>([])
  const loading = ref(false)
  const executing = ref(false)
  const error = ref<string | null>(null)
  const lastExecutionResult = ref<LuaExecutionResult | null>(null)

  const fetchExtensions = async (zone?: LuaUiTargetZone) => {
    loading.value = true
    error.value = null
    try {
      const url = zone ? `/api/v1/lua/ui-extensions?zone=${zone}` : '/api/v1/lua/ui-extensions'
      const data = await $fetch<LuaUiExtension[]>(url)
      extensions.value = data || []
    } catch (err: any) {
      error.value = err?.message || 'Failed to fetch Lua UI extensions'
    } finally {
      loading.value = false
    }
  }

  const invokeAction = async (
    extensionId: string,
    context?: Record<string, any>
  ): Promise<LuaExecutionResult> => {
    executing.value = true
    try {
      const res = await $fetch<LuaExecutionResult>(
        `/api/v1/lua/ui-extensions/${extensionId}/invoke`,
        {
          method: 'POST',
          body: { context }
        }
      )
      lastExecutionResult.value = res
      return res
    } catch (err: any) {
      const failResult: LuaExecutionResult = {
        success: false,
        error: err?.message || 'Failed to invoke Lua UI action',
        output: ''
      }
      lastExecutionResult.value = failResult
      return failResult
    } finally {
      executing.value = false
    }
  }

  const fetchScripts = async () => {
    loading.value = true
    error.value = null
    try {
      const data = await $fetch<LuaScriptRecord[]>('/api/v1/lua/scripts')
      scripts.value = data || []
    } catch (err: any) {
      error.value = err?.message || 'Failed to fetch Lua scripts'
    } finally {
      loading.value = false
    }
  }

  const saveScript = async (script: Partial<LuaScriptRecord>) => {
    loading.value = true
    error.value = null
    try {
      if (script.id) {
        const updated = await $fetch<LuaScriptRecord>(`/api/v1/lua/scripts/${script.id}`, {
          method: 'PUT',
          body: script
        })
        const idx = scripts.value.findIndex(s => s.id === script.id)
        if (idx >= 0) scripts.value[idx] = updated
        return updated
      } else {
        const created = await $fetch<LuaScriptRecord>('/api/v1/lua/scripts', {
          method: 'POST',
          body: script
        })
        scripts.value.unshift(created)
        return created
      }
    } catch (err: any) {
      error.value = err?.message || 'Failed to save Lua script'
      throw err
    } finally {
      loading.value = false
    }
  }

  const deleteScript = async (id: string) => {
    loading.value = true
    try {
      await $fetch(`/api/v1/lua/scripts/${id}`, { method: 'DELETE' })
      scripts.value = scripts.value.filter(s => s.id !== id)
    } catch (err: any) {
      error.value = err?.message || 'Failed to delete Lua script'
      throw err
    } finally {
      loading.value = false
    }
  }

  const runScript = async (id: string, context?: Record<string, any>) => {
    executing.value = true
    try {
      const res = await $fetch<LuaExecutionResult>(`/api/v1/lua/scripts/${id}/run`, {
        method: 'POST',
        body: context || {}
      })
      lastExecutionResult.value = res
      return res
    } catch (err: any) {
      const failResult: LuaExecutionResult = {
        success: false,
        error: err?.message || 'Failed to run script',
        output: ''
      }
      lastExecutionResult.value = failResult
      return failResult
    } finally {
      executing.value = false
    }
  }

  const testExecute = async (code: string, context?: Record<string, any>) => {
    executing.value = true
    try {
      const res = await $fetch<LuaExecutionResult>('/api/v1/lua/execute', {
        method: 'POST',
        body: { code, context }
      })
      lastExecutionResult.value = res
      return res
    } catch (err: any) {
      const failResult: LuaExecutionResult = {
        success: false,
        error: err?.message || 'Failed to execute code in sandbox',
        output: ''
      }
      lastExecutionResult.value = failResult
      return failResult
    } finally {
      executing.value = false
    }
  }

  const machineExtensions = computed(() =>
    extensions.value.filter(
      e => e.targetZone === 'MachineDetails' || (e.targetZone as any) === 0
    )
  )

  const ticketExtensions = computed(() =>
    extensions.value.filter(
      e => e.targetZone === 'TicketDetails' || (e.targetZone as any) === 1
    )
  )

  return {
    extensions,
    scripts,
    loading,
    executing,
    error,
    lastExecutionResult,
    machineExtensions,
    ticketExtensions,
    fetchExtensions,
    invokeAction,
    fetchScripts,
    saveScript,
    deleteScript,
    runScript,
    testExecute
  }
}
