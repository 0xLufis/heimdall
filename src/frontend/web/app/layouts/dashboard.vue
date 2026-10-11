<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Activity,
  Cpu,
  Layers,
  Wrench,
  Boxes,
  MapPin,
  BarChart3,
  Users,
  ShieldCheck,
  Settings,
  HelpCircle,
  LogOut,
  AlertOctagon,
  Search,
  Bell,
  FolderTree,
  ExternalLink,
  ChevronRight,
  Sliders
} from 'lucide-vue-next'
import { useAuthSession } from '~/composables/useAuthSession'
import DigitalTwinSearchBar from '~/components/twin/DigitalTwinSearchBar.vue'

const { user, signOut } = useAuthSession()
const route = useRoute()
const router = useRouter()

const userEmail = computed(() => user.value?.email ?? 'admin@heimdall.dev')
const userName = computed(() => user.value?.name ?? 'System Administrator')
const userInitials = computed(() => (userName.value || 'A').charAt(0).toUpperCase())
const environment = process.env.NODE_ENV || 'development'

// Fetch quick live twin summary for the top bar
const activeLineStopsCount = ref(0)
const machines = ref<any[]>([])
const tickets = ref<any[]>([])
const controllers = ref<any[]>([])

const fetchTwinStatus = async () => {
  try {
    const [tRes, mRes, cRes] = await Promise.all([
      $fetch<any[]>('/api/tickets').catch(() => []),
      $fetch<any[]>('/api/machines', { query: { includeDecommissioned: true } }).catch(() => []),
      $fetch<any[]>('/api/clients').catch(() => [])
    ])
    tickets.value = Array.isArray(tRes) ? tRes : []
    machines.value = Array.isArray(mRes) ? mRes : []
    controllers.value = Array.isArray(cRes) ? cRes : []

    activeLineStopsCount.value = tickets.value.filter(t => t.isLineStop === true && t.status !== 'Closed' && t.status !== 'Resolved').length
  } catch {}
}

onMounted(() => {
  fetchTwinStatus()
})

const pageTitle = computed(() => {
  if (route.path === '/dashboard' || route.path === '/dashboard/overview') return 'Digital Twin Command Center'
  if (route.path.startsWith('/dashboard/machines')) return 'Connected Machine Fleet Matrix'
  if (route.path.startsWith('/dashboard/machine-groups')) return 'Production Lines & Cells'
  if (route.path.startsWith('/dashboard/telemetry')) return 'Live Telemetry & SPC Streams'
  if (route.path.startsWith('/dashboard/tickets')) return 'Incident Response & Downtime'
  if (route.path.startsWith('/dashboard/inventory')) return 'Component Hierarchy & Parts'
  if (route.path.startsWith('/dashboard/clients')) return 'Edge IPC Controllers & OT Bus'
  if (route.path.startsWith('/dashboard/map')) return 'Spatial Plant CAD Layout'
  if (route.path.startsWith('/dashboard/delegations')) return 'Personnel Delegations & Skills'
  if (route.path.startsWith('/dashboard/analytics')) return 'Plant Analytics & OEE Trends'
  if (route.path.startsWith('/dashboard/security-groups')) return 'OU Governance & Access Policies'
  if (route.path.startsWith('/dashboard/users')) return 'Users & Access Governance'
  if (route.path.startsWith('/dashboard/settings')) return 'Twin Studio & Preferences'
  return 'Digital Twin Command'
})

const handleSignOut = async () => {
  await signOut()
  router.push('/auth/login')
}
</script>

<template>
  <div class="min-h-screen bg-[#0c0e12] flex text-zinc-100 font-sans selection:bg-teal-500 selection:text-black">
    <!-- Sleek Industrial Navigation Rail / Sidebar -->
    <aside class="w-64 bg-[#0e1116] border-r border-[#232730] flex-shrink-0 flex flex-col justify-between z-30 select-none">
      <div>
        <!-- Brand Header -->
        <div class="p-5 border-b border-[#232730] flex items-center justify-between bg-[#11141a]">
          <NuxtLink to="/dashboard" class="flex items-center gap-3 group">
            <div class="w-8 h-8 rounded-lg bg-teal-500/10 border border-teal-500/30 flex items-center justify-center font-mono font-bold text-teal-300 shadow-sm group-hover:bg-teal-500 group-hover:text-black transition-colors">
              H
            </div>
            <div>
              <div class="text-sm font-bold tracking-tight text-white flex items-center gap-1.5">
                <span>Heimdall</span>
                <span class="text-[9px] font-mono px-1 py-0.2 rounded bg-zinc-800 text-teal-400 border border-zinc-700">TWIN</span>
              </div>
              <div class="text-[10px] font-mono text-zinc-500 flex items-center gap-1 mt-0.5">
                <span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                <span>Industrial OS</span>
              </div>
            </div>
          </NuxtLink>
        </div>

        <!-- Navigation Menu -->
        <nav class="p-3 space-y-6 overflow-y-auto max-h-[calc(100vh-160px)]">
          <!-- Section 1: Core Digital Twin -->
          <div>
            <div class="px-3 pb-2 text-[9px] font-mono font-bold uppercase tracking-widest text-zinc-500">
              Digital Twin Core
            </div>
            <div class="space-y-1">
              <NuxtLink
                to="/dashboard"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path === '/dashboard' || route.path === '/dashboard/overview' ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Activity class="w-4 h-4 text-teal-400" />
                <span>Command Center</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/machines"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/machines') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Layers class="w-4 h-4 text-teal-400" />
                <span>Fleet &amp; Machines</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/telemetry"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/telemetry') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Cpu class="w-4 h-4 text-teal-400" />
                <span>Live Streams &amp; SPC</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/map"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/map') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <MapPin class="w-4 h-4 text-teal-400" />
                <span>Plant CAD Spatial Map</span>
              </NuxtLink>
            </div>
          </div>

          <!-- Section 2: Floor Operations & Incident Management -->
          <div>
            <div class="px-3 pb-2 text-[9px] font-mono font-bold uppercase tracking-widest text-zinc-500">
              Operations &amp; Maintenance
            </div>
            <div class="space-y-1">
              <NuxtLink
                to="/dashboard/tickets"
                class="flex items-center justify-between px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/tickets') ? 'bg-rose-500/10 text-rose-300 font-semibold border border-rose-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <div class="flex items-center gap-2.5">
                  <Wrench class="w-4 h-4 text-rose-400" />
                  <span>Incident Response</span>
                </div>
                <span
                  v-if="activeLineStopsCount > 0"
                  class="px-1.5 py-0.2 rounded-full text-[9px] font-mono font-bold bg-rose-500 text-white animate-pulse"
                >
                  {{ activeLineStopsCount }}
                </span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/inventory"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/inventory') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Boxes class="w-4 h-4 text-teal-400" />
                <span>Asset Inventory</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/clients"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/clients') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Cpu class="w-4 h-4 text-sky-400" />
                <span>Edge IPC Controllers</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/delegations"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/delegations') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <ShieldCheck class="w-4 h-4 text-emerald-400" />
                <span>Delegations &amp; Skills</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/analytics"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/analytics') ? 'bg-teal-500/10 text-teal-300 font-semibold border border-teal-500/30 shadow-xs' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <BarChart3 class="w-4 h-4 text-teal-400" />
                <span>Plant Analytics &amp; OEE</span>
              </NuxtLink>
            </div>
          </div>

          <!-- Section 3: Governance & Preferences -->
          <div>
            <div class="px-3 pb-2 text-[9px] font-mono font-bold uppercase tracking-widest text-zinc-500">
              Governance &amp; Studio
            </div>
            <div class="space-y-1">
              <NuxtLink
                to="/dashboard/machine-groups"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/machine-groups') ? 'bg-zinc-800 text-white font-semibold' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <FolderTree class="w-4 h-4 text-zinc-400" />
                <span>Lines &amp; Groups</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/security-groups"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/security-groups') ? 'bg-zinc-800 text-white font-semibold' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <ShieldCheck class="w-4 h-4 text-zinc-400" />
                <span>OU Governance</span>
              </NuxtLink>

              <NuxtLink
                to="/dashboard/settings"
                class="flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-medium transition-all"
                :class="route.path.startsWith('/dashboard/settings') ? 'bg-zinc-800 text-white font-semibold' : 'text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50'"
              >
                <Sliders class="w-4 h-4 text-zinc-400" />
                <span>Twin Studio &amp; Settings</span>
              </NuxtLink>
            </div>
          </div>
        </nav>
      </div>

      <!-- Bottom User Profile Card -->
      <div class="p-4 border-t border-[#232730] bg-[#11141a]">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2.5 truncate">
            <div class="w-8 h-8 rounded-full bg-zinc-800 border border-zinc-700 flex items-center justify-center font-bold text-xs text-teal-300">
              {{ userInitials }}
            </div>
            <div class="truncate">
              <div class="text-xs font-semibold text-zinc-200 truncate">{{ userName }}</div>
              <div class="text-[10px] font-mono text-zinc-500 truncate">{{ userEmail }}</div>
            </div>
          </div>
          <button
            @click="handleSignOut"
            class="p-1.5 rounded-lg text-zinc-500 hover:text-rose-400 hover:bg-zinc-800 transition-colors"
            title="Sign Out"
          >
            <LogOut class="w-4 h-4" />
          </button>
        </div>
      </div>
    </aside>

    <!-- Main Command Viewport -->
    <div class="flex-1 flex flex-col min-w-0 bg-[#0c0e12]">
      <!-- Sticky Top Command Bar -->
      <header class="h-16 bg-[#15181e]/95 backdrop-blur-md border-b border-[#232730] flex items-center justify-between px-6 sticky top-0 z-40">
        <!-- Title & Page Context -->
        <div class="flex items-center gap-3">
          <h1 class="text-sm font-bold text-zinc-100 tracking-tight">{{ pageTitle }}</h1>
          <span class="text-xs font-mono text-zinc-600 hidden md:inline">|</span>
          <div class="hidden md:flex items-center gap-1.5 text-[11px] font-mono text-zinc-400">
            <span class="inline-block w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
            <span>Digital Twin Live</span>
          </div>
        </div>

        <!-- Top Right Actions & Quick Omnisearch -->
        <div class="flex items-center gap-3">
          <!-- Active Line Stop Alert Button (if any) -->
          <NuxtLink
            v-if="activeLineStopsCount > 0"
            to="/dashboard/tickets"
            class="hidden sm:flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-rose-500/10 border border-rose-500/40 text-rose-300 text-xs font-mono font-bold animate-pulse hover:bg-rose-500/20 transition-colors"
          >
            <AlertOctagon class="w-3.5 h-3.5 text-rose-400" />
            <span>{{ activeLineStopsCount }} LINE STOP ACTIVE</span>
          </NuxtLink>

          <!-- Omnipresent Digital Twin Search Trigger -->
          <DigitalTwinSearchBar
            :machines="machines"
            :tickets="tickets"
            :controllers="controllers"
            @select-machine="id => router.push(`/dashboard/machines`)"
          />

          <!-- Notification Bell -->
          <NuxtLink
            to="/dashboard/tickets"
            class="p-2 rounded-xl bg-[#0c0e12] border border-[#232730] text-zinc-400 hover:text-white transition-colors relative"
            title="System Alerts"
          >
            <Bell class="w-4 h-4" />
            <span
              v-if="activeLineStopsCount > 0"
              class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-rose-500"
            ></span>
          </NuxtLink>

          <!-- Environment Badge -->
          <div class="text-[10px] font-mono font-bold text-zinc-400 bg-[#0c0e12] px-2.5 py-1 rounded-lg border border-[#232730] uppercase tracking-wider hidden lg:block">
            {{ environment }}
          </div>
        </div>
      </header>

      <!-- Page Content Viewport -->
      <div class="flex-1 overflow-y-auto">
        <slot />
      </div>
    </div>
  </div>
</template>
