<script setup lang="ts">
import { Card, CardContent, CardFooter, CardHeader } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'

defineProps<{
  org: any
}>()

defineEmits(['manage-members'])
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 shadow-sm hover:shadow-xl hover:-translate-y-1 transition-all duration-300 overflow-hidden group font-sans">
    <CardHeader class="p-6 pb-4">
      <div class="flex items-start justify-between mb-4">
        <Avatar shape="square" class="w-12 h-12 border-0">
          <AvatarFallback class="bg-gradient-to-br from-indigo-500 to-indigo-700 text-xl font-black text-white shadow-lg">
            {{ org.name.charAt(0).toUpperCase() }}
          </AvatarFallback>
        </Avatar>
        <div class="flex gap-1">
          <Button variant="ghost" size="icon" class="h-8 w-8 text-slate-500 hover:text-indigo-400">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
            </svg>
          </Button>
        </div>
      </div>
      <h5 class="text-lg font-black text-slate-100 group-hover:text-indigo-400 transition-colors truncate">{{ org.name }}</h5>
      <p class="text-xs text-slate-500 font-mono mt-1">ID: {{ org.id }}</p>
    </CardHeader>
    
    <CardContent class="px-6 pb-6">
      <div class="mt-2 flex items-center gap-4 border-t border-slate-800 pt-4">
        <div class="flex -space-x-2">
          <Avatar v-for="i in 3" :key="i" class="w-6 h-6 border-2 border-slate-900">
            <AvatarFallback class="bg-slate-800 text-[8px] font-bold text-slate-500">U</AvatarFallback>
          </Avatar>
        </div>
        <span class="text-[10px] font-bold text-slate-500 uppercase tracking-widest">Active Members</span>
      </div>
    </CardContent>

    <CardFooter class="px-6 py-4 bg-slate-950/50 flex items-center justify-between border-t border-slate-800">
      <Button 
        variant="link" 
        class="h-auto p-0 text-xs font-black text-indigo-400 uppercase tracking-widest hover:text-indigo-300 no-underline"
        @click="$emit('manage-members', org.id)"
      >
        Manage Members
      </Button>
      <div class="text-[10px] bg-indigo-950 text-indigo-400 border border-indigo-900/50 px-2 py-0.5 rounded font-black truncate max-w-[100px]">
        {{ org.slug }}
      </div>
    </CardFooter>
  </Card>
</template>
