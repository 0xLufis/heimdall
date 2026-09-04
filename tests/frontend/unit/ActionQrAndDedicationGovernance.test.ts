import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { useActionQr } from '../../../frontend/web/app/composables/useActionQr'
import SearchableTargetCombobox from '../../../frontend/web/app/components/common/SearchableTargetCombobox.vue'
import {
  validateDedicationGovernance,
  createRule,
  SEEDED_CANDIDATES,
  findCandidateByNameOrId
} from '../../../frontend/web/server/utils/technicianRulesStore'

describe('Action QR Composable & Role Dedication Governance Suite', () => {

  describe('useActionQr Composable', () => {
    it('initializes with reactive defaults and dynamic options', () => {
      const qr = useActionQr({
        initialParams: {
          action: 'report-incident',
          stationId: 'STATION-OP10-01',
          machineType: 'Milling'
        }
      })

      expect(qr.action.value).toBe('report-incident')
      expect(qr.stationId.value).toBe('STATION-OP10-01')
      expect(qr.machineType.value).toBe('Milling')
      expect(qr.protocol.value).toBe('web')
      expect(qr.activeUri.value).toContain('action=report-incident')
      expect(qr.activeUri.value).toContain('stationId=STATION-OP10-01')
      expect(qr.activeUri.value).toContain('machineType=Milling')
    })

    it('reactively switches between Web URL and Heimdall Native Deep Link', () => {
      const qr = useActionQr({
        initialParams: { action: 'inspect-machine', stationId: 'ROBOT-01' }
      })

      // Default is web
      expect(qr.protocol.value).toBe('web')
      expect(qr.activeUri.value).toContain('/mobile/action?action=inspect-machine')

      // Switch to heimdall deep link protocol
      qr.protocol.value = 'heimdall'
      expect(qr.activeUri.value).toMatch(/^heimdall:\/\/action\?action=inspect-machine/)
      expect(qr.activeUri.value).toContain('stationId=ROBOT-01')

      // Switch back
      qr.protocol.value = 'web'
      expect(qr.activeUri.value).not.toMatch(/^heimdall:\/\//)
    })

    it('dynamically adapts when setMachine, setTicket, or setGroup is invoked', () => {
      const qr = useActionQr()

      // setMachine
      qr.setMachine({
        customIdentifier: 'STATION-L06-150',
        displayName: 'Battery Cell Fastening',
        machineType: 'Screwing Station',
        groupId: 'grp-line06'
      })
      expect(qr.stationId.value).toBe('STATION-L06-150')
      expect(qr.stationName.value).toBe('Battery Cell Fastening')
      expect(qr.machineType.value).toBe('Screwing Station')
      expect(qr.groupId.value).toBe('grp-line06')
      expect(qr.activeUri.value).toContain('stationId=STATION-L06-150')

      // setTicket
      qr.setTicket({
        id: 't-1234',
        title: 'Axis Divergence Error',
        priority: 'High'
      })
      expect(qr.action.value).toBe('view-ticket')
      expect(qr.ticketId.value).toBe('t-1234')
      expect(qr.prefillTitle.value).toBe('Axis Divergence Error')
      expect(qr.prefillPriority.value).toBe('High')
      expect(qr.activeUri.value).toContain('ticketId=t-1234')

      // setGroup
      qr.setGroup({
        id: 'grp-line09',
        machineTypes: ['Automatic Optical Inspection', 'Pressing']
      })
      expect(qr.groupId.value).toBe('grp-line09')
      expect(qr.machineType.value).toBe('Automatic Optical Inspection')
    })

    it('generates a valid QR code data URL', async () => {
      const qr = useActionQr({
        initialParams: { action: 'check-in', stationId: 'STN-99' }
      })

      // Wait a tick for watch trigger
      await qr.refreshQr()
      expect(qr.qrDataUrl.value).toBeTruthy()
      expect(qr.qrDataUrl.value).toMatch(/^data:image\/(png|svg\+xml)/)
    })

    it('parses inbound QR action deep links back into reactive state', () => {
      const qr = useActionQr()
      const rawUri = 'heimdall://action?action=verify-pm&stationId=STATION-OP10-01&machineType=Milling&groupId=grp-line06'

      const parsed = qr.parseFromCode(rawUri)
      expect(parsed).not.toBeNull()
      expect(qr.action.value).toBe('verify-pm')
      expect(qr.stationId.value).toBe('STATION-OP10-01')
      expect(qr.machineType.value).toBe('Milling')
      expect(qr.groupId.value).toBe('grp-line06')
    })
  })

  describe('Multi-Tier Role Dedication Governance', () => {
    it('Tier 1: Engineers & Technicians can ONLY dedicate themselves', () => {
      // Self assignment is permitted
      const selfCheck = validateDedicationGovernance({
        callerRole: 'engineer',
        callerUserName: 'Engineer Sally',
        callerUserId: 'usr-sally',
        technicianName: 'Engineer Sally',
        scopeType: 'technology'
      })
      expect(selfCheck.authorized).toBe(true)

      // Attempting to dedicate someone else is rejected
      const foreignCheck = validateDedicationGovernance({
        callerRole: 'engineer',
        callerUserName: 'Engineer Sally',
        callerUserId: 'usr-sally',
        technicianName: 'István Kovács',
        scopeType: 'machine'
      })
      expect(foreignCheck.authorized).toBe(false)
      expect(foreignCheck.error).toContain('Engineers and technicians can only dedicate themselves')

      // Technician dedicating another person also rejected
      const techCheck = validateDedicationGovernance({
        callerRole: 'technician',
        callerUserName: 'István Kovács',
        callerUserId: 'usr-kovacs',
        technicianName: 'Gábor Varga',
        scopeType: 'machine'
      })
      expect(techCheck.authorized).toBe(false)
      expect(techCheck.error).toContain('Engineers and technicians can only dedicate themselves')
    })

    it('Tier 2: Shift Leaders can dedicate technicians from their shift, but NOT engineers or technology scope', () => {
      // Shift leader dedicating technician to machine is authorized
      const shiftValid = validateDedicationGovernance({
        callerRole: 'shift_leader',
        callerUserName: 'Shift Leader Ferenc',
        technicianName: 'István Kovács',
        scopeType: 'machine'
      })
      expect(shiftValid.authorized).toBe(true)

      // Shift leader attempting to assign an Engineer is rejected
      const assignEng = validateDedicationGovernance({
        callerRole: 'shift_leader',
        callerUserName: 'Shift Leader Ferenc',
        technicianName: 'Engineer Sally',
        scopeType: 'machine'
      })
      expect(assignEng.authorized).toBe(false)
      expect(assignEng.error).toContain('Shift leaders can only dedicate shift technicians')

      // Shift leader attempting to assign technology scope is rejected
      const techScope = validateDedicationGovernance({
        callerRole: 'shift_leader',
        callerUserName: 'Shift Leader Ferenc',
        technicianName: 'István Kovács',
        scopeType: 'technology'
      })
      expect(techScope.authorized).toBe(false)
      expect(techScope.error).toContain('Shift leaders can only dedicate technicians to stations and lines, not technology-wide')
    })

    it('Tier 3: Group Leaders can dedicate both engineers & technicians to stations, lines AND technologies', () => {
      // Group leader dedicating engineer to technology domain is authorized
      const glTech = validateDedicationGovernance({
        callerRole: 'group_leader',
        callerUserName: 'Engineer Orwell',
        technicianName: 'Engineer Sally',
        scopeType: 'technology'
      })
      expect(glTech.authorized).toBe(true)

      // Group leader dedicating technician to line group is authorized
      const glLine = validateDedicationGovernance({
        callerRole: 'group_leader',
        callerUserName: 'Engineer Orwell',
        technicianName: 'István Kovács',
        scopeType: 'group'
      })
      expect(glLine.authorized).toBe(true)

      // Group leader dedicating technician to station machine is authorized
      const glMachine = validateDedicationGovernance({
        callerRole: 'group_leader',
        callerUserName: 'Engineer Orwell',
        technicianName: 'Gábor Varga',
        scopeType: 'machine'
      })
      expect(glMachine.authorized).toBe(true)

      // Group leader attempting to assign plant management is rejected
      const glManager = validateDedicationGovernance({
        callerRole: 'group_leader',
        callerUserName: 'Engineer Orwell',
        technicianName: 'András Molnár (Plant Manager)',
        scopeType: 'technology'
      })
      expect(glManager.authorized).toBe(false)
      expect(glManager.error).toContain('Group leaders cannot assign plant management')
    })

    it('Tier 4: Engineering Managers have full cross-cutting authority', () => {
      // Manager can dedicate any person to any technology or group
      const mgrAll = validateDedicationGovernance({
        callerRole: 'manager',
        callerUserName: 'András Molnár (Plant Manager)',
        technicianName: 'Engineer Orwell',
        scopeType: 'technology'
      })
      expect(mgrAll.authorized).toBe(true)

      const mgrGroup = validateDedicationGovernance({
        callerRole: 'manager',
        callerUserName: 'András Molnár (Plant Manager)',
        technicianName: 'István Kovács',
        scopeType: 'group'
      })
      expect(mgrGroup.authorized).toBe(true)
    })

    it('createRule enforces role governance and throws 403 on violations', () => {
      // Successful self-assignment by engineer
      const rule = createRule({
        technicianName: 'Engineer Sally',
        targetId: 'Milling',
        scopeType: 'technology',
        callerRole: 'engineer',
        callerUserName: 'Engineer Sally'
      })
      expect(rule.id).toBeDefined()
      expect(rule.technicianName).toBe('Engineer Sally')
      expect(rule.scopeType).toBe('technology')

      // Violation: engineer dedicating someone else throws error
      expect(() => {
        createRule({
          technicianName: 'István Kovács',
          targetId: 'STATION-01',
          scopeType: 'machine',
          callerRole: 'engineer',
          callerUserName: 'Engineer Sally'
        })
      }).toThrow('Engineers and technicians can only dedicate themselves')
    })
  })

  describe('SearchableTargetCombobox Component', () => {
    it('renders input with free-text editing capability', async () => {
      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: 'Custom CNC Station',
          placeholder: 'Search station...'
        }
      })

      const input = wrapper.find('input')
      expect(input.exists()).toBe(true)
      expect(input.element.value).toBe('Custom CNC Station')

      // User types free-text
      await input.setValue('Custom Prototype Line 99')
      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['Custom Prototype Line 99'])
    })

    it('filters provided options and emits resolved object when selected', async () => {
      const options = [
        { id: 'm-01', label: 'Milling Station A', sublabel: 'Machining', category: 'Technology' },
        { id: 'm-02', label: 'Laser Solder Station', sublabel: 'Thermal', category: 'Technology' },
        { id: 'm-03', label: 'Vision Inspection Cell', sublabel: 'AOI', category: 'Vision' }
      ]

      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: 'Laser',
          options
        }
      })

      // Open dropdown
      const input = wrapper.find('input')
      await input.trigger('focus')

      // Dropdown should be open
      const listItems = wrapper.findAll('li')
      expect(listItems.length).toBe(1)
      expect(listItems[0].text()).toContain('Laser Solder Station')

      // Click option to select resolved object
      await listItems[0].trigger('click')
      expect(wrapper.emitted('select')?.[0]?.[0]).toMatchObject({
        id: 'm-02',
        label: 'Laser Solder Station'
      })
      expect(wrapper.emitted('update:modelValue')?.[1] || wrapper.emitted('update:modelValue')?.[0]).toEqual(['Laser Solder Station'])
    })

    it('provides an explicit custom free-text selection entry for undefined types', async () => {
      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: 'Undefined Experimental Station X',
          options: [{ id: 'm-01', label: 'Existing Station' }]
        }
      })

      const input = wrapper.find('input')
      await input.trigger('focus')

      // Look for the custom free-text selection element
      const customOption = wrapper.find('div.border-t.border-slate-800')
      expect(customOption.exists()).toBe(true)
      expect(customOption.text()).toContain('Use custom: "Undefined Experimental Station X"')

      // Click custom selection
      await customOption.trigger('click')
      expect(wrapper.emitted('select')?.[0]?.[0]).toMatchObject({
        id: 'Undefined Experimental Station X',
        label: 'Undefined Experimental Station X',
        isCustom: true
      })
    })

    it('enforces disabled state with policy reason for self-dedication lock', () => {
      const wrapper = mount(SearchableTargetCombobox, {
        props: {
          modelValue: 'Engineer Sally',
          disabled: true,
          disabledReason: 'Engineers & Technicians can only dedicate themselves.'
        }
      })

      const input = wrapper.find('input')
      expect(input.attributes('disabled')).toBeDefined()
      expect(wrapper.text()).toContain('Engineers & Technicians can only dedicate themselves.')
      expect(wrapper.find('button[title="Clear"]').exists()).toBe(false)
    })
  })
})
