import { ref, computed, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { 
  UsersIcon, 
  MonitorIcon, 
  AlertTriangleIcon, 
  ZapIcon 
} from "lucide-vue-next"
import { useAppI18n } from '~/composables/useAppI18n'

export const useDashboard = () => {
  const { t } = useAppI18n()

  const statsData = useState('dashboard_stats_data', () => ({
    totalUsers: '0',
    activeClients: '0',
    pendingAlerts: '0',
    avgUptime: '0%'
  }))

  const stats = computed(() => [
    { title: t('dashboard.totalUsers', 'Total Users'), value: statsData.value.totalUsers, bgColor: "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20", trend: "Live", icon: UsersIcon },
    { title: t('dashboard.activeClients', 'Active Clients'), value: statsData.value.activeClients, bgColor: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20", trend: "Live", icon: MonitorIcon },
    { title: t('dashboard.pendingAlerts', 'Pending Alerts'), value: statsData.value.pendingAlerts, bgColor: "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20", trend: "24h", icon: AlertTriangleIcon },
    { title: t('dashboard.avgUptime', 'Avg. Uptime'), value: statsData.value.avgUptime, bgColor: "bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20", trend: "Live", icon: ZapIcon },
  ])

  const recentClients = useState<any[]>('dashboard_recent_clients', () => [])
  const securityEvents = useState<any[]>('dashboard_security_events', () => [])
  const loading = ref(false)
  let pollTimer: any = null

  const fetchDashboardData = async (silent: boolean = false) => {
    if (!silent) loading.value = true
    try {
      try {
        const data = await $fetch<any>('/api/proxy/Dashboard')
        if (data && data.stats) {
          statsData.value.totalUsers = String(data.stats.totalUsers || '0')
          statsData.value.activeClients = String(data.stats.activeClients || '0')
          statsData.value.pendingAlerts = String(data.stats.pendingAlerts || '0')
          statsData.value.avgUptime = String(data.stats.avgUptime || '99.8%')

          recentClients.value = data.recentClients || []
          securityEvents.value = data.securityEvents || []
          return
        }
      } catch {
        // Fallback to local live entity stores
      }

      // Fetch actual data from local API endpoints
      const [machinesRes, ticketsRes, usersRes, auditRes] = await Promise.allSettled([
        $fetch<any[]>('/api/machines'),
        $fetch<any[]>('/api/tickets'),
        $fetch<any[]>('/api/users'),
        $fetch<any>('/api/analytics/audit-logs?limit=8')
      ])

      const machines = machinesRes.status === 'fulfilled' && Array.isArray(machinesRes.value) ? machinesRes.value : []
      const tickets = ticketsRes.status === 'fulfilled' && Array.isArray(ticketsRes.value) ? ticketsRes.value : []
      const users = usersRes.status === 'fulfilled' && Array.isArray(usersRes.value) ? usersRes.value : []
      const auditData = auditRes.status === 'fulfilled' ? auditRes.value : null

      const activeMachinesCount = machines.filter(m => m.status === 'active' || m.status === 'maintenance').length
      const openTicketsCount = tickets.filter(t => t.status !== 'Resolved' && t.status !== 'Closed').length

      statsData.value.totalUsers = usersRes.status === 'fulfilled' ? String(users.length) : '0'
      statsData.value.activeClients = machinesRes.status === 'fulfilled' ? String(activeMachinesCount) : '0'
      statsData.value.pendingAlerts = ticketsRes.status === 'fulfilled' ? String(openTicketsCount) : '0'
      statsData.value.avgUptime = machines.length > 0
        ? `${((activeMachinesCount / machines.length) * 100).toFixed(1)}%`
        : '99.8%'

      if (machines.length > 0) {
        recentClients.value = machines.slice(0, 5).map(m => ({
          id: m.id,
          name: m.displayName || m.name,
          hostname: m.customIdentifier || m.name,
          ipAddress: m.controllers?.[0]?.ipAddress || '192.168.1.100',
          isOnline: m.status === 'active',
          lastSeen: 'Just now'
        }))
      }

      if (auditData?.entries && Array.isArray(auditData.entries)) {
        securityEvents.value = auditData.entries.map((e: any) => ({
          title: e.entityName || e.action.replace('_', ' '),
          description: e.summary,
          time: new Date(e.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          severity: e.severity === 'critical' ? 'high' : e.severity === 'warning' ? 'medium' : 'low'
        }))
      }
    } catch (err) {
      console.warn('[useDashboard] Error fetching local dashboard metrics:', err)
    } finally {
      if (!silent) loading.value = false
    }
  }

  const refreshDashboard = async () => {
    await fetchDashboardData(false)
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      fetchDashboardData(false)
      if (typeof window !== 'undefined') {
        pollTimer = setInterval(() => {
          fetchDashboardData(true)
        }, 5000)
      }
    })

    onUnmounted(() => {
      if (pollTimer) {
        clearInterval(pollTimer)
        pollTimer = null
      }
    })
  }

  return {
    stats,
    recentClients,
    securityEvents,
    refreshDashboard,
    loading
  }
}
