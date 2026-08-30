export interface TicketComment {
  id: string
  ticketId: string
  authorUserId: string
  authorName: string
  content: string
  createdAt: string
}

export interface TicketAttachment {
  id: string
  ticketId: string
  fileName: string
  contentType: string
  fileSize: number
  uploadedAt: string
}

export interface MaintenanceTicket {
  id: string
  ticketNumber: string
  stationId: string
  stationName: string
  controllerId?: string
  title: string
  description: string
  status: 'Open' | 'In_Progress' | 'Pending_Parts' | 'Resolved' | 'Closed'
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  reportedByUserId: string
  reportedByUserName: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt: string
  updatedAt: string
  slaDueAt: string
  resolvedAt?: string
  comments: TicketComment[]
  attachments: TicketAttachment[]
}

const initialTickets: MaintenanceTicket[] = [
  {
    id: 'tkt-001',
    ticketNumber: 'TKT-20260830-0001',
    stationId: 'STATION-OP10-01',
    stationName: 'OP10 Machining Cell',
    controllerId: 'ctrl-101',
    title: 'Spindle Bearing Overheating Alert',
    description: 'Vibration and thermal sensor reported 85°C on main spindle bearing during high-speed pass.',
    status: 'In_Progress',
    priority: 'Critical',
    reportedByUserId: 'usr-op-01',
    reportedByUserName: 'István Kovács (Operator)',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)',
    createdAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 1 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 1 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-01',
        ticketId: 'tkt-001',
        authorUserId: 'usr-op-01',
        authorName: 'István Kovács',
        content: 'Reported during 2nd shift execution. Coolant level is normal.',
        createdAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString()
      },
      {
        id: 'c-02',
        ticketId: 'tkt-001',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Inspected with thermal camera. Bearing housing replacement scheduled.',
        createdAt: new Date(Date.now() - 1 * 3600 * 1000).toISOString()
      }
    ],
    attachments: [
      {
        id: 'att-01',
        ticketId: 'tkt-001',
        fileName: 'thermal_scan_op10.jpg',
        contentType: 'image/jpeg',
        fileSize: 412000,
        uploadedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString()
      }
    ]
  },
  {
    id: 'tkt-002',
    ticketNumber: 'TKT-20260830-0002',
    stationId: 'STATION-OP20-02',
    stationName: 'OP20 Robotic Welding Station',
    controllerId: 'ctrl-201',
    title: 'KUKA Robot Axis 3 Servo Alarm E-409',
    description: 'Robot controller halted execution with position feedback divergence error.',
    status: 'Pending_Parts',
    priority: 'High',
    reportedByUserId: 'usr-op-02',
    reportedByUserName: 'Péter Tóth',
    assignedTechnicianId: 'usr-tech-02',
    assignedTechnicianName: 'Zoltán Németh',
    createdAt: new Date(Date.now() - 6 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 2 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-03',
        ticketId: 'tkt-002',
        authorUserId: 'usr-tech-02',
        authorName: 'Zoltán Németh',
        content: 'Encoder cable damaged. Replacement part ordered from spare store.',
        createdAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString()
      }
    ],
    attachments: []
  },
  {
    id: 'tkt-003',
    ticketNumber: 'TKT-20260830-0003',
    stationId: 'STATION-OP30-03',
    stationName: 'OP30 Automated Quality Inspector',
    title: 'Cognex Camera Lens Cleaning & Calibration',
    description: 'Routine maintenance: dirty lens optics triggering false reject rate of 1.4%.',
    status: 'Open',
    priority: 'Medium',
    reportedByUserId: 'usr-op-03',
    reportedByUserName: 'Katalin Nagy',
    createdAt: new Date(Date.now() - 12 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 12 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 12 * 3600 * 1000).toISOString(),
    comments: [],
    attachments: []
  },
  {
    id: 'tkt-004',
    ticketNumber: 'TKT-20260830-0004',
    stationId: 'IPC-L1-01',
    stationName: 'IPC Line 1 Master',
    title: 'Win10 IoT System Update & OPC-UA Driver Patch',
    description: 'Apply security rollup and update OPC-UA client protocol bindings.',
    status: 'Resolved',
    priority: 'Low',
    reportedByUserId: 'usr-admin',
    reportedByUserName: 'System Admin',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)',
    createdAt: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
    resolvedAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-04',
        ticketId: 'tkt-004',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Patch applied successfully. Gateway ping tests verified.',
        createdAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString()
      }
    ],
    attachments: []
  }
]

let ticketsStore: MaintenanceTicket[] = [...initialTickets]

export function getTicketsStore(): MaintenanceTicket[] {
  return ticketsStore
}

export function addTicketToStore(ticket: MaintenanceTicket): MaintenanceTicket {
  ticketsStore.unshift(ticket)
  return ticket
}

export function findTicketById(id: string): MaintenanceTicket | undefined {
  return ticketsStore.find(t => t.id === id || t.ticketNumber === id)
}

export function updateTicketInStore(id: string, updates: Partial<MaintenanceTicket>): MaintenanceTicket | undefined {
  const ticket = findTicketById(id)
  if (!ticket) return undefined
  
  Object.assign(ticket, updates, { updatedAt: new Date().toISOString() })
  if (updates.status === 'Resolved' && !ticket.resolvedAt) {
    ticket.resolvedAt = new Date().toISOString()
  }
  return ticket
}

export function addCommentToTicket(ticketId: string, comment: TicketComment): TicketComment | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined
  ticket.comments.push(comment)
  ticket.updatedAt = new Date().toISOString()
  return comment
}
