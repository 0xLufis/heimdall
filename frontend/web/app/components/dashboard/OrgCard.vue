<script setup lang="ts">
import { Card, CardContent, CardFooter, CardHeader } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import RbacButton from '~/components/common/RbacButton.vue'
import { Avatar, AvatarFallback } from '~/components/ui/avatar'
import { Edit2, Users, ShieldCheck, Trash2, Building2 } from 'lucide-vue-next'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'

defineProps<{
  org: any
}>()

defineEmits(['manage-members', 'edit', 'delete'])
const { canAdministerSystem } = useRbacPermission()
</script>

<template>
  <Card class="bg-slate-900 border-slate-800 rounded-xl shadow-sm hover:border-slate-700 transition-all overflow-hidden group font-sans">
    <CardHeader class="p-5 pb-3">
      <div class="flex items-start justify-between mb-3">
        <div class="size-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 flex items-center justify-center text-base font-bold shadow-sm">
          {{ org.name.charAt(0).toUpperCase() }}
        </div>
        <div class="flex gap-1">
          <RbacButton
            capability="canAdministerSystem"
            variant="ghost"
            size="icon"
            class="size-8 text-slate-400 hover:text-indigo-400 hover:bg-slate-800 rounded-lg transition-colors"
            @click="$emit('edit', org)"
            title="Edit Organization"
          >
            <Edit2 class="size-3.5" />
          </RbacButton>
          <RbacButton
            capability="canAdministerSystem"
            variant="ghost"
            size="icon"
            class="size-8 text-slate-400 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
            @click="$emit('delete', org)"
            title="Delete Organization"
          >
            <Trash2 class="size-3.5" />
          </RbacButton>
        </div>
      </div>
      <h5 class="text-base font-semibold text-slate-100 group-hover:text-indigo-300 transition-colors truncate">{{ org.name }}</h5>
      <div class="flex flex-wrap items-center gap-1.5 mt-1">
        <div class="text-xs bg-slate-950 text-slate-400 px-2 py-0.5 rounded font-mono truncate max-w-[160px] border border-slate-800">
          {{ org.slug }}
        </div>
        <div v-if="org.ouPath" class="text-[10px] bg-cyan-950/50 text-cyan-400 border border-cyan-800/60 px-1.5 py-0.5 rounded font-mono truncate max-w-[140px]" :title="org.ouPath">
          {{ org.ouPath.split(',')[0].replace('OU=', '') }}
        </div>
        <div v-if="org.vlanName" class="text-[10px] bg-indigo-950/50 text-indigo-400 border border-indigo-800/60 px-1.5 py-0.5 rounded font-mono">
          VLAN {{ org.vlanId }}
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
      <RbacButton
        capability="canAdministerSystem"
        variant="link"
        class="h-auto p-0 text-xs font-medium text-indigo-400 hover:text-indigo-300 no-underline transition-colors"
        @click="$emit('manage-members', org)"
      >
        Manage Members
      </RbacButton>
      <div class="flex items-center gap-1.5 text-slate-400 text-xs">
        <Users class="size-3.5 text-slate-500" />
        <span>
          {{ org.memberCount ?? 0 }} {{ org.memberCount === 1 ? 'Member' : 'Members' }}
        </span>
      </div>
    </CardFooter>
  </Card>
</template>
