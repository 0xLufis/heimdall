import { ref, computed } from 'vue'

export interface NotificationItem {
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

export interface ToastItem {
  id: string
  title: string
  message: string
  type: 'info' | 'success' | 'warning' | 'error' | 'critical'
  durationMs: number
  undoAction?: () => Promise<void> | void
  actionLabel?: string
  onAction?: () => void
  link?: string
  timestamp: number
}

export interface OptimisticActionOptions {
  title?: string
  failureTitle?: string
  failureMessage?: string
  successMessage?: string
  source?: 'ticket' | 'machine' | 'telemetry' | 'system' | 'optimistic'
  link?: string
  onUndo?: () => Promise<void> | void
}

// Global singletons across the client session
const notifications = ref<NotificationItem[]>([])
const activeToasts = ref<ToastItem[]>([])
const isLoading = ref(false)
const browserNotificationsGranted = ref(false)

if (typeof window !== 'undefined' && 'Notification' in window) {
  browserNotificationsGranted.value = window.Notification.permission === 'granted'
}

export function useHeimdallNotifications() {
  const unreadCount = computed(() => notifications.value.filter(n => !n.read).length)

  // ─── Browser / Android Notification Channel ───────────────────────────────
  async function requestBrowserNotificationPermission(): Promise<boolean> {
    if (typeof window === 'undefined' || !('Notification' in window)) {
      console.warn('Browser notifications are not supported in this environment.')
      return false
    }

    try {
      const permission = await window.Notification.requestPermission()
      browserNotificationsGranted.value = permission === 'granted'
      return browserNotificationsGranted.value
    } catch (err) {
      console.error('Error requesting notification permission:', err)
      return false
    }
  }

  function sendBrowserNotification(title: string, options?: NotificationOptions) {
    if (typeof window === 'undefined' || !('Notification' in window)) return
    if (window.Notification.permission !== 'granted') return

    try {
      const defaultIcon = '/favicon.ico'
      const notif = new window.Notification(title, {
        icon: defaultIcon,
        badge: defaultIcon,
        ...options
      })

      notif.onclick = () => {
        window.focus()
        if (options?.data?.link) {
          window.location.href = options.data.link
        }
      }
    } catch (err) {
      console.error('Error dispatching browser notification:', err)
    }
  }

  // ─── Top-Right Toast Overlay UI ───────────────────────────────────────────
  function showToast(options: {
    title: string
    message: string
    type?: 'info' | 'success' | 'warning' | 'error' | 'critical'
    durationMs?: number
    undoAction?: () => Promise<void> | void
    actionLabel?: string
    onAction?: () => void
    link?: string
  }): string {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`
    const type = options.type || 'info'
    const durationMs = options.durationMs !== undefined ? options.durationMs : (type === 'critical' ? 8000 : 5000)

    const toast: ToastItem = {
      id,
      title: options.title,
      message: options.message,
      type,
      durationMs,
      undoAction: options.undoAction,
      actionLabel: options.actionLabel,
      onAction: options.onAction,
      link: options.link,
      timestamp: Date.now()
    }

    activeToasts.value.unshift(toast)

    // Keep at most 5 active toasts on screen
    if (activeToasts.value.length > 5) {
      activeToasts.value = activeToasts.value.slice(0, 5)
    }

    if (durationMs > 0) {
      setTimeout(() => {
        dismissToast(id)
      }, durationMs)
    }

    return id
  }

  function dismissToast(id: string) {
    const idx = activeToasts.value.findIndex(t => t.id === id)
    if (idx !== -1) {
      activeToasts.value.splice(idx, 1)
    }
  }

  // ─── Optimistic Rendering with State Rollback & Undo Notification ───────────
  async function runOptimisticAction<T>(
    actionFn: () => Promise<T>,
    rollbackFn: () => void,
    options: OptimisticActionOptions = {}
  ): Promise<T> {
    try {
      const result = await actionFn()
      if (options.successMessage) {
        showToast({
          title: options.title || 'Action Complete',
          message: options.successMessage,
          type: 'success',
          undoAction: options.onUndo,
          actionLabel: options.onUndo ? 'Undo' : undefined,
          link: options.link
        })
      }
      return result
    } catch (err: any) {
      // Revert optimistic changes
      try {
        rollbackFn()
      } catch (rollbackErr) {
        console.error('Error executing rollback:', rollbackErr)
      }

      const failureTitle = options.failureTitle || `${options.title || 'State Update'} Failed`
      const failureMsg = options.failureMessage || err?.message || err?.statusMessage || 'Action rejected by server. Changes have been rolled back.'

      // Show top-right overlay error toast with undo/revert indicator
      showToast({
        title: failureTitle,
        message: `${failureMsg} (State rolled back to previous value)`,
        type: 'error',
        durationMs: 7000,
        link: options.link
      })

      // Add failure notification to store & unread notifications list
      await addNotification({
        title: failureTitle,
        message: `${failureMsg} (State was reverted to preserve data integrity)`,
        type: 'error',
        source: options.source || 'optimistic',
        link: options.link,
        showOverlayToast: false
      })

      throw err
    }
  }

  // ─── Notification Store Sync ──────────────────────────────────────────────
  async function fetchNotifications() {
    if (typeof $fetch === 'undefined') return
    isLoading.value = true
    try {
      const res = await $fetch<{ notifications: NotificationItem[]; total: number; unreadCount: number }>('/api/notifications')
      if (res?.notifications) {
        notifications.value = res.notifications
      }
    } catch (err) {
      console.warn('Could not fetch notifications from server:', err)
    } finally {
      isLoading.value = false
    }
  }

  async function markAsRead(id: string) {
    const item = notifications.value.find(n => n.id === id)
    if (item) item.read = true

    if (typeof $fetch !== 'undefined') {
      try {
        await $fetch(`/api/notifications/${id}/read`, { method: 'PATCH' })
      } catch (err) {
        console.warn('Error marking notification read:', err)
      }
    }
  }

  async function markAllAsRead() {
    notifications.value.forEach(n => { n.read = true })

    if (typeof $fetch !== 'undefined') {
      try {
        await $fetch('/api/notifications/mark-all-read', { method: 'POST' })
      } catch (err) {
        console.warn('Error marking all notifications read:', err)
      }
    }
  }

  async function addNotification(input: {
    title: string
    message: string
    type?: 'info' | 'success' | 'warning' | 'error' | 'critical'
    source?: 'ticket' | 'machine' | 'telemetry' | 'system' | 'optimistic'
    link?: string
    showOverlayToast?: boolean
    triggerBrowserNotification?: boolean
  }): Promise<NotificationItem> {
    const newItem: NotificationItem = {
      id: `notif-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      title: input.title,
      message: input.message,
      type: input.type || 'info',
      timestamp: new Date().toISOString(),
      read: false,
      source: input.source || 'system',
      link: input.link
    }

    // Immediate optimistic local insertion
    notifications.value.unshift(newItem)

    // Optional top-right toast overlay
    if (input.showOverlayToast !== false) {
      showToast({
        title: input.title,
        message: input.message,
        type: input.type || 'info',
        link: input.link
      })
    }

    // Optional out-of-app / Android browser notification
    if (input.triggerBrowserNotification !== false && browserNotificationsGranted.value) {
      sendBrowserNotification(input.title, {
        body: input.message,
        data: { link: input.link }
      })
    }

    // Persist to server
    if (typeof $fetch !== 'undefined') {
      try {
        const res = await $fetch<{ success: boolean; notification: NotificationItem }>('/api/notifications', {
          method: 'POST',
          body: {
            title: input.title,
            message: input.message,
            type: input.type || 'info',
            source: input.source || 'system',
            link: input.link
          }
        })
        if (res?.notification) {
          newItem.id = res.notification.id
        }
      } catch (err) {
        console.warn('Failed to persist notification to server:', err)
      }
    }

    return newItem
  }

  return {
    notifications,
    activeToasts,
    unreadCount,
    isLoading,
    browserNotificationsGranted,
    requestBrowserNotificationPermission,
    sendBrowserNotification,
    showToast,
    dismissToast,
    runOptimisticAction,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    addNotification
  }
}
