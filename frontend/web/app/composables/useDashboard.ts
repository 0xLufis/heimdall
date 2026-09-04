import { ref, computed, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { 
  UsersIcon, 
  MonitorIcon, 
  AlertTriangleIcon, 
  ZapIcon 
} from "lucide-vue-next"

export const useDashboard = () => {
  const statsData = useState('dashboard_stats_data', () => ({
    totalUsers: '0',
    activeClients: '0',
    pendingAlerts: '0',
    avgUptime: '0%'
  }))

  const stats = computed(() => [
    { title: "Total Users", value: statsData.value.totalUsers, bgColor: "bg-slate-700", trend: "Live", icon: UsersIcon },
    { title: "Active Clients", value: statsData.value.activeClients, bgColor: "bg-zinc-700", trend: "Live", icon: MonitorIcon },
    { title: "Pending Alerts", value: statsData.value.pendingAlerts, bgColor: "bg-slate-800", trend: "24h", icon: AlertTriangleIcon },
    { title: "Avg. Uptime", value: statsData.value.avgUptime, bgColor: "bg-zinc-800", trend: "Live", icon: ZapIcon },
  ])

  const recentClients = useState<any[]>('dashboard_recent_clients', () => [])
  const securityEvents = useState<any[]>('dashboard_security_events', () => [])
  const loading = ref(false)
  let pollTimer: any = null

  const fetchDashboardData = async (silent: boolean = false) => {
    if (!silent) loading.value = true
    try {
      const data = await $fetch<any>('/api/proxy/Dashboard')
      if (data && data.stats) {
        statsData.value.totalUsers = String(data.stats.totalUsers || '0')
        statsData.value.activeClients = String(data.stats.activeClients || '0')
        statsData.value.pendingAlerts = String(data.stats.pendingAlerts || '0')
        statsData.value.avgUptime = String(data.stats.avgUptime || '99.8%')

        recentClients.value = data.recentClients || []
        securityEvents.value = data.securityEvents || []
      }
    } catch {
      // In SSR or test without running backend, keep defaults
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
