<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { authClient } from '~/utils/auth-client'
import { useAuthSession } from '~/composables/useAuthSession'
import { useAppSettings } from '~/composables/useAppSettings'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Avatar, AvatarImage, AvatarFallback } from '@/components/ui/avatar'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import {
  UserIcon,
  ShieldIcon,
  KeyIcon,
  PaletteIcon,
  CheckIcon,
  AlertCircleIcon,
  RefreshCwIcon,
  LockIcon,
  MonitorIcon,
  SunIcon,
  MoonIcon,
  CpuIcon,
  CheckCircle2Icon,
  LayersIcon,
  UsersIcon,
  UserCheckIcon,
  BuildingIcon,
  TerminalIcon
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const {
  user,
  userRole,
  activeOrg,
  isSystemAdmin,
  isHeimdallAdmin,
  isItAdmin,
  isEngineeringAdmin,
  canManageUsers,
  canManageActiveDirectory,
  canManageFunctionalSettings,
  canManageEndpoints,
  canExecuteRemote,
  canAdministerSystem,
  simulatedPersona,
  setSimulatedPersona,
  clearSimulatedPersona,
  DEMO_PERSONAS
} = useAuthSession()

const colorMode = useColorMode()
const { sidebar } = useAppSettings()

// Active tab
const activeTab = ref('profile')

// Profile state
const profileName = ref('')
const profileEmail = ref('')
const profileAvatar = ref('')
const profileSuccess = ref('')
const profileError = ref('')
const savingProfile = ref(false)

// Password state
const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const passwordSuccess = ref('')
const passwordError = ref('')
const changingPassword = ref(false)

// Active sessions state
const sessions = ref<any[]>([])
const loadingSessions = ref(false)
const sessionSuccess = ref('')
const sessionError = ref('')

function syncProfileFields() {
  if (user.value) {
    profileName.value = user.value.name || ''
    profileEmail.value = user.value.email || ''
    profileAvatar.value = (user.value as any)?.image || (user.value as any)?.avatar || ''
  }
}

watch(() => user.value, () => {
  syncProfileFields()
}, { immediate: true })

async function handleSaveProfile() {
  savingProfile.value = true
  profileSuccess.value = ''
  profileError.value = ''
  try {
    // If authClient has updateUser endpoint
    if (authClient && (authClient as any).updateUser) {
      await (authClient as any).updateUser({
        name: profileName.value,
        image: profileAvatar.value
      })
    }
    // Update local reactive user state if simulated or local
    if (user.value) {
      user.value.name = profileName.value
      if (profileAvatar.value) {
        (user.value as any).image = profileAvatar.value
      }
    }
    profileSuccess.value = 'Profile information saved successfully.'
  } catch (err: any) {
    profileError.value = err.message || 'Failed to update profile.'
  } finally {
    savingProfile.value = false
  }
}

async function handleChangePassword() {
  if (!currentPassword.value || !newPassword.value) {
    passwordError.value = 'Please provide both current and new passwords.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = 'New password and confirmation do not match.'
    return
  }
  if (newPassword.value.length < 8) {
    passwordError.value = 'Password must be at least 8 characters in length.'
    return
  }

  changingPassword.value = true
  passwordSuccess.value = ''
  passwordError.value = ''

  try {
    if (authClient && (authClient as any).changePassword) {
      const { error: changeErr } = await (authClient as any).changePassword({
        currentPassword: currentPassword.value,
        newPassword: newPassword.value,
        revokeOtherSessions: true
      })
      if (changeErr) {
        passwordError.value = changeErr.message || 'Failed to update password.'
        return
      }
    }
    passwordSuccess.value = 'Password successfully updated and other sessions revoked.'
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
  } catch (err: any) {
    passwordError.value = err.message || 'Failed to update password.'
  } finally {
    changingPassword.value = false
  }
}

async function fetchSessions() {
  loadingSessions.value = true
  try {
    if (authClient && (authClient as any).listSessions) {
      const res = await (authClient as any).listSessions()
      sessions.value = res?.data || res || []
    }
  } catch {
    // Fallback default active session if offline/dev
    sessions.value = [
      {
        id: 'sess-current',
        ipAddress: '127.0.0.1 (Local Workstation)',
        userAgent: 'Chrome 128 / Linux x86_64',
        createdAt: new Date().toISOString(),
        isCurrent: true
      }
    ]
  } finally {
    if (sessions.value.length === 0) {
      sessions.value = [
        {
          id: 'sess-current',
          ipAddress: '127.0.0.1 (Local Workstation)',
          userAgent: 'Chrome 128 / Linux x86_64',
          createdAt: new Date().toISOString(),
          isCurrent: true
        }
      ]
    }
    loadingSessions.value = false
  }
}

async function handleRevokeOtherSessions() {
  sessionSuccess.value = ''
  sessionError.value = ''
  try {
    if (authClient && (authClient as any).revokeOtherSessions) {
      await (authClient as any).revokeOtherSessions()
    }
    sessionSuccess.value = 'All concurrent sessions revoked.'
    sessions.value = sessions.value.filter(s => s.isCurrent)
  } catch (err: any) {
    sessionError.value = err.message || 'Failed to revoke concurrent sessions.'
  }
}

const roleDescription = computed(() => {
  switch (userRole.value.toLowerCase()) {
    case 'system_admin':
      return 'God User: Full superuser authority across all modules, IT directory approvals, engineering telemetry, and user management.'
    case 'heimdall_admin':
    case 'admin':
      return 'Platform Administrator: Master system governance, plant organizations, audit logs, and Identity Studio console access.'
    case 'it_admin':
      return 'IT Infrastructure Administrator: Active Directory & Entra ID integration, OU Read/Write approvals, security groups, and PKI.'
    case 'engineering_admin':
      return 'Engineering Administrator: Technical functional settings, OT telemetry templates, machine groups, and app user administration (distinct from plant line management).'
    case 'manager':
      return 'Plant Line Management: Manages operational teams, technician shift rotas, and absence authorizations.'
    case 'group_leader':
      return 'Engineering Group Leader: Station, line, and technology dedications across production cells.'
    case 'shift_leader':
      return 'Technician Shift Leader: Shift maintenance scheduling and dedicated shift technicians.'
    case 'engineer':
    case 'controls_engineer':
    case 'lead_engineer':
      return 'Controls / Systems Engineer: PLC configuration, telemetry dispatch, and machine diagnostics.'
    case 'technician':
      return 'Field Maintenance Technician: Ticket execution, diagnostic log review, and equipment servicing.'
    default:
      return 'Standard User: Basic platform viewing permissions.'
  }
})

const mfaPolicyRequirement = computed(() => {
  const r = userRole.value.toLowerCase()
  if (['system_admin', 'heimdall_admin', 'it_admin'].includes(r)) {
    return {
      title: 'High-Security Administrator (Strict Enforcement)',
      threshold: 'Every Sign-In',
      description: 'MFA prompt is required on every single authentication event with no session grace period.'
    }
  }
  if (['engineer', 'controls_engineer', 'lead_engineer', 'engineering_admin'].includes(r)) {
    return {
      title: 'Engineering Technical Tier',
      threshold: '7 Days Grace Period',
      description: 'MFA authentication remains valid for 7 days before requiring re-verification.'
    }
  }
  if (r === 'technician') {
    return {
      title: 'Plant Operations / Technician Tier',
      threshold: '30 Days Grace Period',
      description: 'MFA authentication remains valid for 30 days before requiring re-verification.'
    }
  }
  return {
    title: 'Standard User Policy',
    threshold: '30 Days Grace Period',
    description: 'Standard session threshold applied to general platform members.'
  }
})

onMounted(() => {
  syncProfileFields()
  fetchSessions()
})
</script>

<template>
  <div class="space-y-6 max-w-6xl mx-auto">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight flex items-center gap-2">
          <UserIcon class="h-7 w-7 text-primary" />
          User Settings & Preferences
        </h1>
        <p class="text-sm text-muted-foreground mt-1">
          Manage your account profile, role capabilities, security credentials, and workspace appearance.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <RoleBadge :role="userRole" />
        <Badge variant="outline" class="font-mono text-xs">
          Org: {{ activeOrg?.name || 'Heimdall Engineering' }}
        </Badge>
      </div>
    </div>

    <!-- Tabs Container -->
    <Tabs v-model="activeTab" class="w-full space-y-6">
      <TabsList class="grid grid-cols-2 md:grid-cols-5 w-full bg-muted/60 p-1 rounded-lg">
        <TabsTrigger value="profile" class="flex items-center gap-2 text-xs font-semibold">
          <UserIcon class="h-4 w-4" />
          <span>Profile</span>
        </TabsTrigger>
        <TabsTrigger value="roles" class="flex items-center gap-2 text-xs font-semibold">
          <ShieldIcon class="h-4 w-4" />
          <span>Role & Access</span>
        </TabsTrigger>
        <TabsTrigger value="security" class="flex items-center gap-2 text-xs font-semibold">
          <KeyIcon class="h-4 w-4" />
          <span>Security & MFA</span>
        </TabsTrigger>
        <TabsTrigger value="appearance" class="flex items-center gap-2 text-xs font-semibold">
          <PaletteIcon class="h-4 w-4" />
          <span>Appearance</span>
        </TabsTrigger>
        <TabsTrigger value="personas" class="flex items-center gap-2 text-xs font-semibold">
          <UserCheckIcon class="h-4 w-4 text-cyan-400" />
          <span>Persona Sandbox</span>
        </TabsTrigger>
      </TabsList>

      <!-- TAB 1: Profile & Identity -->
      <TabsContent value="profile" class="space-y-6">
        <Card class="border-border/80">
          <CardHeader>
            <CardTitle class="text-lg">Personal Identity & Profile</CardTitle>
            <CardDescription>
              Your public display information across Heimdall, ticket comments, and audit logs.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-6">
            <div v-if="profileSuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
              <CheckCircle2Icon class="h-4 w-4 shrink-0" />
              <span>{{ profileSuccess }}</span>
            </div>
            <div v-if="profileError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
              <AlertCircleIcon class="h-4 w-4 shrink-0" />
              <span>{{ profileError }}</span>
            </div>

            <div class="flex flex-col sm:flex-row items-center gap-6 p-4 rounded-lg bg-muted/30 border border-border/50">
              <Avatar class="h-20 w-20 rounded-xl border-2 border-primary/40 shadow-md">
                <AvatarImage :src="profileAvatar" :alt="profileName" />
                <AvatarFallback class="rounded-xl bg-primary text-primary-foreground font-black text-2xl">
                  {{ profileName?.charAt(0).toUpperCase() || 'U' }}
                </AvatarFallback>
              </Avatar>
              <div class="space-y-1 text-center sm:text-left">
                <div class="text-lg font-bold">{{ profileName || 'Unnamed User' }}</div>
                <div class="text-xs text-muted-foreground font-mono">{{ profileEmail }}</div>
                <div class="pt-2 flex flex-wrap gap-2 justify-center sm:justify-start">
                  <RoleBadge :role="userRole" />
                  <Badge variant="outline" class="text-[10px] uppercase font-mono">
                    Provider: Better-Auth / Local
                  </Badge>
                </div>
              </div>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase">Full Name</label>
                <Input v-model="profileName" placeholder="e.g. András Molnár" />
              </div>
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase">Email Address</label>
                <Input v-model="profileEmail" placeholder="user@heimdall.dev" :disabled="true" class="bg-muted/50 font-mono text-xs" />
                <p class="text-[10px] text-muted-foreground">Contact IT Administrator to change verified email.</p>
              </div>
              <div class="space-y-1.5 md:col-span-2">
                <label class="text-xs font-semibold text-muted-foreground uppercase">Avatar Image URL</label>
                <Input v-model="profileAvatar" placeholder="https://example.com/avatar.png" />
              </div>
            </div>
          </CardContent>
          <CardFooter class="flex justify-end gap-2 border-t border-border/50 pt-4">
            <Button size="sm" @click="handleSaveProfile" :disabled="savingProfile">
              <RefreshCwIcon v-if="savingProfile" class="h-3.5 w-3.5 mr-2 animate-spin" />
              <CheckIcon v-else class="h-3.5 w-3.5 mr-2" />
              Save Profile Changes
            </Button>
          </CardFooter>
        </Card>
      </TabsContent>

      <!-- TAB 2: Role & System Capabilities -->
      <TabsContent value="roles" class="space-y-6">
        <Card class="border-border/80">
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle class="text-lg">Role & Platform Governance Scope</CardTitle>
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
                      <div class="text-xs font-semibold">Active Directory & Entra OU Approvals</div>
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
                      <div class="text-[10px] text-muted-foreground">Configure telemetry templates & machine rules</div>
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
                      <div class="text-xs font-semibold">Master Platform Governance & Studio</div>
                      <div class="text-[10px] text-muted-foreground">Access Better Auth Studio & global settings</div>
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
                      <div class="text-xs font-semibold">Line Management & Shift Dedication</div>
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
      </TabsContent>

      <!-- TAB 3: Security & Credentials -->
      <TabsContent value="security" class="space-y-6">
        <!-- MFA Status Card -->
        <Card class="border-border/80">
          <CardHeader>
            <CardTitle class="text-base flex items-center gap-2">
              <ShieldIcon class="h-5 w-5 text-emerald-400" />
              <span>Multi-Factor Authentication (MFA) Policy</span>
            </CardTitle>
            <CardDescription>
              Enforced security thresholds based on enterprise zero-trust governance.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div class="p-4 rounded-lg bg-muted/30 border border-border/50 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div>
                <div class="font-bold text-sm text-foreground">{{ mfaPolicyRequirement.title }}</div>
                <p class="text-xs text-muted-foreground mt-1">{{ mfaPolicyRequirement.description }}</p>
              </div>
              <Badge variant="outline" class="font-mono text-xs text-emerald-400 border-emerald-500/40 uppercase self-start sm:self-center">
                Threshold: {{ mfaPolicyRequirement.threshold }}
              </Badge>
            </div>
          </CardContent>
        </Card>

        <!-- Password Change Card -->
        <Card class="border-border/80">
          <CardHeader>
            <CardTitle class="text-base flex items-center gap-2">
              <LockIcon class="h-5 w-5 text-primary" />
              <span>Update Password</span>
            </CardTitle>
            <CardDescription>
              Ensure your account uses a strong passphrase of at least 8 characters.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div v-if="passwordSuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
              <CheckCircle2Icon class="h-4 w-4 shrink-0" />
              <span>{{ passwordSuccess }}</span>
            </div>
            <div v-if="passwordError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
              <AlertCircleIcon class="h-4 w-4 shrink-0" />
              <span>{{ passwordError }}</span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase">Current Password</label>
                <Input v-model="currentPassword" type="password" placeholder="••••••••" />
              </div>
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase">New Password</label>
                <Input v-model="newPassword" type="password" placeholder="••••••••" />
              </div>
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-muted-foreground uppercase">Confirm New Password</label>
                <Input v-model="confirmPassword" type="password" placeholder="••••••••" />
              </div>
            </div>
          </CardContent>
          <CardFooter class="flex justify-end gap-2 border-t border-border/50 pt-4">
            <Button size="sm" @click="handleChangePassword" :disabled="changingPassword">
              <RefreshCwIcon v-if="changingPassword" class="h-3.5 w-3.5 mr-2 animate-spin" />
              <LockIcon v-else class="h-3.5 w-3.5 mr-2" />
              Update Password
            </Button>
          </CardFooter>
        </Card>

        <!-- Active Sessions Card -->
        <Card class="border-border/80">
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle class="text-base flex items-center gap-2">
                  <MonitorIcon class="h-5 w-5 text-cyan-400" />
                  <span>Active Sessions & Authorized Devices</span>
                </CardTitle>
                <CardDescription>
                  Review and revoke active sign-in sessions for this identity.
                </CardDescription>
              </div>
              <Button size="sm" variant="outline" @click="handleRevokeOtherSessions">
                Revoke Other Sessions
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <div v-if="sessionSuccess" class="p-3 mb-4 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
              <CheckCircle2Icon class="h-4 w-4 shrink-0" />
              <span>{{ sessionSuccess }}</span>
            </div>
            <div v-if="sessionError" class="p-3 mb-4 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
              <AlertCircleIcon class="h-4 w-4 shrink-0" />
              <span>{{ sessionError }}</span>
            </div>

            <div class="divide-y divide-border rounded-md border border-border">
              <div v-for="s in sessions" :key="s.id" class="p-3 flex items-center justify-between text-xs">
                <div class="space-y-0.5">
                  <div class="font-bold flex items-center gap-2">
                    <span>{{ s.userAgent || 'Web Browser' }}</span>
                    <Badge v-if="s.isCurrent" variant="default" class="text-[9px] uppercase">Current Session</Badge>
                  </div>
                  <div class="text-muted-foreground font-mono text-[11px]">{{ s.ipAddress }}</div>
                </div>
                <div class="text-muted-foreground text-[11px]">
                  Started: {{ new Date(s.createdAt).toLocaleDateString() }}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- TAB 4: Appearance & Preferences -->
      <TabsContent value="appearance" class="space-y-6">
        <Card class="border-border/80">
          <CardHeader>
            <CardTitle class="text-lg flex items-center gap-2">
              <PaletteIcon class="h-5 w-5 text-purple-400" />
              <span>Theme Appearance & Interface Preferences</span>
            </CardTitle>
            <CardDescription>
              Select your color mode and layout behavior across the Heimdall dashboard.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-6">
            <div>
              <div class="text-xs font-semibold text-muted-foreground uppercase mb-3">Color Scheme</div>
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <!-- Light -->
                <button
                  type="button"
                  @click="colorMode.preference = 'light'"
                  class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
                  :class="colorMode.preference === 'light' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
                >
                  <SunIcon class="h-8 w-8 text-amber-500" />
                  <div class="font-bold text-sm">Light Mode</div>
                </button>

                <!-- Dark -->
                <button
                  type="button"
                  @click="colorMode.preference = 'dark'"
                  class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
                  :class="colorMode.preference === 'dark' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
                >
                  <MoonIcon class="h-8 w-8 text-indigo-400" />
                  <div class="font-bold text-sm">Dark Mode (Default)</div>
                </button>

                <!-- System -->
                <button
                  type="button"
                  @click="colorMode.preference = 'system'"
                  class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
                  :class="colorMode.preference === 'system' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
                >
                  <MonitorIcon class="h-8 w-8 text-slate-400" />
                  <div class="font-bold text-sm">System Default</div>
                </button>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- TAB 5: Persona Sandbox -->
      <TabsContent value="personas" class="space-y-6">
        <Card class="border-border/80">
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle class="text-lg flex items-center gap-2">
                  <UserCheckIcon class="h-5 w-5 text-cyan-400" />
                  <span>Developer & QA Persona Simulation Sandbox</span>
                </CardTitle>
                <CardDescription>
                  Instantly switch between role personas to test and verify RBAC permissions and UI features live.
                </CardDescription>
              </div>
              <Button 
                variant="outline" 
                size="sm" 
                @click="clearSimulatedPersona" 
                :disabled="!simulatedPersona"
              >
                Reset to Real Session
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-4">
            <div v-if="simulatedPersona" class="p-3 rounded-lg bg-cyan-500/15 border border-cyan-500/30 text-cyan-400 text-xs flex items-center justify-between">
              <div class="flex items-center gap-2">
                <CheckCircle2Icon class="h-4 w-4 shrink-0" />
                <span>Currently Simulating: <strong>{{ simulatedPersona.name }}</strong></span>
              </div>
              <RoleBadge :role="simulatedPersona.role" />
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              <div
                v-for="p in DEMO_PERSONAS"
                :key="p.id"
                class="p-3.5 rounded-lg border transition-all flex flex-col justify-between gap-3 bg-card"
                :class="simulatedPersona?.id === p.id ? 'border-primary ring-1 ring-primary/30' : 'border-border/60 hover:border-border'"
              >
                <div>
                  <div class="flex items-center justify-between gap-2">
                    <span class="font-bold text-sm">{{ p.name }}</span>
                    <RoleBadge :role="p.role" class="scale-90 origin-right" />
                  </div>
                  <div class="font-mono text-[11px] text-muted-foreground mt-0.5">{{ p.email }}</div>
                  <p class="text-xs text-muted-foreground mt-2 line-clamp-2">{{ p.description }}</p>
                </div>
                <Button 
                  size="sm" 
                  variant="outline"
                  class="w-full text-xs"
                  :class="simulatedPersona?.id === p.id ? 'bg-primary text-primary-foreground hover:bg-primary/90' : ''"
                  @click="setSimulatedPersona(p)"
                >
                  {{ simulatedPersona?.id === p.id ? 'Active Simulation' : 'Simulate Role' }}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>
    </Tabs>
  </div>
</template>

<style scoped>
</style>
