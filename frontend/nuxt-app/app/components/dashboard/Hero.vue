<script setup lang="ts">
defineProps<{
  userName?: string
  userRole?: string
}>()
</script>

<template>
  <div class="bg-indigo-900 rounded-3xl p-8 text-white shadow-2xl relative overflow-hidden">
    <div class="relative z-10">
      <div class="flex items-center gap-6">
        <div class="w-20 h-20 rounded-2xl bg-white/10 backdrop-blur-md flex items-center justify-center text-4xl font-black border border-white/20 shadow-inner group transition-all hover:bg-white/20">
          {{ userName?.charAt(0) }}
        </div>
        <div>
          <h3 class="text-4xl font-black tracking-tight leading-none mb-2">Welcome back, {{ userName }}</h3>
          <div class="flex items-center gap-3">
            <DashboardRoleBadge :role="userRole" />
            <div class="flex items-center gap-2 px-3 py-1 rounded-full bg-white/5 border border-white/10 backdrop-blur-sm">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              <span class="text-indigo-200 text-[10px] font-black uppercase tracking-widest opacity-80">System Oversight Active</span>
            </div>
          </div>
        </div>
      </div>
    </div>
    <!-- Abstract Background Shapes -->
    <div class="absolute -right-20 -top-20 w-80 h-80 bg-indigo-500 rounded-full blur-[100px] opacity-30 animate-pulse"></div>
    <div class="absolute right-40 bottom-0 w-64 h-64 bg-purple-500 rounded-full blur-[80px] opacity-20 animate-pulse" style="animation-delay: 1s"></div>
    <div class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full bg-gradient-to-br from-transparent via-indigo-800/20 to-transparent pointer-events-none"></div>
  </div>
</template>
