import { ref, onMounted } from 'vue'
import { 
  UsersIcon, 
  MonitorIcon, 
  AlertTriangleIcon, 
  ZapIcon 
} from "lucide-vue-next"

/**
 * Composable for managing the live dashboard state and telemetry data.
 * Handles fetching statistics, recent client activity, and security events from the backend.
 * 
 * @returns {Object} Reactive state and methods for the dashboard.
 * @property {Ref<Array>} stats - High-level metrics for the Stats Grid.
 * @property {Ref<Array>} recentClients - List of the most recently active edge nodes.
 * @property {Ref<Array>} securityEvents - Activity feed of recent system/security events.
 * @property {Function} refreshDashboard - Method to manually trigger a data synchronization.
 * @property {Ref<boolean>} loading - Indicator for active background fetch operations.
 */
export const useDashboard = () => {
  /**
   * Reactive state for the Stats Grid, initialized with placeholders.
   */
  const stats = ref([
    { title: "Total Users", value: "0", bgColor: "bg-slate-700", trend: "...", icon: UsersIcon },
    { title: "Active Clients", value: "0", bgColor: "bg-zinc-700", trend: "...", icon: MonitorIcon },
    { title: "Pending Alerts", value: "0", bgColor: "bg-slate-800", trend: "...", icon: AlertTriangleIcon },
    { title: "Avg. Uptime", value: "0%", bgColor: "bg-zinc-800", trend: "...", icon: ZapIcon },
  ])

  /** Recent client nodes. @type {Ref<any[]>} */
  const recentClients = ref<any[]>([])
  
  /** Security and system activity events. @type {Ref<any[]>} */
  const securityEvents = ref<any[]>([])
  
  /** Loading state indicator. @type {Ref<boolean>} */
  const loading = ref(false)

  /**
   * Internal function to fetch live dashboard telemetry from the proxy API.
   * Maps backend data to local reactive state.
   */
  const fetchDashboardData = async () => {
    loading.value = true
    try {
      const data = await $fetch<any>('/api/proxy/Dashboard')
      if (data) {
        // Update stats
        stats.value[0].value = data.stats.totalUsers
        stats.value[1].value = data.stats.activeClients
        stats.value[2].value = data.stats.pendingAlerts
        stats.value[3].value = data.stats.avgUptime
        
        // Trends are not implemented in backend yet, so we keep them as is or set to static
        stats.value[0].trend = "Live"
        stats.value[1].trend = "Live"
        stats.value[2].trend = "24h"
        stats.value[3].trend = "Live"

        recentClients.value = data.recentClients
        securityEvents.value = data.securityEvents
      }
    } catch (e) {
      console.error('Failed to fetch dashboard data:', e)
    } finally {
      loading.value = false
    }
  }

  /**
   * Triggers a manual refresh of the dashboard data.
   */
  const refreshDashboard = async () => {
    await fetchDashboardData()
  }

  onMounted(() => {
    fetchDashboardData()
  })

  return {
    stats,
    recentClients,
    securityEvents,
    refreshDashboard,
    loading
  }
}
