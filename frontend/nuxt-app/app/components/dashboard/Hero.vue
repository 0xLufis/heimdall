<script setup lang="ts">
import { Card, CardContent } from '~/components/ui/card'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'

defineProps<{
  userName?: string
  userRole?: string
}>()
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 p-8 text-white shadow-2xl relative overflow-hidden rounded-3xl">
    <CardContent class="p-0 relative z-10">
      <div class="flex items-center gap-6">
        <Avatar class="w-20 h-20 border border-white/10 shadow-inner group transition-all hover:bg-white/10 bg-white/5 backdrop-blur-md">
          <AvatarFallback class="text-4xl font-black bg-transparent">
            {{ userName?.charAt(0) }}
          </AvatarFallback>
        </Avatar>
        <div>
          <h3 class="text-4xl font-black tracking-tight leading-none mb-2 text-slate-100">Welcome back, {{ userName }}</h3>
          <div class="flex items-center gap-3">
            <DashboardRoleBadge :role="userRole" />
            <div class="flex items-center gap-2 px-3 py-1 rounded-full bg-white/5 border border-white/10 backdrop-blur-sm">
              <span class="w-1.5 h-1.5 rounded-full bg-slate-400 animate-pulse"></span>
              <span class="text-slate-400 text-[10px] font-black uppercase tracking-widest opacity-80">System Oversight Active</span>
            </div>
          </div>
        </div>
      </div>
    </CardContent>
    <!-- Abstract Background Shapes -->
    <div class="absolute -right-20 -top-20 w-80 h-80 bg-slate-800 rounded-full blur-[100px] opacity-30"></div>
    <div class="absolute right-40 bottom-0 w-64 h-64 bg-slate-900 rounded-full blur-[80px] opacity-20" style="animation-delay: 1s"></div>
    <div class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-full h-full bg-gradient-to-br from-transparent via-slate-800/20 to-transparent pointer-events-none"></div>
  </Card>
</template>
