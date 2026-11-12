<script setup lang="ts">
import { computed } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'
import { roleDescription as describeRole } from '~/utils/roles'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import {
  ShieldIcon,
  CpuIcon,
  LayersIcon,
  UsersIcon,
  BuildingIcon,
  TerminalIcon
} from 'lucide-vue-next'

const {
  userRole,
  canManageUsers,
  canManageActiveDirectory,
  canManageFunctionalSettings,
  canExecuteRemote,
  canAdministerSystem
} = useAuthSession()

// One description per backend role (app/utils/roles.ts). The previous switch knew ten of the
// nineteen roles and described everyone else - a plant director included - as an operator.
const roleDescription = computed(() => describeRole(userRole.value))
</script>

<template>
  <div class="space-y-6">
    <Card class="border-border/80">
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle class="text-lg">Role &amp; Platform Governance Scope</CardTitle>
            <CardDescription>
              Your effective privileges based on Heimdall role assignments and active enterprise directory groups.
            </CardDescription>
          </div>
          <RoleBadge :role="userRole" class="scale-110" />
        </div>
      </CardHeader>
      <CardContent class="space-y-6">
        <div class="p-4 rounded-lg bg-muted/30 border border-border/50">
          <div class="text-xs font-semibold text-muted-foreground uppercase mb-1">Active Assignment</div>
          <div class="text-sm font-bold text-foreground">{{ userRole }}</div>
          <p class="text-xs text-muted-foreground mt-1">{{ roleDescription }}</p>
        </div>

        <div>
          <div class="text-xs font-bold text-muted-foreground uppercase tracking-wider mb-3">
            Granular System Capabilities
          </div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <UsersIcon class="h-4 w-4 text-primary" />
                <div>
                  <div class="text-xs font-semibold">User Administration (/dashboard/users)</div>
                  <div class="text-[10px] text-muted-foreground">Manage roles and platform user access</div>
                </div>
              </div>
              <Badge :variant="canManageUsers ? 'default' : 'outline'" class="text-[10px]">
                {{ canManageUsers ? 'Enabled' : 'Restricted' }}
              </Badge>
            </div>

            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <ShieldIcon class="h-4 w-4 text-cyan-400" />
                <div>
                  <div class="text-xs font-semibold">Active Directory &amp; Entra OU Approvals</div>
                  <div class="text-[10px] text-muted-foreground">Authorize OUs for Read/Write ingestion</div>
                </div>
              </div>
              <Badge :variant="canManageActiveDirectory ? 'default' : 'outline'" class="text-[10px]">
                {{ canManageActiveDirectory ? 'Enabled' : 'Restricted' }}
              </Badge>
            </div>

            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <CpuIcon class="h-4 w-4 text-amber-400" />
                <div>
                  <div class="text-xs font-semibold">Functional Telemetry Settings</div>
                  <div class="text-[10px] text-muted-foreground">Configure telemetry templates &amp; machine rules</div>
                </div>
              </div>
              <Badge :variant="canManageFunctionalSettings ? 'default' : 'outline'" class="text-[10px]">
                {{ canManageFunctionalSettings ? 'Enabled' : 'Restricted' }}
              </Badge>
            </div>

            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <TerminalIcon class="h-4 w-4 text-emerald-400" />
                <div>
                  <div class="text-xs font-semibold">Remote Industrial Commands</div>
                  <div class="text-[10px] text-muted-foreground">Trigger remote script and service execution</div>
                </div>
              </div>
              <Badge :variant="canExecuteRemote ? 'default' : 'outline'" class="text-[10px]">
                {{ canExecuteRemote ? 'Enabled' : 'Restricted' }}
              </Badge>
            </div>

            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <BuildingIcon class="h-4 w-4 text-purple-400" />
                <div>
                  <div class="text-xs font-semibold">Master Platform Governance &amp; Studio</div>
                  <div class="text-[10px] text-muted-foreground">Access Better Auth Studio &amp; global settings</div>
                </div>
              </div>
              <Badge :variant="canAdministerSystem ? 'default' : 'outline'" class="text-[10px]">
                {{ canAdministerSystem ? 'Enabled' : 'Restricted' }}
              </Badge>
            </div>

            <div class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50">
              <div class="flex items-center gap-2.5">
                <LayersIcon class="h-4 w-4 text-blue-400" />
                <div>
                  <div class="text-xs font-semibold">Line Management &amp; Shift Dedication</div>
                  <div class="text-[10px] text-muted-foreground">Manage technician shift rotas and absences</div>
                </div>
              </div>
              <Badge :variant="userRole === 'manager' ? 'default' : 'outline'" class="text-[10px]">
                {{ userRole === 'manager' ? 'Line Manager' : 'Not Assigned' }}
              </Badge>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
