<script setup lang="ts">
import { Avatar, AvatarFallback, AvatarImage } from '~/components/ui/avatar'

defineProps<{
  name: string
  email: string
  image?: string | null
}>()
</script>

<template>
  <div class="flex items-center gap-3 group">
    <Avatar class="h-10 w-10 border border-slate-800 shadow-sm group-hover:scale-110 transition-transform duration-300">
      <AvatarImage v-if="image" :src="image" :alt="name" />
      <AvatarFallback class="bg-gradient-to-br from-indigo-500 to-purple-600 text-white font-black">
        {{ name.charAt(0).toUpperCase() }}
      </AvatarFallback>
    </Avatar>
    <div class="min-w-0">
      <div class="font-bold text-slate-200 group-hover:text-indigo-400 transition-colors truncate">{{ name }}</div>
      <div class="text-[10px] text-slate-500 font-medium truncate flex items-center gap-1">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 opacity-60" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.206" /></svg>
        {{ email }}
      </div>
    </div>
  </div>
</template>
