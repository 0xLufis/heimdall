import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useLuaExtensions } from '@/composables/useLuaExtensions'
import DynamicUiExtensions from '@/components/lua/DynamicUiExtensions.vue'
import type { LuaUiExtension, LuaScriptRecord } from '@/types/lua'

describe('useLuaExtensions composable', () => {
  const mockExtensions: LuaUiExtension[] = [
    {
      id: 'ext-action-1',
      title: 'Recalibrate Drive',
      targetZone: 'MachineDetails',
      extensionType: 'ActionButton',
      icon: 'i-lucide-wrench'
    },
    {
      id: 'ext-badge-1',
      title: 'SLA At Risk',
      targetZone: 'TicketDetails',
      extensionType: 'Badge',
      icon: 'i-lucide-alert-triangle',
      color: 'red'
    }
  ]

  const mockScripts: LuaScriptRecord[] = [
    {
      id: 'script-1',
      name: 'Spindle Monitor',
      description: 'Monitors spindle vibration',
      triggerType: 'TelemetryChange',
      code: 'print("Running")',
      isActive: true,
      createdAt: '2026-10-18T10:00:00Z',
      updatedAt: '2026-10-18T10:00:00Z'
    }
  ]

  beforeEach(() => {
    vi.clearAllMocks()
    ;(globalThis as any).$fetch = vi.fn().mockImplementation((url: string, opts?: any) => {
      if (url.includes('/api/v1/lua/ui-extensions/ext-action-1/invoke')) {
        return Promise.resolve({
          success: true,
          returnValue: 'Calibration Routine Dispatched',
          output: 'Invoking action'
        })
      }
      if (url.includes('/api/v1/lua/ui-extensions')) {
        return Promise.resolve(mockExtensions)
      }
      if (url.includes('/api/v1/lua/scripts/script-1/run')) {
        return Promise.resolve({
          success: true,
          returnValue: 'OK',
          output: 'Run finished'
        })
      }
      if (url.includes('/api/v1/lua/scripts/script-1') && opts?.method === 'PUT') {
        return Promise.resolve({ ...mockScripts[0], name: 'Updated Name' })
      }
      if (url.includes('/api/v1/lua/scripts') && opts?.method === 'POST') {
        return Promise.resolve({
          id: 'new-script-id',
          name: opts.body.name,
          code: opts.body.code,
          triggerType: opts.body.triggerType,
          isActive: true
        })
      }
      if (url.includes('/api/v1/lua/scripts')) {
        return Promise.resolve(mockScripts)
      }
      if (url.includes('/api/v1/lua/execute')) {
        return Promise.resolve({
          success: true,
          returnValue: 42,
          output: 'Test Output'
        })
      }
      return Promise.resolve([])
    })
  })

  it('fetches UI extensions and filters by zone', async () => {
    const { extensions, machineExtensions, ticketExtensions, fetchExtensions } = useLuaExtensions()
    await fetchExtensions()

    expect(extensions.value).toHaveLength(2)
    expect(machineExtensions.value).toHaveLength(1)
    expect(machineExtensions.value[0].id).toBe('ext-action-1')
    expect(ticketExtensions.value).toHaveLength(1)
    expect(ticketExtensions.value[0].id).toBe('ext-badge-1')
  })

  it('invokes dynamic UI action and stores result', async () => {
    const { invokeAction, lastExecutionResult } = useLuaExtensions()
    const result = await invokeAction('ext-action-1', { machine_id: 'm1' })

    expect(result.success).toBe(true)
    expect(result.returnValue).toBe('Calibration Routine Dispatched')
    expect(lastExecutionResult.value?.returnValue).toBe('Calibration Routine Dispatched')
  })

  it('fetches scripts and performs test execution', async () => {
    const { scripts, fetchScripts, testExecute } = useLuaExtensions()
    await fetchScripts()

    expect(scripts.value).toHaveLength(1)
    expect(scripts.value[0].name).toBe('Spindle Monitor')

    const res = await testExecute('return 21 * 2')
    expect(res.success).toBe(true)
    expect(res.returnValue).toBe(42)
  })

  it('saves new script and updates scripts list', async () => {
    const { scripts, saveScript } = useLuaExtensions()
    const created = await saveScript({
      name: 'New Custom Rule',
      code: 'print("hello")',
      triggerType: 'Manual'
    })

    expect(created.id).toBe('new-script-id')
    expect(scripts.value[0].name).toBe('New Custom Rule')
  })
})

describe('DynamicUiExtensions component', () => {
  beforeEach(() => {
    ;(globalThis as any).$fetch = vi.fn().mockImplementation((url: string) => {
      if (url.includes('/api/v1/lua/ui-extensions')) {
        return Promise.resolve([
          {
            id: 'btn-spindle-stop',
            title: 'Recalibrate Axis',
            targetZone: 'MachineDetails',
            extensionType: 'ActionButton',
            icon: 'i-lucide-wrench'
          },
          {
            id: 'badge-sla-warn',
            title: 'SLA Warning',
            targetZone: 'MachineDetails',
            extensionType: 'Badge',
            color: 'red'
          }
        ])
      }
      return Promise.resolve([])
    })
  })

  it('mounts and renders action button and badge for target zone', async () => {
    const wrapper = mount(DynamicUiExtensions, {
      props: {
        zone: 'MachineDetails',
        context: { machine_id: 'L01-CNC-01' }
      }
    })

    // Wait for fetch to complete
    await new Promise(resolve => setTimeout(resolve, 50))
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('Recalibrate Axis')
    expect(wrapper.text()).toContain('SLA Warning')
  })
})
