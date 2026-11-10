import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useDedications } from '../../../src/frontend/web/app/composables/useDedications'
import StationDetailWorkbench from '../../../src/frontend/web/app/components/machines/StationDetailWorkbench.vue'

vi.stubGlobal('useAuthSession', () => ({
  user: { value: { id: 'usr-1', name: 'Plant Admin', email: 'admin@heimdall.dev' } },
  userRole: { value: 'system_admin' },
  dedicationTier: { value: 'manager' },
  simulatedPersona: { value: null },
  setSimulatedPersona: vi.fn(),
  clearSimulatedPersona: vi.fn()
}))

const mockMachineDedications = {
  machineId: 'm-laser-01',
  machineName: 'Trumpf 3D Laser Cell',
  machineGroupId: 'line-pre-assembly',
  machineGroupName: 'Line 01 Pre-Assembly',
  machineGroupType: 'Line',
  dedicatedPerson: {
    userId: 'usr-alex',
    userName: 'Alex Specialist',
    email: 'alex.specialist@heimdall.io',
    source: 'rule',
    isGroupMemberOverride: true,
    overriddenGroupName: 'Precision Laser Maintenance Crew'
  },
  dedicatedGroup: null,
  inheritedDedications: [
    {
      machineGroupId: 'line-pre-assembly',
      machineGroupName: 'Line 01 Pre-Assembly',
      groupType: 'Line',
      dedicatedPerson: {
        userId: 'usr-lead',
        userName: 'Lead Engineer Frank',
        source: 'group_lead'
      }
    }
  ],
  technologicalGroupDedications: [
    {
      groupId: 'grp-laser',
      groupName: 'Precision Laser Maintenance Crew',
      technology: 'Laser',
      isAutomaticallyDedicated: true,
      isSuperseded: true,
      supersededByUserId: 'usr-alex',
      supersededByUserName: 'Alex Specialist'
    }
  ]
}

const mockUserDedications = {
  userId: 'usr-alex',
  userName: 'Alex Specialist',
  directlyDedicatedMachines: [
    {
      machineId: 'm-laser-01',
      machineName: 'Trumpf 3D Laser Cell',
      customIdentifier: 'TRU-LSR-01',
      machineType: 'Laser Welding & Cutting',
      groupName: 'Line 01 Pre-Assembly',
      supersedesTechnologicalGroup: true,
      supersededGroupName: 'Precision Laser Maintenance Crew'
    }
  ],
  dedicatedMachineGroups: [],
  technologicalCoverage: [
    {
      groupId: 'grp-laser',
      groupName: 'Precision Laser Maintenance Crew',
      technology: 'Laser',
      coveredMachines: [
        {
          machineId: 'm-laser-01',
          machineName: 'Trumpf 3D Laser Cell',
          customIdentifier: 'TRU-LSR-01',
          supersedesTechnologicalGroup: true,
          supersededGroupName: 'Precision Laser Maintenance Crew'
        }
      ]
    }
  ]
}

describe('Machine and User Dedications UI & Composable', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn((url: string, opts?: any) => {
      if (url.includes('/api/proxy/v1/Delegation/machine/m-laser-01/dedications')) {
        return Promise.resolve(mockMachineDedications)
      }
      if (url.includes('/api/proxy/v1/Delegation/user/usr-alex/dedications')) {
        return Promise.resolve(mockUserDedications)
      }
      if (url.includes('/api/proxy/v1/Delegation/machine/m-laser-01/dedicate')) {
        if (opts?.method === 'DELETE') return Promise.resolve(true)
        return Promise.resolve(mockMachineDedications)
      }
      if (url.includes('/api/proxy/v1/Delegation/user/usr-alex/dedicate-machine')) {
        return Promise.resolve(true)
      }
      if (url === '/api/users') {
        return Promise.resolve({
          success: true,
          users: [
            { id: 'usr-alex', name: 'Alex Specialist', email: 'alex.specialist@heimdall.io', role: 'Specialist' },
            { id: 'usr-sam', name: 'Sam Technician', email: 'sam@heimdall.io', role: 'Technician' }
          ]
        })
      }
      if (url === '/api/personnel/groups') {
        return Promise.resolve([
          { id: 'grp-laser', name: 'Precision Laser Maintenance Crew', discipline: 'Laser' },
          { id: 'grp-controls', name: 'Automation Controls Crew', discipline: 'Controls' }
        ])
      }
      if (url === '/api/machines') {
        return Promise.resolve([
          { id: 'm-laser-01', name: 'Trumpf 3D Laser Cell', customIdentifier: 'TRU-LSR-01' }
        ])
      }
      return Promise.resolve([])
    }))
  })

  it('useDedications composable correctly fetches and manages machine and user dedications', async () => {
    const {
      fetchMachineDedications,
      dedicateToMachine,
      removeMachineDedication,
      fetchUserDedications,
      dedicateUserToMachine,
      removeUserDedicationFromMachine
    } = useDedications()

    // 1. Fetch Machine Dedications
    const machineDeds = await fetchMachineDedications('m-laser-01')
    expect(machineDeds).not.toBeNull()
    expect(machineDeds?.dedicatedPerson?.userName).toBe('Alex Specialist')
    expect(machineDeds?.technologicalGroupDedications[0].isSuperseded).toBe(true)
    expect(machineDeds?.technologicalGroupDedications[0].supersededByUserName).toBe('Alex Specialist')

    // 2. Dedicate Person to Machine
    const dedicated = await dedicateToMachine('m-laser-01', 'person', 'usr-alex', 'Alex Specialist', 'alex@heimdall.io')
    expect(dedicated).not.toBeNull()

    // 3. Remove Machine Dedication
    const removed = await removeMachineDedication('m-laser-01')
    expect(removed).toBe(true)

    // 4. Fetch User Dedications
    const userDeds = await fetchUserDedications('usr-alex')
    expect(userDeds).not.toBeNull()
    expect(userDeds?.directlyDedicatedMachines[0].supersedesTechnologicalGroup).toBe(true)
    expect(userDeds?.directlyDedicatedMachines[0].supersededGroupName).toBe('Precision Laser Maintenance Crew')

    // 5. Dedicate User to Machine (Vice-Versa)
    const userDedicated = await dedicateUserToMachine('usr-alex', 'm-laser-01')
    expect(userDedicated).toBe(true)

    // 6. Remove User Dedication from Machine
    const userRemoved = await removeUserDedicationFromMachine('usr-alex', 'm-laser-01')
    expect(userRemoved).toBe(true)
  })

  it('renders Dedications tab in StationDetailWorkbench with dedicated specialist and superseded badge', async () => {
    const wrapper = mount(StationDetailWorkbench, {
      props: {
        open: true,
        machine: {
          id: 'm-laser-01',
          name: 'Trumpf 3D Laser Cell',
          customIdentifier: 'TRU-LSR-01',
          machineType: 'Laser Welding',
          groupId: 'Line 01 Pre-Assembly',
          status: 'Active'
        },
        initialTab: 'dedications'
      }
    })

    // Allow async loadDedications to resolve
    await new Promise(r => setTimeout(r, 50))
    await wrapper.vm.$nextTick()

    // Assert tab button exists and is active
    const text = wrapper.text()
    expect(text).toContain('Dedications')
    expect(text).toContain('Current Machine Dedication')
    expect(text).toContain('Alex Specialist')
    expect(text).toContain('Direct Specialist')
    expect(text).toContain('Group Member Override')
    expect(text).toContain('Technological Groups (Auto Coverage)')
    expect(text).toContain('⚡ Superseded')
    expect(text).toContain('Inherited Dedications (Hierarchy)')
    expect(text).toContain('Lead Engineer Frank')
  })
})
