<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '~/components/ui/card'
import { Button } from '~/components/ui/button'

interface SecurityEvent {
  title: string
  description: string
  time: string
  severity?: 'low' | 'medium' | 'high'
}

defineProps<{
  events: SecurityEvent[]
}>()
</script>

<template>
  <Card class="bg-card border-border p-6 shadow-sm rounded-xl">
    <CardHeader class="p-0 mb-6 flex flex-row items-center justify-between space-y-0">
      <CardTitle class="text-sm font-bold text-foreground uppercase tracking-wider flex items-center gap-2">
        <span class="w-2 h-2 bg-primary rounded-full"></span>
        Audit & Security Events
      </CardTitle>
    </CardHeader>
    
    <CardContent class="p-0">
      <div class="space-y-6 relative before:absolute before:left-[7px] before:top-2 before:bottom-2 before:w-[2px] before:bg-border">
        <div v-for="(event, index) in events" :key="index" class="flex gap-4 relative group">
          <div class="w-3.5 h-3.5 rounded-full bg-background border-2 border-border group-hover:border-primary transition-colors z-10 flex-shrink-0 mt-1"></div>
          <div>
            <p class="text-xs font-bold text-foreground">{{ event.title }}</p>
            <p class="text-xs text-muted-foreground mt-0.5 leading-relaxed">{{ event.description }}</p>
            <div class="flex items-center gap-2 mt-2">
              <span class="text-[10px] text-muted-foreground font-mono uppercase">{{ event.time }}</span>
              <span v-if="event.severity === 'high'" class="px-1.5 py-0.5 rounded text-[8px] font-mono uppercase bg-destructive/10 text-destructive border border-destructive/20">Critical</span>
            </div>
          </div>
        </div>
      </div>

      <Button variant="outline" class="w-full mt-6 h-10 border border-dashed border-border bg-transparent rounded-lg text-xs font-semibold text-muted-foreground uppercase tracking-wider hover:border-primary hover:text-foreground transition-colors flex items-center justify-center gap-2">
        Full Audit Logs
      </Button>
    </CardContent>
  </Card>
</template>
