<script setup lang="ts">
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
  <div class="bg-white rounded-3xl shadow-sm border border-gray-100 p-8">
    <div class="flex items-center justify-between mb-8">
      <h3 class="text-lg font-black text-gray-900 uppercase tracking-tight flex items-center gap-2">
        <span class="w-2 h-4 bg-purple-500 rounded-full"></span>
        Security Events
      </h3>
      <div class="flex gap-1">
        <div v-for="i in 3" :key="i" class="w-1.5 h-1.5 rounded-full bg-gray-100"></div>
      </div>
    </div>
    
    <div class="space-y-10 relative before:absolute before:left-[7px] before:top-2 before:bottom-2 before:w-[2px] before:bg-indigo-50">
      <div v-for="(event, index) in events" :key="index" class="flex gap-6 relative group">
        <div class="w-4 h-4 rounded-full bg-white border-2 border-indigo-500 group-hover:border-indigo-600 group-hover:scale-125 transition-all z-10 flex-shrink-0 mt-1.5"></div>
        <div>
          <p class="text-sm font-black text-gray-900 group-hover:text-indigo-600 transition-colors">{{ event.title }}</p>
          <p class="text-xs text-gray-500 mt-1 leading-relaxed">{{ event.description }}</p>
          <div class="flex items-center gap-2 mt-2.5">
            <span class="text-[10px] text-gray-400 font-black uppercase tracking-widest">{{ event.time }}</span>
            <span v-if="event.severity === 'high'" class="w-1 h-1 rounded-full bg-rose-500"></span>
            <span v-if="event.severity === 'high'" class="text-[8px] text-rose-600 font-black uppercase tracking-tighter">Priority</span>
          </div>
        </div>
      </div>
    </div>

    <button class="w-full mt-12 py-4 border-2 border-dashed border-gray-100 rounded-2xl text-xs font-black text-gray-400 uppercase tracking-widest hover:border-indigo-200 hover:bg-indigo-50/30 hover:text-indigo-600 transition-all flex items-center justify-center gap-2 group">
      Audit Logs
      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 transition-transform group-hover:rotate-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
      </svg>
    </button>
  </div>
</template>
