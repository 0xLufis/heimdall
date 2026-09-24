<script setup lang="ts">
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '~/components/ui/table'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '~/components/ui/select'
import { Button } from '~/components/ui/button'
import { Eye, Ban, CheckCircle2, Users } from 'lucide-vue-next'
import DashboardUserAvatar from './UserAvatar.vue'
import DashboardRoleBadge from './RoleBadge.vue'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'

const { canManageUsers } = useRbacPermission()

defineProps<{
  users: any[],
  roles: string[],
  loading: boolean
}>()

defineEmits(['update-role', 'ban-user', 'unban-user'])

function formatDate(date: string | Date) {
  return new Date(date).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
  })
}
</script>

<template>
  <div class="bg-card rounded-xl shadow-sm border border-border overflow-hidden relative font-sans">
    <!-- Loading Overlay -->
    <div v-if="loading" class="absolute inset-0 bg-card/60 backdrop-blur-[1px] z-10 flex items-center justify-center">
      <div class="flex flex-col items-center gap-3">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-500"></div>
        <span class="text-xs font-medium text-muted-foreground">Loading directory...</span>
      </div>
    </div>

    <div class="overflow-x-auto">
      <Table>
        <TableHeader class="bg-muted/50 border-b border-border">
          <TableRow class="border-b border-border hover:bg-transparent">
            <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Identity</TableHead>
            <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Access Level</TableHead>
            <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Account Health</TableHead>
            <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold">Metadata</TableHead>
            <TableHead class="px-4 py-3 text-xs text-muted-foreground uppercase tracking-wider font-semibold text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody class="divide-y divide-border">
          <TableRow v-for="user in users" :key="user.id" class="group hover:bg-muted/50 transition-colors border-b border-border">
            <TableCell class="px-4 py-3">
              <DashboardUserAvatar :name="user.name" :email="user.email" />
            </TableCell>
            <TableCell class="px-4 py-3">
              <div class="flex items-center gap-2">
                <DashboardRoleBadge :role="user.role" />
                <RbacTooltip :disabled="!canManageUsers" :tooltip="RBAC_TOOLTIPS.USER_MANAGEMENT">
                  <Select 
                    :disabled="!canManageUsers"
                    :model-value="user.role || 'user'" 
                    @update:model-value="(val) => canManageUsers && $emit('update-role', user.id, val)"
                  >
                    <SelectTrigger :disabled="!canManageUsers" class="h-7 w-full max-w-[130px] text-xs bg-background border-border font-medium text-foreground hover:bg-muted rounded-md transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed">
                      <SelectValue placeholder="Select Role" />
                    </SelectTrigger>
                    <SelectContent class="bg-popover border-border text-popover-foreground">
                      <SelectItem v-for="role in roles" :key="role" :value="role" class="text-xs focus:bg-accent focus:text-accent-foreground">
                        {{ role }}
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </RbacTooltip>
              </div>
            </TableCell>
            <TableCell class="px-4 py-3 text-xs font-medium">
              <div v-if="user.banned" class="text-rose-600 dark:text-rose-400 flex items-center gap-1.5">
                <span class="w-1.5 h-1.5 rounded-full bg-rose-500 animate-pulse"></span>
                Suspended
              </div>
              <div v-else class="text-emerald-600 dark:text-emerald-400 flex items-center gap-1.5">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
                Active
              </div>
            </TableCell>
            <TableCell class="px-4 py-3">
              <div class="text-[11px] text-muted-foreground font-medium">Active since</div>
              <div class="text-xs text-foreground font-medium">{{ formatDate(user.createdAt) }}</div>
            </TableCell>
            <TableCell class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-1">
                <Button 
                  variant="ghost" 
                  size="icon" 
                  @click="navigateTo(`/dashboard/users/${user.id}`)" 
                  class="size-8 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg"
                  title="View User Profile"
                >
                  <Eye class="size-4" />
                </Button>
                <RbacTooltip :disabled="!canManageUsers" :tooltip="RBAC_TOOLTIPS.USER_MANAGEMENT">
                  <Button 
                    v-if="!user.banned" 
                    variant="ghost" 
                    size="icon"
                    :disabled="!canManageUsers"
                    @click="canManageUsers && $emit('ban-user', user.id)" 
                    class="size-8 text-muted-foreground hover:text-rose-600 dark:hover:text-rose-400 hover:bg-rose-500/10 rounded-lg disabled:opacity-40 disabled:pointer-events-none"
                    title="Suspend Account"
                  >
                    <Ban class="size-4" />
                  </Button>
                  <Button 
                    v-else 
                    variant="ghost" 
                    size="icon"
                    :disabled="!canManageUsers"
                    @click="canManageUsers && $emit('unban-user', user.id)" 
                    class="size-8 text-muted-foreground hover:text-emerald-600 dark:hover:text-emerald-400 hover:bg-emerald-500/10 rounded-lg disabled:opacity-40 disabled:pointer-events-none"
                    title="Restore Access"
                  >
                    <CheckCircle2 class="size-4" />
                  </Button>
                </RbacTooltip>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
    
    <!-- Empty State -->
    <div v-if="!loading && users.length === 0" class="flex flex-col items-center justify-center py-20 px-4">
      <div class="w-12 h-12 bg-muted text-muted-foreground rounded-xl flex items-center justify-center mb-3">
        <Users class="size-6" />
      </div>
      <p class="text-xs font-medium text-muted-foreground">No matching user records found.</p>
    </div>
  </div>
</template>
