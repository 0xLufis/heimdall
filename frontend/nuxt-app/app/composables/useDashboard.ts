import { ref } from 'vue'
import { 
  UsersIcon, 
  MonitorIcon, 
  AlertTriangleIcon, 
  ZapIcon 
} from "lucide-vue-next"

export const useDashboard = () => {
  const stats = ref([
    { title: "Total Users", value: "1,284", bgColor: "bg-indigo-600", trend: "+12%", icon: UsersIcon },
    { title: "Active Clients", value: "842", bgColor: "bg-emerald-600", trend: "+5%", icon: MonitorIcon },
    { title: "Pending Alerts", value: "12", bgColor: "bg-rose-600", trend: "-2%", icon: AlertTriangleIcon },
    { title: "Avg. Uptime", value: "99.9%", bgColor: "bg-amber-600", trend: "Stable", icon: ZapIcon },
  ])

  const recentClients = ref([
    { id: 'PC-10293', hostname: 'LINE-A-OP1', os: 'Windows 10 Pro', lastSeen: '2 mins' },
    { id: 'PC-10294', hostname: 'LINE-A-OP2', os: 'Windows 10 Pro', lastSeen: '5 mins' },
    { id: 'PC-10295', hostname: 'LINE-B-CTRL', os: 'Ubuntu 22.04 LTS', lastSeen: '12 mins' },
  ])

  const securityEvents = ref([
    { title: 'New login detected', description: 'Admin logged in from a new Windows device in Budapest, HU.', time: '24 mins ago', severity: 'low' },
    { title: 'Unauthorized access attempt', description: 'Failed SSH login attempt from unknown IP: 192.168.1.45', time: '1 hour ago', severity: 'high' },
    { title: 'System update applied', description: 'Critical security patches applied to 42 client nodes.', time: '4 hours ago', severity: 'medium' },
    { title: 'New client registered', description: 'Node LINE-C-PACK-4 was successfully provisioned.', time: '1 day ago', severity: 'low' },
  ])

  const refreshDashboard = async () => {
    // In a real app, this would fetch from multiple API endpoints
    console.log('Refreshing dashboard data...')
  }

  return {
    stats,
    recentClients,
    securityEvents,
    refreshDashboard
  }
}
