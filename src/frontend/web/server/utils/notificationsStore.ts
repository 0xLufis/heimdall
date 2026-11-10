export interface StoredNotification {
  id: string
  title: string
  message: string
  type: 'info' | 'success' | 'warning' | 'error' | 'critical'
  timestamp: string
  read: boolean
  userId?: string
  source?: 'ticket' | 'machine' | 'telemetry' | 'system' | 'optimistic'
  link?: string
}

let inMemoryNotifications: StoredNotification[] = [
  {
    id: 'notif-001',
    title: 'Spindle Vibration Harmonic Warning',
    message: 'Machine STATION-OP10-01 exceeded harmonic vibration envelope threshold (4.5 mm/s RMS).',
    type: 'critical',
    timestamp: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    read: false,
    source: 'machine',
    link: '/dashboard/machines'
  },
  {
    id: 'notif-002',
    title: 'Ticket Escalated to Field',
    message: 'Ticket TKT-20260928-0001 escalated to Gábor Varga (Dedicated Shift Engineer) for toolholder inspection.',
    type: 'warning',
    timestamp: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    read: false,
    source: 'ticket',
    link: '/dashboard/tickets'
  },
  {
    id: 'notif-003',
    title: 'Copia Sync Staged',
    message: 'Local PLC changes on STATION-OP10-01 staged in commit loc-7f9a2b1c. Review visual diff before pushing.',
    type: 'info',
    timestamp: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    read: true,
    source: 'system'
  }
]

export function getNotifications(userId?: string): StoredNotification[] {
  if (userId) {
    return inMemoryNotifications.filter(n => !n.userId || n.userId === userId)
  }
  return inMemoryNotifications
}

export function addNotification(notif: Omit<StoredNotification, 'id' | 'timestamp' | 'read'> & { id?: string; timestamp?: string; read?: boolean }): StoredNotification {
  const full: StoredNotification = {
    id: notif.id || `notif-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
    title: notif.title,
    message: notif.message,
    type: notif.type,
    timestamp: notif.timestamp || new Date().toISOString(),
    read: notif.read ?? false,
    userId: notif.userId,
    source: notif.source || 'system',
    link: notif.link
  }
  inMemoryNotifications.unshift(full)
  // Keep max 200 items in memory
  if (inMemoryNotifications.length > 200) {
    inMemoryNotifications = inMemoryNotifications.slice(0, 200)
  }
  return full
}

export function markNotificationRead(id: string): StoredNotification | undefined {
  const notif = inMemoryNotifications.find(n => n.id === id)
  if (notif) {
    notif.read = true
  }
  return notif
}

export function markAllNotificationsRead(userId?: string): number {
  let count = 0
  for (const n of inMemoryNotifications) {
    if (!userId || !n.userId || n.userId === userId) {
      if (!n.read) {
        n.read = true
        count++
      }
    }
  }
  return count
}

export function deleteNotification(id: string): boolean {
  const idx = inMemoryNotifications.findIndex(n => n.id === id)
  if (idx !== -1) {
    inMemoryNotifications.splice(idx, 1)
    return true
  }
  return false
}

export function clearNotifications(userId?: string): void {
  if (userId) {
    inMemoryNotifications = inMemoryNotifications.filter(n => n.userId !== userId)
  } else {
    inMemoryNotifications = []
  }
}

