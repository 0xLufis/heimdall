<script setup lang="ts">
import { Card, CardContent } from '~/components/ui/card'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'

defineProps<{
  userName?: string
  userRole?: string
}>()
</script>

<template>
  <Card class="bg-card border-border p-6 text-card-foreground shadow-sm rounded-xl">
    <CardContent class="p-0">
      <div class="flex items-center gap-5">
        <Avatar class="w-14 h-14 border border-border bg-muted">
          <AvatarFallback class="text-2xl font-bold text-foreground bg-transparent">
            {{ userName?.charAt(0) || 'U' }}
          </AvatarFallback>
        </Avatar>
        <div>
          <h3 class="text-2xl font-bold tracking-tight text-foreground">Welcome back, {{ userName }}</h3>
          <div class="flex items-center gap-3 mt-1">
            <DashboardRoleBadge :role="userRole" />
            <div class="flex items-center gap-2 px-2.5 py-0.5 rounded border border-border bg-muted/50 text-xs font-mono">
              <span class="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span class="text-muted-foreground uppercase tracking-wider text-[11px]">System Nominal</span>
            </div>
          </div>
        </div>
      </div>
    </CardContent>
  </Card>
</template>
