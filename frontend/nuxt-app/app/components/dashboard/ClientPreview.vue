<script setup lang="ts">
interface Client {
  id: string
  hostname: string
  os: string
  lastSeen: string
}

defineProps<{
  clients: Client[]
}>()
</script>

<template>
  <div class="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden">
    <div class="px-8 py-6 border-b border-gray-50 flex justify-between items-center bg-gray-50/30">
      <h3 class="text-lg font-black text-gray-900 uppercase tracking-tight flex items-center gap-2">
        <span class="w-2 h-4 bg-indigo-500 rounded-full"></span>
        Active Clients
      </h3>
      <NuxtLink 
        to="/dashboard/clients" 
        class="group flex items-center gap-1.5 px-4 py-1.5 rounded-full bg-indigo-50 text-[10px] font-black text-indigo-600 hover:bg-indigo-600 hover:text-white transition-all uppercase tracking-widest"
      >
        View All
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 transition-transform group-hover:translate-x-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M9 5l7 7-7 7" />
        </svg>
      </NuxtLink>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full text-left">
        <thead class="bg-gray-50 text-[10px] text-gray-400 uppercase tracking-widest font-black">
          <tr>
            <th class="px-8 py-4">Endpoint</th>
            <th class="px-8 py-4">Status</th>
            <th class="px-8 py-4 text-right">Uptime</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-50">
          <tr v-for="client in clients" :key="client.id" class="hover:bg-indigo-50/20 transition-colors group">
            <td class="px-8 py-5">
              <div class="font-bold text-gray-900 group-hover:text-indigo-600 transition-colors">{{ client.hostname }}</div>
              <div class="text-[10px] text-gray-400 font-mono flex items-center gap-1 mt-0.5">
                <span class="opacity-50 tracking-tighter">{{ client.id }}</span>
                <span class="w-1 h-1 rounded-full bg-gray-200"></span>
                <span class="font-bold text-gray-500">{{ client.os }}</span>
              </div>
            </td>
            <td class="px-8 py-5">
              <span class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-[10px] font-black uppercase tracking-wider bg-emerald-50 text-emerald-600 border border-emerald-100 shadow-sm">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                Operational
              </span>
            </td>
            <td class="px-8 py-5 text-right">
              <div class="text-sm font-black text-gray-900">{{ client.lastSeen }}</div>
              <div class="text-[10px] text-gray-400 font-bold uppercase">Signal Stable</div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
