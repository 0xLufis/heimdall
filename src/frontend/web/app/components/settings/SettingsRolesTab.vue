<script setup lang="ts">
import { computed } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'
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

const roleDescription = computed(() => {
  switch (userRole.value.toLowerCase()) {
    case 'system_admin':
      return 'System Administrator: Full platform superuser authority across all modules, IT directory approvals, engineering telemetry, and user management.'
    case 'heimdall_admin':
    case 'admin':
      return 'Platform Administrator: Master system governance, plant organizations, audit logs, and Identity Studio console access.'
    case 'it_admin':
      return 'IT Infrastructure Administrator: Active Directory & Entra ID integration, OU Read/Write approvals, security groups, and PKI.'
    case 'engineering_admin':
      return 'Engineering Administrator: Technical functional settings, OT telemetry templates, machine groups, and app user administration.'
    case 'manager':
      return 'Plant Line Management: Manages operational teams, technician shift rotas, and absence authorizations.'
    case 'group_leader':
      return 'Engineering Group Leader: Station, line, and technology dedications across production cells.'
    case 'shift_leader':
      return 'Technician Shift Leader: Shift maintenance scheduling and dedicated shift technicians.'
    case 'engineer':
    case 'controls_engineer':
      return 'Controls Engineer: PLC automation, TwinCAT Copia code review, telemetry models, and machine diagnosis.'
    case 'technician':
      return 'Maintenance Technician: Incident triage, field repair dispatch, telemetry inspection, and parts management.'
    case 'operator':
    default:
      return 'Operator: Machine line operator. Incident reporting and machine status overview.'
  }
})
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
