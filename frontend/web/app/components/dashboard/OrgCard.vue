<script setup lang="ts">
import { Card, CardContent, CardFooter, CardHeader } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'
import { Edit2, Users, ShieldCheck, Trash2, Building2 } from 'lucide-vue-next'

defineProps<{
  org: any
}>()

defineEmits(['manage-members', 'edit', 'delete'])
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm hover:border-slate-700 transition-all overflow-hidden group font-sans">
    <CardHeader class="p-5 pb-3">
      <div class="flex items-start justify-between mb-3">
        <div class="size-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center text-base font-bold shadow-sm">
          {{ org.name.charAt(0).toUpperCase() }}
        </div>
        <div class="flex gap-1">
          <Button
            variant="ghost"
            size="icon"
            class="size-8 text-slate-400 hover:text-indigo-400 hover:bg-slate-800 rounded-lg transition-colors"
            @click="$emit('edit', org)"
            title="Edit Organization"
          >
            <Edit2 class="size-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="size-8 text-slate-400 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
            @click="$emit('delete', org)"
            title="Delete Organization"
          >
            <Trash2 class="size-3.5" />
          </Button>
        </div>
      </div>
      <h5 class="text-base font-semibold text-slate-100 group-hover:text-indigo-300 transition-colors truncate">{{ org.name }}</h5>
      <div class="flex items-center gap-2 mt-1">
        <div class="text-xs bg-slate-950 text-slate-400 px-2 py-0.5 rounded font-mono truncate max-w-[160px] border border-slate-800">
          {{ org.slug }}
        </div>
      </div>
    </CardHeader>

    <CardContent class="px-5 pb-4">
      <div class="flex items-center justify-between border-t border-slate-800/80 pt-3 text-xs">
        <div class="flex items-center gap-1.5 text-slate-400">
          <Building2 class="size-3.5 text-slate-500" />
          <span>Plant Organization</span>
        </div>
        <div class="flex items-center gap-1 text-emerald-400 font-medium">
          <ShieldCheck class="size-3.5" />
          <span>Active Site</span>
        </div>
      </div>
    </CardContent>

    <CardFooter class="px-5 py-3 bg-slate-950/40 flex items-center justify-between border-t border-slate-800">
      <Button
        variant="link"
        class="h-auto p-0 text-xs font-medium text-indigo-400 hover:text-indigo-300 no-underline transition-colors"
        @click="$emit('manage-members', org)"
      >
        Manage Members
      </Button>
      <div class="flex items-center gap-1.5 text-slate-400 text-xs">
        <Users class="size-3.5 text-slate-500" />
        <span>
          {{ org.memberCount ?? 0 }} {{ org.memberCount === 1 ? 'Member' : 'Members' }}
        </span>
      </div>
    </CardFooter>
  </Card>
</template>
