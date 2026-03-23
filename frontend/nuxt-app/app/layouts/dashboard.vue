<template>
  <div class="min-h-screen bg-slate-950 flex">
    <!-- Sidebar -->
    <aside class="w-64 bg-slate-950 text-slate-400 flex-shrink-0 flex flex-col border-r border-slate-900">
      <div class="p-6">
        <div class="flex items-center gap-2 text-white">
          <div class="w-8 h-8 bg-slate-800 rounded-lg flex items-center justify-center text-slate-100 font-bold shadow-md ring-1 ring-white/10">H</div>
          <span class="text-xl font-bold tracking-tight text-slate-100">Heimdall</span>
        </div>
      </div>
      
      <nav class="flex-grow px-4 space-y-1 mt-4">
        <NuxtLink 
          to="/dashboard" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path === '/dashboard' ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
          </svg>
          Dashboard
        </NuxtLink>
        <NuxtLink 
          to="/dashboard/clients" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path.startsWith('/dashboard/clients') ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
          </svg>
          Client PCs
        </NuxtLink>
        <NuxtLink 
          to="/dashboard/inventory" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path.startsWith('/dashboard/inventory') ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
          </svg>
          Inventory
        </NuxtLink>
        
        <NuxtLink 
          to="/dashboard/map" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path.startsWith('/dashboard/map') ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
          </svg>
          Plant Map
        </NuxtLink>
        
        <div class="pt-4 pb-2 px-3 text-[10px] font-bold uppercase tracking-wider text-slate-600 select-none">Administration</div>
        
        <NuxtLink 
          to="/dashboard/users" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path.includes('/users') ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
          </svg>
          Users & Roles
        </NuxtLink>

        <NuxtLink 
          to="/dashboard/organizations" 
          class="flex items-center gap-3 px-3 py-2 rounded-lg transition-all"
          :class="route.path.startsWith('/dashboard/organizations') ? 'bg-white/5 text-white shadow-sm ring-1 ring-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-70" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
          </svg>
          Organizations
        </NuxtLink>
      </nav>

      <div class="p-4 border-t border-slate-900 bg-slate-950">
        <div class="flex items-center gap-3 px-3 py-2">
          <div class="w-8 h-8 rounded-full bg-slate-800 border border-white/5 flex items-center justify-center font-bold shadow-inner text-white text-xs">{{ userInitials }}</div>
          <div class="flex-grow truncate">
            <div class="text-sm font-medium text-slate-200">{{ userName }}</div>
            <div class="text-xs text-slate-500 truncate">{{ userEmail }}</div>
          </div>
        </div>
        <button @click="handleSignOut" class="w-full mt-2 flex items-center gap-3 px-3 py-2 rounded-lg text-slate-500 hover:bg-white/5 hover:text-white transition-all">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 opacity-60" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
          <span class="text-[10px] font-bold uppercase tracking-widest">Sign Out</span>
        </button>
      </div>
    </aside>

    <!-- Main Content -->
    <main class="flex-grow flex flex-col min-w-0 bg-slate-950">
      <header class="h-16 bg-slate-900 border-b border-slate-800 flex items-center justify-between px-8 sticky top-0 z-10">
        <h2 class="text-lg font-bold text-slate-100 tracking-tight">{{ pageTitle }}</h2>
        <div class="flex items-center gap-4">
           <div class="h-8 w-px bg-slate-800"></div>
           <div class="text-[10px] font-bold text-slate-400 bg-slate-950 px-3 py-1.5 rounded-full border border-slate-800 uppercase tracking-widest">
             Env: <span class="text-slate-200">{{ environment }}</span>
           </div>
        </div>
      </header>
      <div class="p-8 overflow-y-auto max-h-[calc(100vh-64px)]">
        <slot />
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { authClient } from "~/utils/auth-client"

const session = authClient.useSession()
const route = useRoute()

const userEmail = computed(() => session.data?.value?.user?.email ?? '...')
const userName = computed(() => session.data?.value?.user?.name ?? 'User')
const userInitials = computed(() => userName.value.charAt(0).toUpperCase())
const environment = process.env.NODE_ENV || 'development'

const pageTitle = computed(() => {
  if (route.path.includes('/users')) return 'Users'
  if (route.path.includes('/organizations')) return 'Organizations'
  if (route.path.includes('/clients')) return 'Client PCs'
  if (route.path.includes('/inventory')) return 'Inventory'
  return 'Dashboard'
})

async function handleSignOut() {
  await authClient.signOut()
  navigateTo('/auth/login')
}
</script>
