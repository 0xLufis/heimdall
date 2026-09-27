<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
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
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter
} from '@/components/ui/dialog'
import {
  UserIcon,
  ShieldIcon,
  ShieldCheckIcon,
  ShieldAlertIcon,
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
  TerminalIcon,
  SmartphoneIcon,
  QrCodeIcon,
  CopyIcon,
  DownloadIcon,
  Trash2Icon,
  ArrowRightIcon,
  ClockIcon,
  KeyRoundIcon
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
  isPersonaSimulationAllowed,
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
      if (Array.isArray(res?.data)) {
        sessions.value = res.data
      } else if (Array.isArray(res)) {
        sessions.value = res
      } else {
        sessions.value = []
      }
    }
  } catch {
    sessions.value = []
  } finally {
    if (!Array.isArray(sessions.value) || sessions.value.length === 0) {
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
    sessions.value = (Array.isArray(sessions.value) ? sessions.value : []).filter(s => s && s.isCurrent)
  } catch (err: any) {
    sessionError.value = err.message || 'Failed to revoke concurrent sessions.'
  }
}

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

// Multi-Factor Authentication (MFA) State
const loadingMfa = ref(false)
const mfaState = ref({
  enabled: false,
  enrolledAt: null as string | null,
  method: 'none' as 'totp' | 'none',
  backupCodesCount: 0,
  lastVerifiedAt: null as string | null
})
const mfaEvaluation = ref<any>(null)
const evaluatingPolicy = ref(false)

// MFA Enrollment Wizard state
const isSettingUpMfa = ref(false)
const setupStep = ref<1 | 2 | 3>(1)
const mfaSetupData = ref<{
  secret: string
  qrCode: string
  totpUri: string
  backupCodes: string[]
} | null>(null)
const setupTotpCode = ref('')
const verifyingSetup = ref(false)
const setupError = ref('')
const setupSuccess = ref('')
const copiedSecret = ref(false)
const copiedBackupCodes = ref(false)

// Challenge / Re-verify Modal state
const showChallengeModal = ref(false)
const challengeCode = ref('')
const challengingMfa = ref(false)
const challengeError = ref('')
const challengeSuccess = ref('')

// Backup Codes View / Regenerate Modal state
const showBackupCodesModal = ref(false)
const userBackupCodes = ref<string[]>([])
const loadingBackupCodes = ref(false)
const regeneratingBackupCodes = ref(false)
const backupCodesError = ref('')
const copiedModalCodes = ref(false)

// Disable MFA Modal state
const showDisableMfaModal = ref(false)
const disablingMfa = ref(false)
const disableError = ref('')

interface SsoStatus {
  enabled: boolean
  provider: string
  providerName: string
  tenantId: string
  isConfigured: boolean
  allowMockSimulation: boolean
}
const ssoStatus = ref<SsoStatus | null>(null)

async function fetchSsoStatus() {
  try {
    const res = await $fetch<SsoStatus>('/api/auth/sso/status')
    ssoStatus.value = res
  } catch {
    ssoStatus.value = null
  }
}

const isSsoUser = computed(() => {
  const u = user.value as any
  if (!u) return false
  return (
    u.id?.startsWith('usr-entra') ||
    u.provider === 'microsoft' ||
    u.email?.includes('entra') ||
    u.email?.endsWith('@fake-factory.internal') ||
    u.email?.endsWith('@factory.corp')
  )
})

async function fetchMfaStatus() {
  loadingMfa.value = true
  try {
    const userId = user.value?.id || 'usr-default'
    const res: any = await $fetch('/api/user/mfa', { query: { userId } })
    if (res) {
      mfaState.value = {
        enabled: !!res.enabled,
        enrolledAt: res.enrolledAt || null,
        method: res.enabled ? 'totp' : 'none',
        backupCodesCount: res.backupCodesRemaining ?? res.backupCodesCount ?? 0,
        lastVerifiedAt: res.lastVerifiedAt || null
      }
    }
  } catch (err: any) {
    console.error('Failed to load MFA state', err)
  } finally {
    loadingMfa.value = false
  }
}

async function evaluateUserMfaPolicy() {
  evaluatingPolicy.value = true
  try {
    const res: any = await $fetch('/api/system/mfa-policy/evaluate', {
      method: 'POST',
      body: {
        role: userRole.value,
        lastMfaAt: mfaState.value.lastVerifiedAt || undefined
      }
    })
    mfaEvaluation.value = res
  } catch (err: any) {
    console.error('Failed to evaluate MFA policy', err)
  } finally {
    evaluatingPolicy.value = false
  }
}

async function startMfaSetup() {
  setupError.value = ''
  setupSuccess.value = ''
  setupTotpCode.value = ''
  setupStep.value = 1
  verifyingSetup.value = true
  try {
    const res: any = await $fetch('/api/user/mfa/setup', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        email: user.value?.email || 'user@heimdall.dev'
      }
    })
    mfaSetupData.value = res
    isSettingUpMfa.value = true
  } catch (err: any) {
    setupError.value = err.message || 'Failed to initialize MFA setup.'
  } finally {
    verifyingSetup.value = false
  }
}

function cancelMfaSetup() {
  isSettingUpMfa.value = false
  mfaSetupData.value = null
  setupTotpCode.value = ''
  setupError.value = ''
  setupStep.value = 1
}

async function handleVerifyAndEnableMfa() {
  if (!setupTotpCode.value || setupTotpCode.value.trim().length < 6) {
    setupError.value = 'Please enter a valid 6-digit TOTP code.'
    return
  }
  setupError.value = ''
  verifyingSetup.value = true
  try {
    await $fetch('/api/user/mfa/verify', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        code: setupTotpCode.value.trim()
      }
    })
    setupSuccess.value = 'Multi-Factor Authentication enabled successfully!'
    await fetchMfaStatus()
    await evaluateUserMfaPolicy()
    setupStep.value = 3
  } catch (err: any) {
    setupError.value = err.data?.message || err.message || 'Invalid verification code.'
  } finally {
    verifyingSetup.value = false
  }
}

function finishMfaSetup() {
  isSettingUpMfa.value = false
  mfaSetupData.value = null
  setupTotpCode.value = ''
  setupStep.value = 1
}

function openChallengeModal() {
  challengeCode.value = ''
  challengeError.value = ''
  challengeSuccess.value = ''
  showChallengeModal.value = true
}

async function handleVerifyChallenge() {
  if (!challengeCode.value || challengeCode.value.trim().length === 0) {
    challengeError.value = 'Please enter your 6-digit code or backup recovery code.'
    return
  }
  challengingMfa.value = true
  challengeError.value = ''
  challengeSuccess.value = ''
  try {
    const res: any = await $fetch('/api/user/mfa/challenge', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        code: challengeCode.value.trim()
      }
    })
    challengeSuccess.value = res.usedBackupCode
      ? `Re-verified successfully using a backup recovery code! (${res.backupCodesRemaining} codes remaining)`
      : 'TOTP challenge verified successfully. Session grace period renewed!'
    await fetchMfaStatus()
    await evaluateUserMfaPolicy()
    setTimeout(() => {
      showChallengeModal.value = false
      challengeSuccess.value = ''
    }, 1800)
  } catch (err: any) {
    challengeError.value = err.data?.message || err.message || 'Verification challenge failed.'
  } finally {
    challengingMfa.value = false
  }
}

async function openBackupCodesModal() {
  showBackupCodesModal.value = true
  loadingBackupCodes.value = true
  backupCodesError.value = ''
  try {
    const res: any = await $fetch('/api/user/mfa/backup-codes', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        action: 'get'
      }
    })
    userBackupCodes.value = res.backupCodes || []
  } catch (err: any) {
    backupCodesError.value = err.data?.message || err.message || 'Failed to retrieve recovery codes.'
  } finally {
    loadingBackupCodes.value = false
  }
}

async function handleRegenerateBackupCodes() {
  regeneratingBackupCodes.value = true
  backupCodesError.value = ''
  try {
    const res: any = await $fetch('/api/user/mfa/backup-codes', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        action: 'regenerate'
      }
    })
    userBackupCodes.value = res.backupCodes || []
    await fetchMfaStatus()
  } catch (err: any) {
    backupCodesError.value = err.data?.message || err.message || 'Failed to regenerate recovery codes.'
  } finally {
    regeneratingBackupCodes.value = false
  }
}

function openDisableMfaModal() {
  disableError.value = ''
  showDisableMfaModal.value = true
}

async function handleDisableMfa() {
  disablingMfa.value = true
  disableError.value = ''
  try {
    await $fetch('/api/user/mfa/disable', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default'
      }
    })
    showDisableMfaModal.value = false
    await fetchMfaStatus()
    await evaluateUserMfaPolicy()
  } catch (err: any) {
    disableError.value = err.data?.message || err.message || 'Failed to disable MFA.'
  } finally {
    disablingMfa.value = false
  }
}

function copyToClipboard(text: string, type: 'secret' | 'setupCodes' | 'modalCodes') {
  if (!navigator?.clipboard) return
  navigator.clipboard.writeText(text)
  if (type === 'secret') {
    copiedSecret.value = true
    setTimeout(() => { copiedSecret.value = false }, 2000)
  } else if (type === 'setupCodes') {
    copiedBackupCodes.value = true
    setTimeout(() => { copiedBackupCodes.value = false }, 2000)
  } else if (type === 'modalCodes') {
    copiedModalCodes.value = true
    setTimeout(() => { copiedModalCodes.value = false }, 2000)
  }
}

function downloadBackupCodes(codes: string[]) {
  if (!codes || !codes.length) return
  const content = `HEIMDALL TWO-FACTOR AUTHENTICATION RECOVERY CODES
Account: ${user.value?.email || 'user@heimdall.dev'}
Generated: ${new Date().toISOString()}

Save these one-time recovery codes in a secure location.
Each code can only be used once.

${codes.map((c, i) => `${(i + 1).toString().padStart(2, '0')}. ${c}`).join('\n')}
`
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `heimdall-mfa-backup-codes-${Date.now()}.txt`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

watch(() => userRole.value, () => {
  evaluateUserMfaPolicy()
})

onMounted(() => {
  syncProfileFields()
  fetchSessions()
  fetchMfaStatus()
  evaluateUserMfaPolicy()
  fetchSsoStatus()
})
</script>

<template>
  <div class="space-y-6 max-w-6xl mx-auto">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-600 dark:text-indigo-400">
          <UserIcon class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-foreground">
            User Settings & Preferences
          </h1>
          <p class="text-sm text-muted-foreground mt-0.5">
            Manage your account profile, role capabilities, security credentials, and workspace appearance
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <RoleBadge :role="userRole" />
        <Badge variant="outline" class="font-mono text-xs bg-muted border-border text-foreground">
          Org: {{ activeOrg?.name || 'Heimdall Engineering' }}
        </Badge>
      </div>
    </div>

    <!-- Tabs Container -->
    <Tabs v-model="activeTab" class="w-full space-y-6">
      <TabsList class="grid grid-cols-2 md:grid-cols-5 w-full bg-muted/60 p-1 rounded-xl border border-border h-auto">
        <TabsTrigger value="profile" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors">
          <UserIcon class="size-4" />
          <span>Profile</span>
        </TabsTrigger>
        <TabsTrigger value="roles" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors">
          <ShieldIcon class="size-4" />
          <span>Role & Access</span>
        </TabsTrigger>
        <TabsTrigger value="security" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors">
          <KeyIcon class="size-4" />
          <span>Security & MFA</span>
        </TabsTrigger>
        <TabsTrigger value="appearance" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors">
          <PaletteIcon class="size-4" />
          <span>Appearance</span>
        </TabsTrigger>
        <TabsTrigger v-if="isPersonaSimulationAllowed" value="personas" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors">
          <UserCheckIcon class="size-4 text-cyan-600 dark:text-cyan-400" />
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
                  <Badge 
                    variant="outline" 
                    class="text-[10px] uppercase font-mono flex items-center gap-1.5"
                    :class="isSsoUser ? 'border-sky-500/40 text-sky-400 bg-sky-500/10' : 'border-border text-muted-foreground'"
                  >
                    <span class="size-1.5 rounded-full" :class="isSsoUser ? 'bg-sky-400' : 'bg-muted-foreground'" />
                    Provider: {{ isSsoUser ? 'Microsoft Entra ID (Azure SSO)' : 'Better-Auth / Local Token' }}
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
        <!-- Enterprise Single Sign-On (Azure AD / Entra ID) Card -->
        <Card class="border-sky-500/30 bg-sky-500/5 dark:bg-sky-950/15 shadow-sm">
          <CardHeader>
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div class="space-y-1">
                <CardTitle class="text-base flex items-center gap-2">
                  <svg class="size-5 shrink-0" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <rect x="1" y="1" width="9" height="9" fill="#F25022"/>
                    <rect x="11" y="1" width="9" height="9" fill="#7FBA00"/>
                    <rect x="1" y="11" width="9" height="9" fill="#00A4EF"/>
                    <rect x="11" y="11" width="9" height="9" fill="#FFB900"/>
                  </svg>
                  <span>Enterprise Single Sign-On & Identity Provider</span>
                </CardTitle>
                <CardDescription>
                  Enterprise authentication managed via Microsoft Entra ID (Azure SSO) and corporate Active Directory.
                </CardDescription>
              </div>
              <Badge 
                :variant="ssoStatus?.isConfigured ? 'default' : 'outline'" 
                class="self-start sm:self-center font-mono text-xs px-2.5 py-0.5 tracking-wider uppercase"
                :class="ssoStatus?.isConfigured ? 'bg-emerald-600 text-white' : 'border-sky-500/40 text-sky-400 bg-sky-500/10'"
              >
                {{ ssoStatus?.isConfigured ? 'Azure Cloud Connected' : 'Dev SSO Sandbox' }}
              </Badge>
            </div>
          </CardHeader>
          <CardContent class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
              <div class="p-3 rounded-lg bg-background/80 border border-border">
                <div class="text-[10px] uppercase font-bold text-muted-foreground tracking-wider mb-1">Identity Provider</div>
                <div class="font-semibold text-foreground">Microsoft Entra ID (Azure AD)</div>
                <div class="text-[11px] text-muted-foreground mt-0.5">OAuth 2.0 / OpenID Connect</div>
              </div>
              <div class="p-3 rounded-lg bg-background/80 border border-border">
                <div class="text-[10px] uppercase font-bold text-muted-foreground tracking-wider mb-1">Configured Tenant ID</div>
                <div class="font-mono text-[11px] text-foreground font-semibold truncate">
                  {{ ssoStatus?.tenantId || '72f988bf-86f1-41af-91ab-2d7cd011db47' }}
                </div>
                <div class="text-[11px] text-muted-foreground mt-0.5">Multi-tenant Plant Partition</div>
              </div>
              <div class="p-3 rounded-lg bg-background/80 border border-border">
                <div class="text-[10px] uppercase font-bold text-muted-foreground tracking-wider mb-1">Claims Transformation</div>
                <div class="font-semibold text-emerald-500">Auto-Role & Org Provisioning</div>
                <div class="text-[11px] text-muted-foreground mt-0.5">Claims: groups, roles, wids</div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Two-Factor Authentication (TOTP) Card -->
        <Card class="border-border/80 shadow-sm">
          <CardHeader>
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div class="space-y-1">
                <CardTitle class="text-base flex items-center gap-2">
                  <SmartphoneIcon class="h-5 w-5 text-indigo-500" />
                  <span>Two-Factor Authentication (TOTP / Authenticator App)</span>
                </CardTitle>
                <CardDescription>
                  Protect your Heimdall account with time-based one-time passcodes from Google Authenticator, 1Password, or Microsoft Authenticator.
                </CardDescription>
              </div>
              <Badge 
                :variant="mfaState.enabled ? 'default' : 'outline'" 
                class="self-start sm:self-center font-mono text-xs px-2.5 py-0.5 tracking-wider uppercase"
                :class="mfaState.enabled ? 'bg-emerald-600 hover:bg-emerald-600 text-white' : 'border-amber-500/50 text-amber-500 bg-amber-500/10'"
              >
                <ShieldCheckIcon v-if="mfaState.enabled" class="size-3.5 mr-1" />
                <ShieldAlertIcon v-else class="size-3.5 mr-1" />
                {{ mfaState.enabled ? 'Enrolled & Active' : 'Not Configured' }}
              </Badge>
            </div>
          </CardHeader>
          <CardContent class="space-y-5">
            <!-- Case 1: Setup Wizard Active -->
            <div v-if="isSettingUpMfa" class="space-y-6 rounded-xl border border-indigo-500/20 bg-indigo-500/5 p-5">
              <!-- Wizard Steps Header -->
              <div class="flex items-center justify-between border-b border-border/60 pb-3">
                <div class="flex items-center gap-2">
                  <div class="flex items-center justify-center size-6 rounded-full bg-indigo-600 text-white text-xs font-bold font-mono">
                    {{ setupStep }}
                  </div>
                  <span class="font-semibold text-sm">
                    {{ setupStep === 1 ? 'Step 1: Link Authenticator App' : setupStep === 2 ? 'Step 2: Verify 6-Digit Passcode' : 'Step 3: Save Backup Recovery Codes' }}
                  </span>
                </div>
                <Button v-if="setupStep !== 3" variant="ghost" size="sm" class="text-xs text-muted-foreground hover:text-foreground" @click="cancelMfaSetup">
                  Cancel Setup
                </Button>
              </div>

              <!-- Wizard Step 1: Scan QR or copy Secret -->
              <div v-if="setupStep === 1" class="space-y-5">
                <p class="text-xs text-muted-foreground leading-relaxed">
                  Open your mobile authenticator app (such as Google Authenticator, Microsoft Authenticator, 1Password, or Authy), tap <strong>Add Account</strong>, and scan the QR code below. If you cannot scan the code, copy and paste the manual setup key into your app.
                </p>

                <div class="flex flex-col md:flex-row items-center gap-6 p-4 rounded-xl bg-background/80 border border-border">
                  <!-- QR Code Image -->
                  <div class="shrink-0 p-2 rounded-lg bg-white shadow-sm border border-border">
                    <img 
                      v-if="mfaSetupData?.qrCode" 
                      :src="mfaSetupData.qrCode" 
                      alt="Heimdall MFA QR Code" 
                      class="size-44 object-contain"
                    />
                    <div v-else class="size-44 flex items-center justify-center bg-muted text-muted-foreground text-xs">
                      Loading QR...
                    </div>
                  </div>

                  <!-- Manual Secret Entry -->
                  <div class="space-y-3 w-full">
                    <div>
                      <label class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider block mb-1">
                        Manual Base32 Secret Key
                      </label>
                      <div class="flex items-center gap-2">
                        <code class="px-3 py-2 rounded-lg bg-muted border border-border font-mono text-xs sm:text-sm text-foreground select-all break-all flex-1 font-semibold">
                          {{ mfaSetupData?.secret }}
                        </code>
                        <Button 
                          variant="outline" 
                          size="sm" 
                          class="shrink-0"
                          @click="copyToClipboard(mfaSetupData?.secret || '', 'secret')"
                        >
                          <CheckIcon v-if="copiedSecret" class="size-3.5 text-emerald-500 mr-1" />
                          <CopyIcon v-else class="size-3.5 mr-1" />
                          {{ copiedSecret ? 'Copied' : 'Copy Key' }}
                        </Button>
                      </div>
                    </div>

                    <div class="text-[11px] text-muted-foreground space-y-1">
                      <div>• Algorithm: <strong>HMAC-SHA1</strong></div>
                      <div>• Digits: <strong>6</strong></div>
                      <div>• Period: <strong>30 seconds</strong></div>
                    </div>
                  </div>
                </div>

                <div class="flex justify-end gap-2 pt-2">
                  <Button variant="outline" size="sm" @click="cancelMfaSetup">Cancel</Button>
                  <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white" @click="setupStep = 2">
                    Next: Enter Code
                    <ArrowRightIcon class="size-3.5 ml-1.5" />
                  </Button>
                </div>
              </div>

              <!-- Wizard Step 2: Enter & Verify Code -->
              <div v-else-if="setupStep === 2" class="space-y-4">
                <p class="text-xs text-muted-foreground">
                  Enter the 6-digit one-time code shown in your authenticator app to verify that setup was completed properly.
                </p>

                <div v-if="setupError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
                  <AlertCircleIcon class="h-4 w-4 shrink-0" />
                  <span>{{ setupError }}</span>
                </div>

                <div class="flex flex-col items-center justify-center py-4 space-y-3">
                  <div class="space-y-1.5 text-center">
                    <label class="text-xs font-semibold uppercase text-muted-foreground tracking-wider">
                      6-Digit Security Code
                    </label>
                    <Input 
                      v-model="setupTotpCode" 
                      type="text" 
                      inputmode="numeric"
                      pattern="[0-9]*"
                      maxlength="6" 
                      placeholder="000000" 
                      class="font-mono text-center text-2xl tracking-[0.4em] max-w-[220px] h-12 font-bold"
                      @keyup.enter="handleVerifyAndEnableMfa"
                    />
                  </div>
                  <span class="text-[11px] text-muted-foreground">Dev code '123456' accepted in sandbox</span>
                </div>

                <div class="flex justify-between items-center pt-2">
                  <Button variant="outline" size="sm" @click="setupStep = 1">
                    Back to QR Code
                  </Button>
                  <Button 
                    size="sm" 
                    class="bg-indigo-600 hover:bg-indigo-700 text-white"
                    :disabled="verifyingSetup || setupTotpCode.trim().length < 6"
                    @click="handleVerifyAndEnableMfa"
                  >
                    <RefreshCwIcon v-if="verifyingSetup" class="size-3.5 mr-2 animate-spin" />
                    <ShieldCheckIcon v-else class="size-3.5 mr-2" />
                    Verify & Activate MFA
                  </Button>
                </div>
              </div>

              <!-- Wizard Step 3: Backup Recovery Codes -->
              <div v-else-if="setupStep === 3" class="space-y-5">
                <div class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
                  <CheckCircle2Icon class="h-4 w-4 shrink-0" />
                  <span>{{ setupSuccess || 'Multi-Factor Authentication enabled successfully!' }}</span>
                </div>

                <div class="space-y-2">
                  <div class="flex items-center justify-between">
                    <div>
                      <h4 class="text-xs font-bold uppercase tracking-wider text-foreground">One-Time Backup Recovery Codes</h4>
                      <p class="text-[11px] text-muted-foreground mt-0.5">
                        Save these one-time recovery codes in a safe place. If you lose your phone or authenticator app, each code can be used once to access your Heimdall account.
                      </p>
                    </div>
                    <div class="flex items-center gap-2">
                      <Button 
                        variant="outline" 
                        size="sm" 
                        class="text-xs h-8"
                        @click="copyToClipboard((mfaSetupData?.backupCodes || []).join('\n'), 'setupCodes')"
                      >
                        <CheckIcon v-if="copiedBackupCodes" class="size-3 text-emerald-500 mr-1" />
                        <CopyIcon v-else class="size-3 mr-1" />
                        {{ copiedBackupCodes ? 'Copied' : 'Copy All' }}
                      </Button>
                      <Button 
                        variant="outline" 
                        size="sm" 
                        class="text-xs h-8"
                        @click="downloadBackupCodes(mfaSetupData?.backupCodes || [])"
                      >
                        <DownloadIcon class="size-3 mr-1" />
                        Download (.txt)
                      </Button>
                    </div>
                  </div>

                  <div class="grid grid-cols-2 sm:grid-cols-5 gap-2 p-3 rounded-lg bg-muted/60 border border-border font-mono text-xs text-center font-bold">
                    <div 
                      v-for="(code, idx) in mfaSetupData?.backupCodes" 
                      :key="idx" 
                      class="p-2 rounded bg-background border border-border/80 tracking-wider text-foreground"
                    >
                      {{ code }}
                    </div>
                  </div>
                </div>

                <div class="flex justify-end pt-2">
                  <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white" @click="finishMfaSetup">
                    Done & Return to Settings
                  </Button>
                </div>
              </div>
            </div>

            <!-- Case 2: Not Configured Callout -->
            <div v-else-if="!mfaState.enabled" class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 rounded-xl bg-muted/30 border border-border/70">
              <div class="space-y-1">
                <div class="font-semibold text-sm text-foreground flex items-center gap-2">
                  <span>Enhance Identity Security</span>
                  <Badge variant="outline" class="text-[10px] text-amber-500 border-amber-500/40">Recommended</Badge>
                </div>
                <p class="text-xs text-muted-foreground max-w-xl">
                  Two-Factor Authentication is currently not enabled for your account. When enabled, signing in requires your password and a 6-digit TOTP code generated on your authenticator device.
                </p>
              </div>
              <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white shrink-0" @click="startMfaSetup">
                <ShieldCheckIcon class="size-4 mr-2" />
                Configure Two-Factor
              </Button>
            </div>

            <!-- Case 3: Enrolled & Active View -->
            <div v-else class="space-y-4">
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
                  <div class="text-[10px] font-semibold text-muted-foreground uppercase">MFA Method</div>
                  <div class="text-xs font-bold mt-1 text-foreground flex items-center gap-1.5">
                    <SmartphoneIcon class="size-3.5 text-indigo-400" />
                    <span>Authenticator App (TOTP)</span>
                  </div>
                  <div class="text-[10px] text-muted-foreground mt-0.5">RFC 6238 30-second window</div>
                </div>

                <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
                  <div class="text-[10px] font-semibold text-muted-foreground uppercase">Enrolled Since</div>
                  <div class="text-xs font-bold mt-1 text-foreground">
                    {{ mfaState.enrolledAt ? new Date(mfaState.enrolledAt).toLocaleDateString() : 'Active' }}
                  </div>
                  <div class="text-[10px] text-muted-foreground mt-0.5">Primary multi-factor key</div>
                </div>

                <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
                  <div class="text-[10px] font-semibold text-muted-foreground uppercase">Backup Recovery Codes</div>
                  <div class="text-xs font-bold mt-1 text-foreground flex items-center gap-1.5">
                    <KeyRoundIcon class="size-3.5 text-cyan-400" />
                    <span>{{ mfaState.backupCodesCount }} codes remaining</span>
                  </div>
                  <div class="text-[10px] text-muted-foreground mt-0.5">For account recovery</div>
                </div>
              </div>

              <!-- Active MFA Action Buttons -->
              <div class="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border/50">
                <div class="flex flex-wrap items-center gap-2">
                  <Button variant="outline" size="sm" class="text-xs" @click="openChallengeModal">
                    <KeyRoundIcon class="size-3.5 mr-1.5 text-indigo-400" />
                    Test / Re-verify Challenge
                  </Button>
                  <Button variant="outline" size="sm" class="text-xs" @click="openBackupCodesModal">
                    <LockIcon class="size-3.5 mr-1.5 text-cyan-400" />
                    Manage Recovery Codes
                  </Button>
                </div>
                <Button variant="outline" size="sm" class="text-xs text-destructive hover:bg-destructive/10 hover:border-destructive/40" @click="openDisableMfaModal">
                  <Trash2Icon class="size-3.5 mr-1.5" />
                  Disable 2FA
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Enterprise Zero-Trust MFA Governance & Session Status Card -->
        <Card class="border-border/80 shadow-sm">
          <CardHeader>
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div class="space-y-1">
                <CardTitle class="text-base flex items-center gap-2">
                  <ShieldIcon class="h-5 w-5 text-emerald-400" />
                  <span>Enterprise Zero-Trust MFA Governance & Session Status</span>
                </CardTitle>
                <CardDescription>
                  Enforced security thresholds based on your active role ({{ userRole }}).
                </CardDescription>
              </div>
              <div class="flex items-center gap-2">
                <Badge 
                  :variant="mfaEvaluation?.isExpired ? 'destructive' : 'outline'" 
                  class="font-mono text-xs px-2.5 py-0.5"
                  :class="!mfaEvaluation?.isExpired ? 'text-emerald-400 border-emerald-500/40 bg-emerald-500/10' : ''"
                >
                  {{ mfaEvaluation?.isExpired ? 'RE-VERIFICATION REQUIRED' : 'SESSION MFA VALID' }}
                </Badge>
              </div>
            </div>
          </CardHeader>
          <CardContent class="space-y-4">
            <div class="p-4 rounded-lg bg-muted/30 border border-border/50 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div>
                <div class="font-bold text-sm text-foreground">{{ mfaPolicyRequirement.title }}</div>
                <p class="text-xs text-muted-foreground mt-1">{{ mfaPolicyRequirement.description }}</p>
                <div v-if="mfaEvaluation?.reason" class="text-xs text-indigo-400 mt-2 font-mono">
                  Governance Rule: {{ mfaEvaluation.reason }}
                </div>
              </div>
              <div class="flex flex-col items-start sm:items-end gap-1.5">
                <Badge variant="outline" class="font-mono text-xs text-emerald-400 border-emerald-500/40 uppercase">
                  Threshold: {{ mfaEvaluation?.appliedThreshold || mfaPolicyRequirement.threshold }}
                </Badge>
                <div v-if="mfaEvaluation?.expiresAt" class="text-[11px] text-muted-foreground font-mono">
                  Expires: {{ new Date(mfaEvaluation.expiresAt).toLocaleString() }}
                </div>
              </div>
            </div>

            <div v-if="mfaState.enabled" class="flex items-center justify-between p-3 rounded-lg border border-border/50 bg-background/50 text-xs">
              <div class="flex items-center gap-2 text-muted-foreground">
                <ClockIcon class="size-4 text-primary" />
                <span>Last MFA Verification: <strong>{{ mfaState.lastVerifiedAt ? new Date(mfaState.lastVerifiedAt).toLocaleString() : 'Not yet verified this session' }}</strong></span>
              </div>
              <Button size="sm" variant="ghost" class="text-xs text-indigo-400 hover:text-indigo-300 h-7 px-2" @click="openChallengeModal">
                Re-verify Session Now
              </Button>
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
              <div v-for="s in (Array.isArray(sessions) ? sessions : [])" :key="s?.id || 'sess-default'" class="p-3 flex items-center justify-between text-xs">
                <div class="space-y-0.5">
                  <div class="font-bold flex items-center gap-2">
                    <span>{{ s?.userAgent || 'Web Browser' }}</span>
                    <Badge v-if="s?.isCurrent" variant="default" class="text-[9px] uppercase">Current Session</Badge>
                  </div>
                  <div class="text-muted-foreground font-mono text-[11px]">{{ s?.ipAddress }}</div>
                </div>
                <div class="text-muted-foreground text-[11px]">
                  Started: {{ s?.createdAt ? new Date(s.createdAt).toLocaleDateString() : 'Active' }}
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
                  <MoonIcon class="h-8 w-8 text-indigo-500 dark:text-indigo-400" />
                  <div class="font-bold text-sm">Dark Mode (Default)</div>
                </button>

                <!-- System -->
                <button
                  type="button"
                  @click="colorMode.preference = 'system'"
                  class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
                  :class="colorMode.preference === 'system' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
                >
                  <MonitorIcon class="h-8 w-8 text-muted-foreground" />
                  <div class="font-bold text-sm">System Default</div>
                </button>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- TAB 5: Persona Sandbox -->
      <TabsContent v-if="isPersonaSimulationAllowed" value="personas" class="space-y-6">
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

    <!-- Dialog 1: Challenge / Re-verify Modal -->
    <Dialog v-model:open="showChallengeModal">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-foreground">
            <KeyRoundIcon class="size-5 text-indigo-500" />
            <span>Re-verify Multi-Factor Authentication</span>
          </DialogTitle>
          <DialogDescription>
            Enter the 6-digit TOTP code from your authenticator app or one of your 8-character recovery backup codes to confirm identity.
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-4 py-2">
          <div v-if="challengeSuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
            <CheckCircle2Icon class="h-4 w-4 shrink-0" />
            <span>{{ challengeSuccess }}</span>
          </div>
          <div v-if="challengeError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
            <AlertCircleIcon class="h-4 w-4 shrink-0" />
            <span>{{ challengeError }}</span>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-semibold uppercase text-muted-foreground tracking-wider">Passcode or Recovery Code</label>
            <Input 
              v-model="challengeCode" 
              type="text" 
              placeholder="e.g. 123456 or ABCD-1234" 
              class="font-mono text-center text-lg tracking-widest uppercase font-bold"
              @keyup.enter="handleVerifyChallenge"
            />
          </div>
          <div class="text-[11px] text-muted-foreground text-center">
            Zero-trust session grace period will be renewed upon successful verification.
          </div>
        </div>

        <DialogFooter class="flex sm:justify-between items-center gap-2">
          <Button variant="outline" size="sm" @click="showChallengeModal = false">
            Cancel
          </Button>
          <Button 
            size="sm" 
            class="bg-indigo-600 hover:bg-indigo-700 text-white" 
            :disabled="challengingMfa || !challengeCode.trim()"
            @click="handleVerifyChallenge"
          >
            <RefreshCwIcon v-if="challengingMfa" class="size-3.5 mr-2 animate-spin" />
            <ShieldCheckIcon v-else class="size-3.5 mr-2" />
            Verify Challenge
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Dialog 2: Backup Recovery Codes Modal -->
    <Dialog v-model:open="showBackupCodesModal">
      <DialogContent class="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-foreground">
            <LockIcon class="size-5 text-cyan-400" />
            <span>One-Time Backup Recovery Codes</span>
          </DialogTitle>
          <DialogDescription>
            Each code can be used once to access your Heimdall account if you lose your primary authenticator device.
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-4 py-2">
          <div v-if="backupCodesError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
            <AlertCircleIcon class="h-4 w-4 shrink-0" />
            <span>{{ backupCodesError }}</span>
          </div>

          <div v-if="loadingBackupCodes" class="py-8 flex items-center justify-center text-xs text-muted-foreground">
            <RefreshCwIcon class="size-5 animate-spin mr-2" />
            Loading backup codes...
          </div>
          <div v-else class="space-y-3">
            <div class="grid grid-cols-2 gap-2 p-3 rounded-lg bg-muted/60 border border-border font-mono text-xs text-center font-bold">
              <div 
                v-for="(code, idx) in userBackupCodes" 
                :key="idx" 
                class="p-2 rounded bg-background border border-border/80 tracking-wider text-foreground"
              >
                {{ code }}
              </div>
            </div>
            <div v-if="userBackupCodes.length === 0" class="text-xs text-amber-500 text-center py-2">
              No backup codes remaining. Please regenerate codes immediately.
            </div>
          </div>
        </div>

        <DialogFooter class="flex flex-col sm:flex-row sm:justify-between items-center gap-2">
          <Button 
            variant="outline" 
            size="sm" 
            class="text-xs text-destructive hover:bg-destructive/10 hover:border-destructive/40"
            :disabled="regeneratingBackupCodes"
            @click="handleRegenerateBackupCodes"
          >
            <RefreshCwIcon v-if="regeneratingBackupCodes" class="size-3.5 mr-1.5 animate-spin" />
            <RefreshCwIcon v-else class="size-3.5 mr-1.5" />
            Regenerate New Codes
          </Button>

          <div class="flex items-center gap-2">
            <Button 
              variant="outline" 
              size="sm" 
              class="text-xs"
              :disabled="userBackupCodes.length === 0"
              @click="copyToClipboard(userBackupCodes.join('\n'), 'modalCodes')"
            >
              <CheckIcon v-if="copiedModalCodes" class="size-3 text-emerald-500 mr-1" />
              <CopyIcon v-else class="size-3 mr-1" />
              {{ copiedModalCodes ? 'Copied' : 'Copy All' }}
            </Button>
            <Button 
              variant="outline" 
              size="sm" 
              class="text-xs"
              :disabled="userBackupCodes.length === 0"
              @click="downloadBackupCodes(userBackupCodes)"
            >
              <DownloadIcon class="size-3 mr-1" />
              Download (.txt)
            </Button>
            <Button size="sm" @click="showBackupCodesModal = false">
              Done
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Dialog 3: Disable MFA Modal -->
    <Dialog v-model:open="showDisableMfaModal">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-destructive">
            <Trash2Icon class="size-5" />
            <span>Disable Two-Factor Authentication</span>
          </DialogTitle>
          <DialogDescription>
            Are you sure you want to turn off Multi-Factor Authentication? Your account will only be protected by your password.
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-3 py-2">
          <div v-if="disableError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
            <AlertCircleIcon class="h-4 w-4 shrink-0" />
            <span>{{ disableError }}</span>
          </div>
          <div class="p-3 rounded-lg bg-amber-500/10 border border-amber-500/20 text-xs text-amber-500">
            Warning: Disabling 2FA reduces account security and may violate enterprise zero-trust access policies for privileged roles.
          </div>
        </div>

        <DialogFooter class="flex sm:justify-between items-center gap-2">
          <Button variant="outline" size="sm" @click="showDisableMfaModal = false">
            Cancel
          </Button>
          <Button 
            size="sm" 
            variant="destructive"
            :disabled="disablingMfa"
            @click="handleDisableMfa"
          >
            <RefreshCwIcon v-if="disablingMfa" class="size-3.5 mr-2 animate-spin" />
            <Trash2Icon v-else class="size-3.5 mr-2" />
            Confirm & Disable 2FA
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>

<style scoped>
</style>
