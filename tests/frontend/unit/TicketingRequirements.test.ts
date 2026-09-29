import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TicketPersonalHeader from '~/components/tickets/TicketPersonalHeader.vue'
import TicketList from '~/components/tickets/TicketList.vue'
import TicketDetailDrawer from '~/components/tickets/TicketDetailDrawer.vue'
import TicketMetricsOverview from '~/components/tickets/TicketMetricsOverview.vue'
import type { MaintenanceTicket } from '~/types/maintenance'

describe('Ticketing Flow & UI Requirements Specification Suite', () => {
  const sampleTickets: MaintenanceTicket[] = [
    {
      id: 'tkt-req-1',
      ticketNumber: 'TKT-2026-0001',
      stationId: 'CNC-01',
      stationName: 'Milling Cell 01',
      controllerId: 'ctrl-cnc-01',
      controllerName: 'IPC-CNC-HOST-01',
      groupId: 'Line-Alpha',
      title: 'Spindle Bearing Overheating',
      description: 'Bearing temperature exceeded 85C',
      status: 'Open',
      priority: 'Critical',
      isLineStop: true,
      lineStopDurationMinutes: 45,
      responsibleDepartment: 'Robotics',
      issueType: 'Maintenance',
      originatorType: 'MachineAutomatic',
      externalOperatorId: 'OP-441',
      externalOperatorName: 'John Operator',
      assignedTechnicianName: 'Gábor Varga',
      createdAt: '2026-09-29T10:00:00Z',
      updatedAt: '2026-09-29T10:00:00Z',
      comments: [],
      attachments: [],
      telemetrySnapshot: {
        spindleTempC: 87.5,
        vibrationRms: 4.2,
        robotCollisionDetected: false
      }
    },
    {
      id: 'tkt-req-2',
      ticketNumber: 'TKT-2026-0002',
      stationId: 'ROBOT-02',
      stationName: 'Welding Robot Cell',
      controllerId: 'ctrl-rob-02',
      controllerName: 'IPC-ROB-HOST-02',
      groupId: 'Line-Alpha',
      title: 'Robot Collision Joint 2 Over-Torque',
      description: 'Joint 2 torque limit tripped during cycle',
      status: 'InProgress',
      priority: 'High',
      isLineStop: true,
      lineStopDurationMinutes: 120,
      responsibleDepartment: 'Robotics',
      issueType: 'Transient',
      originatorType: 'MachineAutomatic',
      isEscalated: true,
      escalationReason: 'Automatic trigger: Robot collision detected',
      escalationTarget: 'DedicatedEngineer',
      escalationHandoverState: 'HandOff',
      assignedTechnicianName: 'Zoltán Németh',
      startedAt: '2026-09-29T10:15:00Z',
      reactionTimeMinutes: 5,
      createdAt: '2026-09-29T10:10:00Z',
      updatedAt: '2026-09-29T10:15:00Z',
      comments: [],
      attachments: [],
      changeHistory: [
        {
          id: 'chg-1',
          timestamp: '2026-09-29T10:20:00Z',
          changedBy: 'Zoltán Németh',
          field: 'description',
          oldValue: 'Initial error',
          newValue: 'Joint 2 torque limit tripped during cycle',
          note: 'Fixed typo and added torque limit context'
        }
      ]
    },
    {
      id: 'tkt-req-3',
      ticketNumber: 'TKT-2026-0003',
      stationId: 'SMT-03',
      stationName: 'SMT Feeder Station',
      controllerId: 'ctrl-smt-03',
      controllerName: 'IPC-SMT-HOST-03',
      groupId: 'Line-Beta',
      title: 'Feeder Jam on Reel 12',
      description: 'Feeder pitch mismatch',
      status: 'Pending',
      priority: 'Medium',
      pendingReason: 'SignOff',
      pendingDetails: 'Awaiting shift supervisor QA signoff',
      isLineStop: false,
      responsibleDepartment: 'SMT',
      issueType: 'Improvement',
      originatorType: 'ManualUser',
      assignedTechnicianName: 'Gábor Varga',
      createdAt: '2026-09-29T08:00:00Z',
      updatedAt: '2026-09-29T08:30:00Z',
      comments: [],
      attachments: []
    },
    {
      id: 'tkt-req-4',
      ticketNumber: 'TKT-2026-0004',
      stationId: 'CONV-04',
      stationName: 'Main Conveyor Transfer',
      title: 'Conveyor Sensor Dirt Cleaning',
      description: 'Optical sensor obscured by dust',
      status: 'Resolved',
      priority: 'Low',
      isLineStop: false,
      responsibleDepartment: 'Assy',
      issueType: 'Maintenance',
      originatorType: 'ScheduledMaintenance',
      assignedTechnicianName: 'Anna Szabó',
      createdAt: '2026-09-29T07:00:00Z',
      updatedAt: '2026-09-29T09:00:00Z',
      comments: [],
      attachments: []
    }
  ]

  describe('TicketPersonalHeader Component', () => {
    it('completely hides if no tickets are dedicated to user', () => {
      const wrapper = mount(TicketPersonalHeader, {
        props: {
          tickets: sampleTickets,
          currentUserName: 'Unassigned Technician'
        }
      })

      expect(wrapper.find('[data-testid="ticket-personal-header"]').exists()).toBe(false)
      expect(wrapper.html()).toBe('<!--v-if-->')
    })

    it('renders dedicated workspace when user has assigned or reserved tickets', () => {
      const wrapper = mount(TicketPersonalHeader, {
        props: {
          tickets: sampleTickets,
          currentUserName: 'Gábor Varga'
        }
      })

      expect(wrapper.find('[data-testid="ticket-personal-header"]').exists()).toBe(true)
      expect(wrapper.text()).toContain('My Dedicated Incidents')
      expect(wrapper.text()).toContain('2 Assigned')
      expect(wrapper.text()).toContain('TKT-2026-0001')
      expect(wrapper.text()).toContain('TKT-2026-0003')
      // Shows line stop badge
      expect(wrapper.text()).toContain('1 Line-Stop Incident')
    })

    it('emits selectTicket event when a dedicated ticket card is clicked', async () => {
      const wrapper = mount(TicketPersonalHeader, {
        props: {
          tickets: sampleTickets,
          currentUserName: 'Gábor Varga'
        }
      })

      const cards = wrapper.findAll('[role="button"]')
      expect(cards.length).toBeGreaterThan(0)
      await cards[0].trigger('click')

      expect(wrapper.emitted('selectTicket')).toBeTruthy()
      expect(wrapper.emitted('selectTicket')![0][0]).toEqual(sampleTickets[0])
    })
  })

  describe('TicketList Component Configurable Columns & Lifecycle Sorting', () => {
    it('renders all must-have columns by default in TicketList', () => {
      const wrapper = mount(TicketList, {
        props: { tickets: sampleTickets },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' },
            Icon: { template: '<span></span>' }
          }
        }
      })

      const text = wrapper.text()
      // Must-have columns specified in ticketing.TODO.md:
      // Ticket ID, Machines Name + Machine user identifier, Heimdall-agent host computer name,
      // Prod cell/line name, Raised at, started at, originator, currently working on it, escalation badge.
      expect(text).toContain('Ticket ID')
      expect(text).toContain('Machine & Operator')
      expect(text).toContain('Agent Host (IPC)')
      expect(text).toContain('Prod Cell / Line')
      expect(text).toContain('Line Stop')
      expect(text).toContain('Raised At')
      expect(text).toContain('Started At')
      expect(text).toContain('Originator')
      expect(text).toContain('Assigned / Working')
      expect(text).toContain('Escalation')
    })

    it('sorts tickets by lifecycle status first (Open -> InProgress -> Pending -> Resolved -> Closed), then secondarily by time opened', () => {
      const wrapper = mount(TicketList, {
        props: { tickets: sampleTickets },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' },
            Icon: { template: '<span></span>' }
          }
        }
      })

      const rows = wrapper.findAll('tbody tr')
      expect(rows.length).toBe(4)

      // Row 0: Open (tkt-req-1)
      expect(rows[0].text()).toContain('TKT-2026-0001')
      // Row 1: InProgress (tkt-req-2)
      expect(rows[1].text()).toContain('TKT-2026-0002')
      // Row 2: Pending (tkt-req-3)
      expect(rows[2].text()).toContain('TKT-2026-0003')
      // Row 3: Resolved (tkt-req-4)
      expect(rows[3].text()).toContain('TKT-2026-0004')
    })

    it('allows technicians to reserve open tickets', async () => {
      const wrapper = mount(TicketList, {
        props: { tickets: sampleTickets },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' },
            Icon: { template: '<span></span>' }
          }
        }
      })

      const reserveBtns = wrapper.findAll('button').filter(b => b.text().includes('Reserve'))
      expect(reserveBtns.length).toBeGreaterThan(0)

      await reserveBtns[0].trigger('click')
      expect(wrapper.emitted('reserveTicket')).toBeTruthy()
      expect(wrapper.emitted('reserveTicket')![0]).toEqual(['tkt-req-1'])
    })

    it('displays line stop indicator and stoppage duration badges', () => {
      const wrapper = mount(TicketList, {
        props: { tickets: sampleTickets },
        global: {
          stubs: {
            NuxtLink: { template: '<a><slot /></a>' },
            Icon: { template: '<span></span>' }
          }
        }
      })

      expect(wrapper.text()).toContain('STOP (45m)')
      expect(wrapper.text()).toContain('STOP (120m)')
    })
  })

  describe('TicketDetailDrawer QR Pickup, Handover States & Change History', () => {
    it('renders scannable QR code and direct QR pickup button', () => {
      const wrapper = mount(TicketDetailDrawer, {
        props: { ticket: sampleTickets[0], open: true }
      })

      expect(wrapper.text()).toContain('Direct Floor QR Pickup')
      expect(wrapper.text()).toContain('Scan & Pickup Ticket')
      const img = wrapper.find('img[alt="Ticket QR Code"]')
      expect(img.exists()).toBe(true)
      expect(img.attributes('src')).toContain('data:image/svg+xml')
    })

    it('renders escalation handover states and target options in escalation form', async () => {
      const wrapper = mount(TicketDetailDrawer, {
        props: { ticket: sampleTickets[0], open: true }
      })

      // Click Escalate to open escalation config form
      const escalateBtn = wrapper.findAll('button').find(b => b.text().includes('Escalate'))
      expect(escalateBtn).toBeDefined()
      await escalateBtn!.trigger('click')

      expect(wrapper.text()).toContain('Configure Incident Escalation & Handover')
      expect(wrapper.text()).toContain('Escalation Target')
      expect(wrapper.text()).toContain('Handover State')

      const selects = wrapper.findAll('select')
      const handoverSelect = selects.find(s => s.html().includes('Notification'))
      expect(handoverSelect).toBeDefined()
      expect(handoverSelect?.html()).toContain('HandOff')
      expect(handoverSelect?.html()).toContain('ParallelWork')
    })

    it('displays active escalation banner with handover state when escalated', () => {
      const wrapper = mount(TicketDetailDrawer, {
        props: { ticket: sampleTickets[1], open: true }
      })

      expect(wrapper.text()).toContain('Active Incident Escalation')
      expect(wrapper.text()).toContain('State: HandOff')
      expect(wrapper.text()).toContain('DedicatedEngineer')
    })

    it('renders telemetry snapshot captured at ticket raise', () => {
      const wrapper = mount(TicketDetailDrawer, {
        props: { ticket: sampleTickets[0], open: true }
      })

      expect(wrapper.text()).toContain('Telemetry Snapshot at Incident Raise')
      expect(wrapper.text()).toContain('87.5')
      expect(wrapper.text()).toContain('4.2')
    })

    it('displays audit trail and change history with old and new values', () => {
      const wrapper = mount(TicketDetailDrawer, {
        props: { ticket: sampleTickets[1], open: true }
      })

      expect(wrapper.text()).toContain('Audit Trail & Change History')
      expect(wrapper.text()).toContain('Zoltán Németh')
      expect(wrapper.text()).toContain('Fixed typo and added torque limit context')
    })
  })

  describe('TicketMetricsOverview Monitor View & Stoppage Stats', () => {
    it('renders plant floor stoppage monitor with weekly stats, worst machines, and department attribution', () => {
      const mockStoppageStats = {
        totalStoppageMinutesThisWeek: 360,
        totalLineStopIncidents: 4,
        departmentBreakdown: [
          { department: 'Robotics', stoppageMinutes: 200, incidentCount: 2 },
          { department: 'Assy', stoppageMinutes: 160, incidentCount: 2 }
        ],
        topWorstMachines: [
          { machineName: 'Robotic Welding Cell', totalStoppageMinutes: 200, incidentCount: 2 },
          { machineName: 'Milling Cell 01', totalStoppageMinutes: 160, incidentCount: 2 }
        ]
      }

      const wrapper = mount(TicketMetricsOverview, {
        props: {
          metrics: {
            totalTickets: 4,
            openCount: 1,
            inProgressCount: 1,
            pendingCount: 1,
            resolvedCount: 1,
            closedCount: 0,
            escalatedCount: 1,
            criticalCount: 1,
            overdueCount: 0,
            slaCompliancePercent: 100
          },
          stoppageStats: mockStoppageStats,
          allowRemoteStart: true
        }
      })

      expect(wrapper.find('[data-testid="stoppage-monitor-overview"]').exists()).toBe(true)
      expect(wrapper.text()).toContain('Weekly Line Stoppage Time')
      expect(wrapper.text()).toContain('6h 0m')
      expect(wrapper.text()).toContain('4 incidents')
      expect(wrapper.text()).toContain('Robotic Welding Cell')
      expect(wrapper.text()).toContain('Robotics:')
      expect(wrapper.text()).toContain('Remote Start Allowed')
    })

    it('toggles start mode between remote start allowed and QR only start', async () => {
      const wrapper = mount(TicketMetricsOverview, {
        props: {
          metrics: {
            totalTickets: 2,
            openCount: 1,
            inProgressCount: 0,
            pendingCount: 0,
            resolvedCount: 1,
            closedCount: 0,
            escalatedCount: 0,
            criticalCount: 0,
            overdueCount: 0,
            slaCompliancePercent: 100
          },
          allowRemoteStart: true
        }
      })

      const toggleBtn = wrapper.findAll('button').find(b => b.text().includes('Remote Start Allowed'))
      expect(toggleBtn).toBeDefined()

      await toggleBtn!.trigger('click')
      expect(wrapper.emitted('update:allowRemoteStart')).toBeTruthy()
      expect(wrapper.emitted('update:allowRemoteStart')![0]).toEqual([false])
      expect(wrapper.text()).toContain('QR-Only Start Required')
    })
  })
})
