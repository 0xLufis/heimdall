<template>
  <div class="flex items-center gap-3 group">
    <div 
      class="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center text-white font-black shadow-sm group-hover:scale-110 transition-transform duration-300"
    >
      {{ name.charAt(0).toUpperCase() }}
    </div>
    <div class="min-w-0">
      <div class="font-bold text-gray-900 group-hover:text-indigo-600 transition-colors truncate">{{ name }}</div>
      <div class="text-[10px] text-gray-400 font-medium truncate flex items-center gap-1">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 opacity-60" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.206" /></svg>
        {{ email }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  name: string
  email: string
  image?: string | null
}>()
</script>
