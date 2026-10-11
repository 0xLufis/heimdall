import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useHeimdallNotifications } from '../../../src/frontend/web/app/composables/useHeimdallNotifications'
import {
  getNotifications,
  addNotification,
  markNotificationRead,
  markAllNotificationsRead,
  clearNotifications
} from '../../../src/frontend/web/server/utils/notificationsStore'
import {
  ensureTicketInStore,
  autoCloseTicketInStore,
  retractTicketInStore,
  editTicketWithHistoryInStore,
  findTicketById
} from '../../../src/frontend/web/server/utils/ticketsStore'

describe('Notifications System & Optimistic Actions', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    clearNotifications()
  })

  describe('notificationsStore', () => {
    it('adds and retrieves notifications', () => {
      const notif = addNotification({
        title: 'Spindle Warning',
        message: 'Spindle bearing vibration threshold exceeded',
        type: 'warning',
        source: 'machine'
      })

      expect(notif.id).toBeDefined()
      expect(notif.title).toBe('Spindle Warning')
      expect(notif.read).toBe(false)

      const all = getNotifications()
      expect(all.length).toBe(1)
      expect(all[0].id).toBe(notif.id)
    })

    it('marks single notification read and marks all read', () => {
      const n1 = addNotification({ title: 'Alert 1', message: 'Msg 1', type: 'info' })
      const n2 = addNotification({ title: 'Alert 2', message: 'Msg 2', type: 'error' })

      markNotificationRead(n1.id)
      expect(getNotifications().find(n => n.id === n1.id)?.read).toBe(true)
      expect(getNotifications().find(n => n.id === n2.id)?.read).toBe(false)

      const markedCount = markAllNotificationsRead()
      expect(markedCount).toBe(1)
      expect(getNotifications().every(n => n.read)).toBe(true)
    })
  })

  describe('useHeimdallNotifications composable', () => {
    it('manages toasts lifecycle: showToast and dismissToast', () => {
      const { activeToasts, showToast, dismissToast } = useHeimdallNotifications()

      const id = showToast({
        title: 'Test Toast',
        message: 'Toast message body',
        type: 'info',
        durationMs: 0
      })

      expect(activeToasts.value.some(t => t.id === id)).toBe(true)
      dismissToast(id)
      expect(activeToasts.value.some(t => t.id === id)).toBe(false)
    })

    it('runOptimisticAction: executes successfully and optionally shows success toast', async () => {
      const { runOptimisticAction, activeToasts } = useHeimdallNotifications()

      let state = 'initial'
      let rollbackCalled = false

      state = 'optimistic'
      const result = await runOptimisticAction(
        async () => {
          return 'server-ok'
        },
        () => {
          rollbackCalled = true
          state = 'initial'
        },
        {
          title: 'Update State',
          successMessage: 'State saved successfully'
        }
      )

      expect(result).toBe('server-ok')
      expect(state).toBe('optimistic')
      expect(rollbackCalled).toBe(false)
      expect(activeToasts.value.some(t => t.title === 'Update State')).toBe(true)
    })

    it('runOptimisticAction: rolls back state and dispatches error toast + notification on failure', async () => {
      const { runOptimisticAction, activeToasts } = useHeimdallNotifications()

      let state = 'initial'
      let rollbackCalled = false

      state = 'optimistic'

      await expect(
        runOptimisticAction(
          async () => {
            throw new Error('500 Internal Server Error')
          },
          () => {
            rollbackCalled = true
            state = 'initial'
          },
          {
            title: 'Equipment Transfer',
            failureTitle: 'Equipment Transfer Failed'
          }
        )
      ).rejects.toThrow('500 Internal Server Error')

      // State must be rolled back
      expect(rollbackCalled).toBe(true)
      expect(state).toBe('initial')

      // Error toast must be present
      const errToast = activeToasts.value.find(t => t.title === 'Equipment Transfer Failed')
      expect(errToast).toBeDefined()
      expect(errToast?.type).toBe('error')
      expect(errToast?.message).toContain('State rolled back')
    })
  })
})

describe('Ticketing Lifecycle: Auto-Close, Retract & Escalation', () => {
  const testTicketId = 'tkt-test-lifecycle-001'

  beforeEach(() => {
    ensureTicketInStore(testTicketId, {
      title: 'CNC Mill Bearing Overheating',
      status: 'Open',
      stationId: 'STATION-OP10-01',
      stationName: 'OP10 Machining Cell',
      isLineStop: true,
      lineStopDurationMinutes: 25
    })
  })

  it('auto-closes ticket when machine controller condition clears', () => {
    const updated = autoCloseTicketInStore(testTicketId, {
      machineId: 'STATION-OP10-01',
      controllerId: 'PLC-CTRL-OP10',
      clearedReason: 'Bearing thermal envelope normal (< 65C)',
      resolutionNotes: 'Self-cleared by machine telemetry watchdog'
    })

    expect(updated).toBeDefined()
    expect(updated?.status).toBe('Closed')
    expect(updated?.isLineStop).toBe(false)
    expect(updated?.resolvedAt).toBeDefined()

    // Transition comment must be recorded
    const autoCloseComment = updated?.comments.find(c => c.authorUserId === 'system-plc-autoclose')
    expect(autoCloseComment).toBeDefined()
    expect(autoCloseComment?.transition?.toStatus).toBe('Closed')
    expect(autoCloseComment?.transition?.actor).toBe('PLC-CTRL-OP10')
  })

  it('retracts ticket from kiosk view and updates pendingReason and history', () => {
    const updated = retractTicketInStore(
      testTicketId,
      'Kiosk Operator OP10',
      'Operator resolved workpiece alignment without technician intervention'
    )

    expect(updated).toBeDefined()
    expect(updated?.status).toBe('Closed')
    expect(updated?.pendingReason).toBe('Retracted')
    expect(updated?.pendingReasons).toContain('Retracted')

    const retractComment = updated?.comments.find(c => c.authorUserId === 'terminal-retract')
    expect(retractComment).toBeDefined()
    expect(retractComment?.transition?.reason).toContain('Operator resolved workpiece alignment')
  })

  it('supports multiple pending reasons simultaneously', () => {
    const ticket = findTicketById(testTicketId)
    expect(ticket).toBeDefined()

    const updated = editTicketWithHistoryInStore(
      testTicketId,
      {
        status: 'Pending',
        pendingReasons: ['Parts', 'ExternalOk'],
        pendingReason: 'Parts, ExternalOk',
        pendingDetails: 'Awaiting delivery of spindle belt and vendor QA stamp'
      },
      'Shift Supervisor',
      'Set multi-reason pending state'
    )

    expect(updated?.status).toBe('Pending')
    expect(updated?.pendingReasons).toEqual(['Parts', 'ExternalOk'])
    expect(updated?.pendingReason).toBe('Parts, ExternalOk')
    expect(updated?.pendingDetails).toContain('Awaiting delivery')
  })

  it('escalates an open ticket into pending (escalated) state with hand-off info', () => {
    const updated = editTicketWithHistoryInStore(
      testTicketId,
      {
        status: 'Pending',
        isEscalated: true,
        escalationReason: 'Spindle runaway exceeds standard floor repair playbook',
        escalationTarget: 'Gábor Varga (Plant Engineering Manager)',
        escalationHandoverState: 'HandedOffToField',
        assignedTechnicianName: 'Gábor Varga',
        escalatedBy: 'Technician István',
        escalatedAt: new Date().toISOString(),
        pendingReasons: ['Escalated'],
        pendingReason: 'Escalated',
        pendingDetails: 'Escalated to Gábor Varga (Plant Engineering Manager)'
      },
      'Technician István',
      'Escalation with work order hand-off'
    )

    expect(updated?.isEscalated).toBe(true)
    expect(updated?.status).toBe('Pending')
    expect(updated?.assignedTechnicianName).toBe('Gábor Varga')
    expect(updated?.escalationHandoverState).toBe('HandedOffToField')
    expect(updated?.pendingReasons).toContain('Escalated')
  })
})
