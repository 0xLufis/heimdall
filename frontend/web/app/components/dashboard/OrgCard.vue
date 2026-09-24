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
  <Card class="bg-card border-border rounded-xl shadow-sm hover:border-border/80 transition-all overflow-hidden group font-sans">
    <CardHeader class="p-5 pb-3">
      <div class="flex items-start justify-between mb-3">
        <div class="size-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-600 dark:text-indigo-400 flex items-center justify-center text-base font-bold shadow-sm">
          {{ org.name.charAt(0).toUpperCase() }}
        </div>
        <div class="flex gap-1">
          <RbacButton
            capability="canAdministerSystem"
            variant="ghost"
            size="icon"
            class="size-8 text-muted-foreground hover:text-indigo-600 dark:hover:text-indigo-400 hover:bg-muted rounded-lg transition-colors"
            @click="$emit('edit', org)"
            title="Edit Organization"
          >
            <Edit2 class="size-3.5" />
          </RbacButton>
          <RbacButton
            capability="canAdministerSystem"
            variant="ghost"
            size="icon"
            class="size-8 text-muted-foreground hover:text-rose-600 dark:hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
            @click="$emit('delete', org)"
            title="Delete Organization"
          >
            <Trash2 class="size-3.5" />
          </RbacButton>
        </div>
      </div>
      <h5 class="text-base font-semibold text-foreground group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors truncate">{{ org.name }}</h5>
      <div class="flex flex-wrap items-center gap-1.5 mt-1">
        <div class="text-xs bg-muted text-muted-foreground px-2 py-0.5 rounded font-mono truncate max-w-[160px] border border-border">
          {{ org.slug }}
        </div>
        <div v-if="org.ouPath" class="text-[10px] bg-cyan-500/10 text-cyan-700 dark:text-cyan-400 border border-cyan-500/20 px-1.5 py-0.5 rounded font-mono truncate max-w-[140px]" :title="org.ouPath">
          {{ org.ouPath.split(',')[0].replace('OU=', '') }}
        </div>
        <div v-if="org.vlanName" class="text-[10px] bg-indigo-500/10 text-indigo-700 dark:text-indigo-400 border border-indigo-500/20 px-1.5 py-0.5 rounded font-mono">
          VLAN {{ org.vlanId }}
        </div>
      </div>
    </CardHeader>

    <CardContent class="px-5 pb-4">
      <div class="flex items-center justify-between border-t border-border/80 pt-3 text-xs">
        <div class="flex items-center gap-1.5 text-muted-foreground">
          <Building2 class="size-3.5 text-muted-foreground/70" />
          <span>Plant Organization</span>
        </div>
        <div class="flex items-center gap-1 text-emerald-600 dark:text-emerald-400 font-medium">
          <ShieldCheck class="size-3.5" />
          <span>Active Site</span>
        </div>
      </div>
    </CardContent>

    <CardFooter class="px-5 py-3 bg-muted/30 flex items-center justify-between border-t border-border">
      <RbacButton
        capability="canAdministerSystem"
        variant="link"
        class="h-auto p-0 text-xs font-medium text-indigo-600 dark:text-indigo-400 hover:text-indigo-500 dark:hover:text-indigo-300 no-underline transition-colors"
        @click="$emit('manage-members', org)"
      >
        Manage Members
      </RbacButton>
      <div class="flex items-center gap-1.5 text-muted-foreground text-xs">
        <Users class="size-3.5 text-muted-foreground/70" />
        <span>
          {{ org.memberCount ?? 0 }} {{ org.memberCount === 1 ? 'Member' : 'Members' }}
        </span>
      </div>
    </CardFooter>
  </Card>
</template>
