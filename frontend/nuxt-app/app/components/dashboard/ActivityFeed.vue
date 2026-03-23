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
  <Card class="bg-slate-900 border-slate-800 p-8 shadow-sm rounded-3xl">
    <CardHeader class="p-0 mb-8 flex flex-row items-center justify-between space-y-0">
      <CardTitle class="text-sm font-black text-slate-100 uppercase tracking-tight flex items-center gap-2">
        <span class="w-2 h-4 bg-slate-700 rounded-full"></span>
        Security Events
      </CardTitle>
      <div class="flex gap-1">
        <div v-for="i in 3" :key="i" class="w-1.5 h-1.5 rounded-full bg-slate-800"></div>
      </div>
    </CardHeader>
    
    <CardContent class="p-0">
      <div class="space-y-10 relative before:absolute before:left-[7px] before:top-2 before:bottom-2 before:w-[2px] before:bg-slate-800">
        <div v-for="(event, index) in events" :key="index" class="flex gap-6 relative group">
          <div class="w-4 h-4 rounded-full bg-slate-900 border-2 border-slate-700 group-hover:border-slate-500 group-hover:scale-125 transition-all z-10 flex-shrink-0 mt-1.5"></div>
          <div>
            <p class="text-sm font-black text-slate-200 group-hover:text-slate-100 transition-colors">{{ event.title }}</p>
            <p class="text-xs text-slate-500 mt-1 leading-relaxed">{{ event.description }}</p>
            <div class="flex items-center gap-2 mt-2.5">
              <span class="text-[10px] text-slate-600 font-black uppercase tracking-widest">{{ event.time }}</span>
              <span v-if="event.severity === 'high'" class="w-1 h-1 rounded-full bg-rose-500"></span>
              <span v-if="event.severity === 'high'" class="text-[8px] text-rose-600 font-black uppercase tracking-tighter">Priority</span>
            </div>
          </div>
        </div>
      </div>

      <Button variant="outline" class="w-full mt-12 h-14 border-2 border-dashed border-slate-800 bg-transparent rounded-2xl text-[10px] font-black text-slate-600 uppercase tracking-widest hover:border-slate-700 hover:bg-slate-800/30 hover:text-slate-400 transition-all flex items-center justify-center gap-2 group">
        Audit Logs
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 transition-transform group-hover:rotate-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
        </svg>
      </Button>
    </CardContent>
  </Card>
</template>
