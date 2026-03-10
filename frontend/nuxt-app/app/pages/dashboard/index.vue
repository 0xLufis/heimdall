<template>
  <div class="space-y-8 font-sans">
    <!-- Hero Section -->
    <div class="bg-indigo-900 rounded-3xl p-8 text-white shadow-2xl relative overflow-hidden">
        <div class="relative z-10">
            <div class="flex items-center gap-4">
                <div class="w-16 h-16 rounded-2xl bg-white/10 backdrop-blur-md flex items-center justify-center text-3xl font-black border border-white/20 shadow-inner">
                    {{ session?.user.name.charAt(0) }}
                </div>
                <div>
                    <h3 class="text-3xl font-black tracking-tight">Welcome back, {{ session?.user.name }}</h3>
                    <div class="flex items-center gap-2 mt-1">
                        <DashboardRoleBadge :role="session?.user.role" />
                        <span class="text-indigo-300 text-xs font-bold uppercase tracking-widest opacity-70">System Oversight Active</span>
                    </div>
                </div>
            </div>
        </div>
        <!-- Abstract Background Shapes -->
        <div class="absolute -right-20 -top-20 w-64 h-64 bg-indigo-500 rounded-full blur-3xl opacity-20"></div>
        <div class="absolute right-40 bottom-0 w-48 h-48 bg-purple-500 rounded-full blur-3xl opacity-20"></div>
    </div>

    <!-- Stats Cards Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <DashboardStatCard title="Total Users" value="1,284" bgColor="bg-indigo-600" trend="+12%">
        <template #icon>
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" /></svg>
        </template>
      </DashboardStatCard>
      
      <DashboardStatCard title="Active Clients" value="842" bgColor="bg-emerald-600" trend="+5%">
        <template #icon>
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
        </template>
      </DashboardStatCard>

      <DashboardStatCard title="Pending Alerts" value="12" bgColor="bg-rose-600" trend="-2%">
        <template #icon>
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
        </template>
      </DashboardStatCard>

      <DashboardStatCard title="Avg. Uptime" value="99.9%" bgColor="bg-amber-600" trend="Stable">
        <template #icon>
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
        </template>
      </DashboardStatCard>
    </div>

    <!-- Main Content Grid -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <!-- Client Preview Table -->
        <div class="lg:col-span-2 bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden">
            <div class="px-8 py-6 border-b border-gray-50 flex justify-between items-center bg-gray-50/30">
                <h3 class="text-lg font-black text-gray-900 uppercase tracking-tight">Active Clients</h3>
                <NuxtLink to="/dashboard/clients" class="text-xs font-black text-indigo-600 hover:text-indigo-800 uppercase tracking-widest">View All</NuxtLink>
            </div>
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead class="bg-gray-50 text-[10px] text-gray-400 uppercase tracking-widest font-black">
                        <tr>
                            <th class="px-8 py-4">Client ID</th>
                            <th class="px-8 py-4">Status</th>
                            <th class="px-8 py-4 text-right">Uptime</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-50">
                        <tr v-for="client in recentClients" :key="client.id" class="hover:bg-indigo-50/20 transition-colors">
                            <td class="px-8 py-4">
                                <div class="font-bold text-gray-900">{{ client.hostname }}</div>
                                <div class="text-[10px] text-gray-400 font-mono">{{ client.id }}</div>
                            </td>
                            <td class="px-8 py-4">
                                <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[10px] font-black uppercase tracking-wider bg-emerald-50 text-emerald-600 border border-emerald-100">
                                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                                    Operational
                                </span>
                            </td>
                            <td class="px-8 py-4 text-right">
                                <div class="text-sm font-bold text-gray-900">{{ client.lastSeen }}</div>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>

        <!-- Recent Activity Feed (Clerk Style) -->
        <div class="bg-white rounded-3xl shadow-sm border border-gray-100 p-8">
            <h3 class="text-lg font-black text-gray-900 uppercase tracking-tight mb-6">Security Events</h3>
            <div class="space-y-6">
                <div v-for="i in 4" :key="i" class="flex gap-4">
                    <div class="w-2 h-2 rounded-full bg-indigo-500 mt-2 flex-shrink-0"></div>
                    <div>
                        <p class="text-sm font-bold text-gray-900">New login detected</p>
                        <p class="text-xs text-gray-500 mt-0.5 leading-relaxed">Admin logged in from a new Windows device in Budapest, HU.</p>
                        <p class="text-[10px] text-gray-400 font-bold uppercase mt-2">24 minutes ago</p>
                    </div>
                </div>
            </div>
            <button class="w-full mt-8 py-3 border-2 border-dashed border-gray-100 rounded-2xl text-xs font-black text-gray-400 uppercase tracking-widest hover:border-indigo-200 hover:text-indigo-500 transition-all">
                Audit Logs
            </button>
        </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"
const { data: session } = authClient.useSession()

definePageMeta({
  layout: 'dashboard'
})

const recentClients = [
  { id: 'PC-10293', hostname: 'LINE-A-OP1', os: 'Windows 10 Pro (22H2)', lastSeen: '2 mins' },
  { id: 'PC-10294', hostname: 'LINE-A-OP2', os: 'Windows 10 Pro (22H2)', lastSeen: '5 mins' },
  { id: 'PC-10295', hostname: 'LINE-B-CTRL', os: 'Ubuntu 22.04 LTS', lastSeen: '12 mins' },
]
</script>
