import { ref, onMounted, onUnmounted } from 'vue'
import { 
  UsersIcon, 
  MonitorIcon, 
  AlertTriangleIcon, 
  ZapIcon 
} from "lucide-vue-next"

export const useDashboard = () => {
  const stats = ref([
    { title: "Total Users", value: "0", bgColor: "bg-slate-700", trend: "Live", icon: UsersIcon },
    { title: "Active Clients", value: "0", bgColor: "bg-zinc-700", trend: "Live", icon: MonitorIcon },
    { title: "Pending Alerts", value: "0", bgColor: "bg-slate-800", trend: "24h", icon: AlertTriangleIcon },
    { title: "Avg. Uptime", value: "0%", bgColor: "bg-zinc-800", trend: "Live", icon: ZapIcon },
  ])

  const recentClients = ref<any[]>([])
  const securityEvents = ref<any[]>([])
  const loading = ref(false)
  let pollTimer: any = null

  const fetchDashboardData = async (silent: boolean = false) => {
    if (!silent) loading.value = true
    try {
      const data = await $fetch<any>('/api/proxy/Dashboard')
      if (data && data.stats) {
        stats.value[0].value = String(data.stats.totalUsers || '0')
        stats.value[1].value = String(data.stats.activeClients || '0')
        stats.value[2].value = String(data.stats.pendingAlerts || '0')
        stats.value[3].value = String(data.stats.avgUptime || '99.8%')

        recentClients.value = data.recentClients || []
        securityEvents.value = data.securityEvents || []
      }
    } catch (e) {
      console.warn('Dashboard telemetry sync note:', e)
    } finally {
      if (!silent) loading.value = false
    }
  }

  const refreshDashboard = async () => {
    await fetchDashboardData(false)
  }

  onMounted(() => {
    fetchDashboardData(false)
    if (typeof window !== 'undefined') {
      pollTimer = setInterval(() => {
        fetchDashboardData(true)
      }, 4000)
    }
  })

  onUnmounted(() => {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  })

  return {
    stats,
    recentClients,
    securityEvents,
    refreshDashboard,
    loading
  }
}
