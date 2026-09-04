import { describe, it, expect } from 'vitest'
import {
  ERROR_TEMPLATES,
  CATEGORIES,
  getTemplatesByCategory,
  getTemplatesByGroup,
  getTemplateById,
  applyTemplate
} from '../../../frontend/web/app/utils/errorTemplateEngine'
import {
  resolvePreferredTechnician
} from '../../../frontend/web/app/utils/technicianInheritance'
import {
  generateQrUri,
  parseQrUri
} from '../../../frontend/web/app/utils/qrActionGenerator'
import {
  getAllGroups,
  getAllMachineIdsInGroup,
  createGroup,
  deleteGroup
} from '../../../frontend/web/server/utils/machineGroupsStore'
import {
  getAllRules,
  createRule,
  getAllAbsences,
  createAbsence,
  resolveAbsence,
  getTeamsOooStatuses,
  toggleSimulatedOoo,
  isPersonAvailable
} from '../../../frontend/web/server/utils/technicianRulesStore'
import {
  updateTicketStatus,
  addAttachmentToTicket,
  findTicketById
} from '../../../frontend/web/server/utils/ticketsStore'

describe('Maintenance Full Incident Lifecycle Suite', () => {

  describe('4-Tier Error Templating Engine', () => {
    it('provides all 4 standard categories', () => {
      expect(CATEGORIES).toContain('Error')
      expect(CATEGORIES).toContain('Prevention')
      expect(CATEGORIES).toContain('Improvement')
      expect(CATEGORIES).toContain('ETC')
    })

    it('contains valid templates with FB state, telemetry keys, and Kanban target state', () => {
      const eMot = getTemplateById('E-MOT-01')
      expect(eMot).toBeDefined()
      expect(eMot?.category).toBe('Error')
      expect(eMot?.errorGroup).toBe('Motion & Drive')
      expect(eMot?.errorCode).toBe('E-MOT-01')
      expect(eMot?.sampleFbState?.blockName).toBe('FB_AxisControl')
      expect(eMot?.targetKanbanState).toBe('In_Progress')
      expect(eMot?.defaultTags).toContain('#Motion')

      const eSafe = getTemplateById('E-SAFE-01')
      expect(eSafe?.targetKanbanState).toBe('Escalated')

      const eNet = getTemplateById('E-NET-01')
      expect(eNet?.targetKanbanState).toBe('Escalated_External')
      expect(eNet?.externalEscalationTarget).toBe('SAP Engineers')

      const pCal = getTemplateById('P-CAL-01')
      expect(pCal?.targetKanbanState).toBe('Closure_Pending')
    })

    it('filters templates by category and error group', () => {
      const errorTmpls = getTemplatesByCategory('Error')
      expect(errorTmpls.length).toBeGreaterThan(5)

      const motionTmpls = getTemplatesByGroup('Motion & Drive')
      expect(motionTmpls.length).toBeGreaterThanOrEqual(2)
      expect(motionTmpls.every(t => t.errorGroup === 'Motion & Drive')).toBe(true)
    })

    it('interpolates template with machine context and workpiece SFC', () => {
      const tmpl = getTemplateById('E-MOT-01')!
      const applied = applyTemplate(tmpl, {
        stationName: 'OP10 Machining Cell',
        sfcSerial: 'SFC-BAT-20260904-8841'
      })
      expect(applied.title).toContain('[E-MOT-01]')
      expect(applied.title).toContain('OP10 Machining Cell')
      expect(applied.title).toContain('SFC-BAT-20260904-8841')
      expect(applied.tags).toContain('#Motion')
      expect(applied.tags).toContain('#SFC-8841')
    })
  })

  describe('Hierarchical Preferred Technician & Absence Engine', () => {
    const mockRules = [
      {
        id: 'r1',
        name: 'Milling Specialist',
        technicianId: 'tech-sally',
        technicianName: 'Engineer Sally',
        scopeType: 'technology' as const,
        targetId: 'Milling',
        backupTechnicianName: 'Gábor Varga'
      },
      {
        id: 'r2',
        name: 'AUDI Line Mechanical',
        technicianId: 'tech-orwell',
        technicianName: 'Engineer Orwell',
        scopeType: 'group' as const,
        targetId: 'grp-line06',
        categoryFilter: 'Mechanical',
        backupTechnicianName: 'Zoltán Németh'
      },
      {
        id: 'r3',
        name: 'Specific Machine Dedicated',
        technicianId: 'tech-kovacs',
        technicianName: 'István Kovács',
        scopeType: 'machine' as const,
        targetId: 'STATION-SPECIAL-01'
      }
    ]

    it('resolves machine-level override with top priority', () => {
      const res = resolvePreferredTechnician('STATION-SPECIAL-01', 'Milling', 'grp-line06', mockRules, [])
      expect(res).toBeDefined()
      expect(res?.technicianName).toBe('István Kovács')
      expect(res?.source).toBe('machine_override')
    })

    it('falls back to group-level rule when no machine override is defined', () => {
      const res = resolvePreferredTechnician('STATION-REGULAR-02', 'Milling', 'grp-line06', mockRules, [])
      expect(res).toBeDefined()
      expect(res?.technicianName).toBe('Engineer Orwell')
      expect(res?.source).toBe('group_rule')
    })

    it('falls back to technology rule when no machine or group rule matches', () => {
      const res = resolvePreferredTechnician('STATION-OTHER-03', 'Milling', 'grp-other', mockRules, [])
      expect(res).toBeDefined()
      expect(res?.technicianName).toBe('Engineer Sally')
      expect(res?.source).toBe('technology_rule')
    })

    it('flags absent technicians and routes to backup', () => {
      const absences = [
        {
          id: 'a1',
          technicianId: 'tech-sally',
          technicianName: 'Engineer Sally',
          reason: 'Vacation' as const,
          startDate: new Date().toISOString(),
          endDate: new Date(Date.now() + 86400000).toISOString(),
          markedBy: 'Shift Leader Ferenc',
          backupTechnicianName: 'Gábor Varga',
          active: true
        }
      ]
      const res = resolvePreferredTechnician('STATION-04', 'Milling', undefined, mockRules, absences)
      expect(res?.isAbsent).toBe(true)
      expect(res?.absenceReason).toBe('Vacation')
      expect(res?.backupTechnicianName).toBe('Gábor Varga')
    })
  })

  describe('Actionable QR Code URIs', () => {
    it('generates actionable URIs with query parameters', () => {
      const uri = generateQrUri({
        action: 'report-incident',
        stationId: 'STATION-OP10-01',
        machineType: 'Milling',
        groupId: 'grp-line06'
      })
      expect(uri).toContain('action=report-incident')
      expect(uri).toContain('stationId=STATION-OP10-01')
      expect(uri).toContain('machineType=Milling')
      expect(uri).toContain('groupId=grp-line06')
    })

    it('parses web URL and heimdall:// action URIs accurately', () => {
      const parsedWeb = parseQrUri('https://heimdall.local/mobile/action?action=report-incident&stationId=L06-OP150&machineType=Gap%20Filler')
      expect(parsedWeb).toBeDefined()
      expect(parsedWeb?.action).toBe('report-incident')
      expect(parsedWeb?.stationId).toBe('L06-OP150')
      expect(parsedWeb?.machineType).toBe('Gap Filler')

      const parsedApp = parseQrUri('heimdall://action?action=view-ticket&ticketId=tkt-001')
      expect(parsedApp).toBeDefined()
      expect(parsedApp?.action).toBe('view-ticket')
      expect(parsedApp?.ticketId).toBe('tkt-001')
    })

    it('gracefully returns null for invalid non-action strings', () => {
      expect(parseQrUri('INVALID-RAW-BARCODE-ONLY')).toBeNull()
    })
  })

  describe('Recursive Machine Grouping Hierarchy', () => {
    it('returns plant, lines, and cells hierarchy', () => {
      const all = getAllGroups()
      expect(all.some(g => g.id === 'grp-plant')).toBe(true)
      expect(all.some(g => g.id === 'grp-line06')).toBe(true)
      expect(all.some(g => g.id === 'grp-cell-a')).toBe(true)
    })

    it('recursively gathers all machines in a group and child subgroups', () => {
      const machines = getAllMachineIdsInGroup('grp-line06')
      expect(machines).toContain('STATION-OP10-01')
      expect(machines).toContain('STATION-SC-L06-03')
      expect(machines).toContain('STATION-AOI-L06-02')
    })
  })

  describe('Teams Out of Office & Shift Absence Notification Exclusion', () => {
    it('checks person availability against both shift absences and Teams OOO', () => {
      const status = isPersonAvailable('usr-orwell')
      expect(status.available).toBe(false)
      expect(status.reason).toContain('Teams Out of Office')

      const available = isPersonAvailable('usr-sally')
      expect(available.available).toBe(true)
    })

    it('allows toggling simulated Teams OOO state in development', () => {
      toggleSimulatedOoo('usr-sally', true)
      const after = isPersonAvailable('usr-sally')
      expect(after.available).toBe(false)

      toggleSimulatedOoo('usr-sally', false)
      const restored = isPersonAvailable('usr-sally')
      expect(restored.available).toBe(true)
    })
  })

  describe('8-Stage Kanban Lifecycle & Transition Comments', () => {
    it('denotes state transitions in comments when status updates', () => {
      const updated = updateTicketStatus('tkt-001', 'Closure_Pending', 'Technician Ferenc')
      expect(updated?.status).toBe('Closure_Pending')

      const latestComment = updated?.comments[updated.comments.length - 1]
      expect(latestComment?.transition).toBeDefined()
      expect(latestComment?.transition?.toStatus).toBe('Closure_Pending')
    })

    it('supports attaching images to tickets and specific comments', () => {
      const att = addAttachmentToTicket('tkt-001', {
        fileName: 'repair_finish.jpg',
        contentType: 'image/jpeg',
        fileSize: 102400,
        url: 'data:image/jpeg;base64,...'
      })
      expect(att).toBeDefined()
      expect(att?.ticketId).toBe('tkt-001')

      const tkt = findTicketById('tkt-001')
      expect(tkt?.attachments.some(a => a.fileName === 'repair_finish.jpg')).toBe(true)
    })
  })
})
