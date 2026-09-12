<script setup lang="ts">
import { Card, CardContent } from '~/components/ui/card'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'

defineProps<{
  userName?: string
  userRole?: string
}>()
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 p-6 shadow-sm rounded-xl">
    <CardContent class="p-0">
      <div class="flex items-center gap-5">
        <Avatar class="w-14 h-14 border border-slate-800 bg-slate-950">
          <AvatarFallback class="text-2xl font-bold text-slate-100 bg-transparent">
            {{ userName?.charAt(0) || 'U' }}
          </AvatarFallback>
        </Avatar>
        <div>
          <h3 class="text-2xl font-bold tracking-tight text-slate-100">Welcome back, {{ userName }}</h3>
          <div class="flex items-center gap-3 mt-1.5">
            <DashboardRoleBadge :role="userRole" />
            <div class="flex items-center gap-2 px-2.5 py-0.5 rounded-md border border-emerald-500/30 bg-emerald-500/10 text-xs font-mono text-emerald-300">
              <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span class="font-medium text-xs">System Nominal</span>
            </div>
          </div>
        </div>
      </div>
    </CardContent>
  </Card>
</template>

