<script setup lang="ts">
import { Card, CardContent, CardFooter, CardHeader } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'
import { Edit2Icon, UsersIcon, ShieldCheckIcon } from 'lucide-vue-next'

defineProps<{
  org: any
}>()

defineEmits(['manage-members', 'edit'])
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
          <Button 
            variant="ghost" 
            size="icon" 
            class="h-8 w-8 text-slate-500 hover:text-indigo-400 hover:bg-slate-800 rounded-lg transition-colors"
            @click="$emit('edit', org)"
          >
            <Edit2Icon class="h-4 w-4" />
          </Button>
        </div>
      </div>
      <h5 class="text-lg font-black text-slate-100 group-hover:text-indigo-400 transition-colors truncate tracking-tight">{{ org.name }}</h5>
      <div class="flex items-center gap-2 mt-1">
        <div class="text-[10px] bg-slate-800 text-slate-400 px-2 py-0.5 rounded font-mono truncate max-w-[150px] border border-slate-700/50">
          {{ org.slug }}
        </div>
      </div>
    </CardHeader>
    
    <CardContent class="px-6 pb-6">
      <div class="mt-2 flex items-center justify-between border-t border-slate-800/50 pt-4">
        <div class="flex items-center gap-2">
            <UsersIcon class="h-3 w-3 text-slate-500" />
            <span class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Team Unit</span>
        </div>
        <div class="flex items-center gap-1 text-emerald-500/80">
            <ShieldCheckIcon class="h-3 w-3" />
            <span class="text-[9px] font-black uppercase tracking-tighter">Active System</span>
        </div>
      </div>
    </CardContent>

    <CardFooter class="px-6 py-4 bg-slate-950/30 flex items-center justify-between border-t border-slate-800">
      <Button 
        variant="link" 
        class="h-auto p-0 text-[10px] font-black text-indigo-400 uppercase tracking-[0.2em] hover:text-indigo-300 no-underline transition-colors"
        @click="$emit('manage-members', org)"
      >
        Manage Personnel
      </Button>
      <div class="flex -space-x-2">
          <div v-for="i in 3" :key="i" class="w-6 h-6 rounded-full border-2 border-slate-900 bg-slate-800 flex items-center justify-center text-[8px] font-black text-slate-500 uppercase">
            {{ String.fromCharCode(64 + i) }}
          </div>
      </div>
    </CardFooter>
  </Card>
</template>
