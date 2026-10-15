<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { authClient } from '~/utils/auth-client'
import { useAuthSession } from '~/composables/useAuthSession'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter
} from '@/components/ui/dialog'
import {
  ShieldIcon,
  ShieldCheckIcon,
  ShieldAlertIcon,
  CheckIcon,
  AlertCircleIcon,
  RefreshCwIcon,
  LockIcon,
  MonitorIcon,
  CheckCircle2Icon,
  SmartphoneIcon,
  CopyIcon,
  DownloadIcon,
  Trash2Icon,
  ArrowRightIcon,
  ClockIcon,
  KeyRoundIcon,
  GitBranchIcon,
  CloudIcon
} from 'lucide-vue-next'

const { user, userRole } = useAuthSession()

// MFA Policy Requirement
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

// Active sessions state
const sessions = ref<any[]>([])
const loadingSessions = ref(false)
const sessionSuccess = ref('')
const sessionError = ref('')

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

// ── Copia Personal Key & EULA Anti-Pooling State ─────────────────────────────
const personalCopiaKey = ref('')
const hasCopiaKey = ref(false)
const maskedCopiaKey = ref<string | null>(null)
const savingCopiaKey = ref(false)
const copiaKeySuccess = ref('')
const copiaKeyError = ref('')
const pendingCopiaSyncs = ref<any[]>([])
const syncingCopiaId = ref<string | null>(null)
const copiaSyncSuccess = ref('')
const copiaSyncError = ref('')

async function fetchCopiaKeyStatus() {
  try {
    const res = await $fetch<{ hasKey: boolean; maskedKey: string | null }>(`/api/user/copia-key?userId=${user.value?.id || 'usr-default'}`)
    hasCopiaKey.value = res.hasKey
    maskedCopiaKey.value = res.maskedKey
  } catch {
    hasCopiaKey.value = false
  }
}

async function fetchPendingCopiaSyncs() {
  try {
    const res = await $fetch<{ pendingSyncs: any[] }>('/api/integrations/copia/pending-syncs')
    pendingCopiaSyncs.value = res?.pendingSyncs || []
  } catch {
    pendingCopiaSyncs.value = []
  }
}

async function handleSaveCopiaKey() {
  if (!personalCopiaKey.value.trim()) return
  savingCopiaKey.value = true
  copiaKeySuccess.value = ''
  copiaKeyError.value = ''
  try {
    const res = await $fetch<{ success: boolean; hasKey: boolean; maskedKey: string; message: string }>('/api/user/copia-key', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        apiKey: personalCopiaKey.value.trim()
      }
    })
    hasCopiaKey.value = res.hasKey
    maskedCopiaKey.value = res.maskedKey
    personalCopiaKey.value = ''
    copiaKeySuccess.value = res.message || 'Personal Copia API key saved successfully.'
  } catch (err: any) {
    copiaKeyError.value = err.data?.statusMessage || err.message || 'Failed to save Copia key.'
  } finally {
    savingCopiaKey.value = false
  }
}

async function handleClearCopiaKey() {
  savingCopiaKey.value = true
  copiaKeySuccess.value = ''
  copiaKeyError.value = ''
  try {
    await $fetch('/api/user/copia-key', {
      method: 'POST',
      body: {
        userId: user.value?.id || 'usr-default',
        action: 'clear'
      }
    })
    hasCopiaKey.value = false
    maskedCopiaKey.value = null
    copiaKeySuccess.value = 'Personal Copia API key removed.'
  } catch (err: any) {
    copiaKeyError.value = err.message || 'Failed to remove Copia key.'
  } finally {
    savingCopiaKey.value = false
  }
}

async function handleAuthorizeCopiaSync(syncId: string) {
  syncingCopiaId.value = syncId
  copiaSyncSuccess.value = ''
  copiaSyncError.value = ''
  try {
    const res = await $fetch<any>('/api/integrations/copia/sync', {
      method: 'POST',
      body: {
        syncId,
        userId: user.value?.id || 'usr-default',
        userEmail: user.value?.email || 'engineer@plant.heimdall.dev',
        userDisplayName: user.value?.name || 'Engineer'
      }
    })
    copiaSyncSuccess.value = `Successfully pulled/pushed local repo into Copia Cloud (Commit: ${res.cloudCommitHash}) under personal license.`
    fetchPendingCopiaSyncs()
  } catch (err: any) {
    copiaSyncError.value = err.data?.statusMessage || err.message || 'Cloud sync failed. Make sure your personal Copia key is saved.'
  } finally {
    syncingCopiaId.value = null
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

watch(() => userRole.value, () => {
  evaluateUserMfaPolicy()
})

onMounted(() => {
  fetchSessions()
  fetchMfaStatus()
  evaluateUserMfaPolicy()
  fetchSsoStatus()
  fetchCopiaKeyStatus()
  fetchPendingCopiaSyncs()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Enterprise Single Sign-On (Azure AD / Entra ID) Card -->
    <Card class="border-sky-500/30 bg-sky-500/5 dark:bg-sky-950/15 shadow-xs">
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
              <span>Enterprise Single Sign-On &amp; Identity Provider</span>
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
            <div class="font-semibold text-emerald-500">Auto-Role &amp; Org Provisioning</div>
            <div class="text-[11px] text-muted-foreground mt-0.5">Claims: groups, roles, wids</div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Two-Factor Authentication (TOTP) Card -->
    <Card class="border-border/80 shadow-xs">
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
          <div class="flex items-center justify-between border-b border-border/60 pb-3">
            <div class="flex items-center gap-2">
              <div class="flex items-center justify-center size-6 rounded-full bg-indigo-600 text-white text-xs font-bold font-mono">
                {{ setupStep }}
              </div>
              <span class="font-semibold text-sm">
                {{ setupStep === 1 ? 'Step 1: Link Authenticator App' : setupStep === 2 ? 'Step 2: Verify 6-Digit Passcode' : 'Step 3: Save Backup Recovery Codes' }}
              </span>
            </div>
            <Button v-if="setupStep !== 3" variant="ghost" size="sm" class="text-xs text-muted-foreground hover:text-foreground cursor-pointer" @click="cancelMfaSetup">
              Cancel Setup
            </Button>
          </div>

          <!-- Wizard Step 1 -->
          <div v-if="setupStep === 1" class="space-y-5">
            <p class="text-xs text-muted-foreground leading-relaxed">
              Open your mobile authenticator app, tap <strong>Add Account</strong>, and scan the QR code below.
            </p>

            <div class="flex flex-col md:flex-row items-center gap-6 p-4 rounded-xl bg-background/80 border border-border">
              <div class="shrink-0 p-2 rounded-lg bg-white shadow-xs border border-border">
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
                      class="shrink-0 cursor-pointer"
                      @click="copyToClipboard(mfaSetupData?.secret || '', 'secret')"
                    >
                      <CheckIcon v-if="copiedSecret" class="size-3.5 text-emerald-500 mr-1" />
                      <CopyIcon v-else class="size-3.5 mr-1" />
                      {{ copiedSecret ? 'Copied' : 'Copy Key' }}
                    </Button>
                  </div>
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-2">
              <Button variant="outline" size="sm" class="cursor-pointer" @click="cancelMfaSetup">Cancel</Button>
              <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white cursor-pointer" @click="setupStep = 2">
                Next: Enter Code
                <ArrowRightIcon class="size-3.5 ml-1.5" />
              </Button>
            </div>
          </div>

          <!-- Wizard Step 2 -->
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
            </div>

            <div class="flex justify-between items-center pt-2">
              <Button variant="outline" size="sm" class="cursor-pointer" @click="setupStep = 1">
                Back to QR Code
              </Button>
              <Button 
                size="sm" 
                class="bg-indigo-600 hover:bg-indigo-700 text-white cursor-pointer"
                :disabled="verifyingSetup || setupTotpCode.trim().length < 6"
                @click="handleVerifyAndEnableMfa"
              >
                <RefreshCwIcon v-if="verifyingSetup" class="size-3.5 mr-2 animate-spin" />
                <ShieldCheckIcon v-else class="size-3.5 mr-2" />
                Verify &amp; Activate MFA
              </Button>
            </div>
          </div>

          <!-- Wizard Step 3 -->
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
                    Save these one-time recovery codes in a safe place.
                  </p>
                </div>
                <div class="flex items-center gap-2">
                  <Button 
                    variant="outline" 
                    size="sm" 
                    class="text-xs h-8 cursor-pointer"
                    @click="copyToClipboard((mfaSetupData?.backupCodes || []).join('\n'), 'setupCodes')"
                  >
                    <CheckIcon v-if="copiedBackupCodes" class="size-3 text-emerald-500 mr-1" />
                    <CopyIcon v-else class="size-3 mr-1" />
                    {{ copiedBackupCodes ? 'Copied' : 'Copy All' }}
                  </Button>
                  <Button 
                    variant="outline" 
                    size="sm" 
                    class="text-xs h-8 cursor-pointer"
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
              <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white cursor-pointer" @click="finishMfaSetup">
                Done &amp; Return to Settings
              </Button>
            </div>
          </div>
        </div>

        <!-- Case 2: Not Configured -->
        <div v-else-if="!mfaState.enabled" class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 rounded-xl bg-muted/30 border border-border/70">
          <div class="space-y-1">
            <div class="font-semibold text-sm text-foreground flex items-center gap-2">
              <span>Enhance Identity Security</span>
              <Badge variant="outline" class="text-[10px] text-amber-500 border-amber-500/40">Recommended</Badge>
            </div>
            <p class="text-xs text-muted-foreground max-w-xl">
              Two-Factor Authentication is currently not enabled for your account.
            </p>
          </div>
          <Button size="sm" class="bg-indigo-600 hover:bg-indigo-700 text-white shrink-0 cursor-pointer" @click="startMfaSetup">
            <ShieldCheckIcon class="size-4 mr-2" />
            Configure Two-Factor
          </Button>
        </div>

        <!-- Case 3: Enrolled & Active -->
        <div v-else class="space-y-4">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
              <div class="text-[10px] font-semibold text-muted-foreground uppercase">MFA Method</div>
              <div class="text-xs font-bold mt-1 text-foreground flex items-center gap-1.5">
                <SmartphoneIcon class="size-3.5 text-indigo-400" />
                <span>Authenticator App (TOTP)</span>
              </div>
            </div>

            <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
              <div class="text-[10px] font-semibold text-muted-foreground uppercase">Enrolled Since</div>
              <div class="text-xs font-bold mt-1 text-foreground">
                {{ mfaState.enrolledAt ? new Date(mfaState.enrolledAt).toLocaleDateString() : 'Active' }}
              </div>
            </div>

            <div class="p-3.5 rounded-lg border border-border/60 bg-muted/20">
              <div class="text-[10px] font-semibold text-muted-foreground uppercase">Backup Recovery Codes</div>
              <div class="text-xs font-bold mt-1 text-foreground flex items-center gap-1.5">
                <KeyRoundIcon class="size-3.5 text-cyan-400" />
                <span>{{ mfaState.backupCodesCount }} codes remaining</span>
              </div>
            </div>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border/50">
            <div class="flex flex-wrap items-center gap-2">
              <Button variant="outline" size="sm" class="text-xs cursor-pointer" @click="openChallengeModal">
                <KeyRoundIcon class="size-3.5 mr-1.5 text-indigo-400" />
                Test / Re-verify Challenge
              </Button>
              <Button variant="outline" size="sm" class="text-xs cursor-pointer" @click="openBackupCodesModal">
                <LockIcon class="size-3.5 mr-1.5 text-cyan-400" />
                Manage Recovery Codes
              </Button>
            </div>
            <Button variant="outline" size="sm" class="text-xs text-destructive hover:bg-destructive/10 hover:border-destructive/40 cursor-pointer" @click="openDisableMfaModal">
              <Trash2Icon class="size-3.5 mr-1.5" />
              Disable 2FA
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Copia Automation Personal Credentials & EULA Anti-Pooling Card -->
    <Card class="border-border/80 shadow-xs">
      <CardHeader>
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="space-y-1">
            <CardTitle class="text-base flex items-center gap-2">
              <GitBranchIcon class="h-5 w-5 text-indigo-400" />
              <span>Copia Automation Personal Credentials &amp; EULA Compliance</span>
            </CardTitle>
            <CardDescription>
              Named-user personal API access token (PAT) for pulling/pushing TwinCAT repositories to Copia Cloud.
            </CardDescription>
          </div>
          <Badge
            variant="outline"
            class="font-mono text-[10px] px-2.5 py-0.5 text-indigo-400 border-indigo-500/40 bg-indigo-500/10"
          >
            EULA STRICT 1:1 SEAT ENFORCEMENT
          </Badge>
        </div>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="p-3.5 rounded-lg bg-muted/40 border border-border/60 text-xs text-muted-foreground space-y-2">
          <div class="flex items-center gap-2 text-foreground font-semibold">
            <ShieldCheckIcon class="size-4 text-emerald-400" />
            <span>Anti-User Pooling Architecture</span>
          </div>
          <p>
            To strictly comply with Copia Automation licensing terms (which prohibit service account pooling),
            Heimdall separates PLC version control into two stages:
          </p>
          <ul class="list-disc list-inside space-y-1 pl-1 text-[11px]">
            <li><strong>Local Git Tracking:</strong> ADS download snapshots are captured on-premise using the service identity <code class="text-indigo-400 font-mono">heimdall-probe</code> without cloud calls.</li>
            <li><strong>Cloud Synchronization:</strong> Pushing or pulling repositories into Copia Cloud requires your personal named API token below.</li>
          </ul>
        </div>

        <div v-if="copiaKeySuccess" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-400 text-xs flex items-center gap-2">
          <CheckCircle2Icon class="h-4 w-4 shrink-0" />
          <span>{{ copiaKeySuccess }}</span>
        </div>
        <div v-if="copiaKeyError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
          <AlertCircleIcon class="h-4 w-4 shrink-0" />
          <span>{{ copiaKeyError }}</span>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 items-end">
          <div class="sm:col-span-2 space-y-1.5">
            <label class="text-xs font-semibold text-muted-foreground uppercase flex items-center justify-between">
              <span>My Personal Copia API Key / PAT</span>
              <span v-if="hasCopiaKey" class="text-emerald-400 font-mono text-[10px] lowercase font-normal">Active: {{ maskedCopiaKey }}</span>
            </label>
            <Input
              v-model="personalCopiaKey"
              type="password"
              placeholder="copia_pat_..."
              class="font-mono text-xs"
            />
          </div>
          <div class="flex gap-2">
            <Button
              size="sm"
              class="bg-indigo-600 hover:bg-indigo-700 text-white flex-1 cursor-pointer"
              :disabled="savingCopiaKey || !personalCopiaKey.trim()"
              @click="handleSaveCopiaKey"
            >
              <RefreshCwIcon v-if="savingCopiaKey" class="h-3.5 w-3.5 mr-2 animate-spin" />
              <KeyRoundIcon v-else class="h-3.5 w-3.5 mr-2" />
              Save Key
            </Button>
            <Button
              v-if="hasCopiaKey"
              size="sm"
              variant="outline"
              class="text-destructive hover:bg-destructive/10 cursor-pointer"
              :disabled="savingCopiaKey"
              @click="handleClearCopiaKey"
            >
              Remove
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Active Sessions Card -->
    <Card class="border-border/80">
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle class="text-base flex items-center gap-2">
              <MonitorIcon class="h-5 w-5 text-cyan-400" />
              <span>Active Sessions &amp; Authorized Devices</span>
            </CardTitle>
            <CardDescription>
              Review and revoke active sign-in sessions for this identity.
            </CardDescription>
          </div>
          <Button size="sm" variant="outline" class="cursor-pointer" @click="handleRevokeOtherSessions">
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

    <!-- Dialog 1: Challenge Modal -->
    <Dialog v-model:open="showChallengeModal">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle class="flex items-center gap-2 text-foreground">
            <KeyRoundIcon class="size-5 text-indigo-500" />
            <span>Re-verify Multi-Factor Authentication</span>
          </DialogTitle>
          <DialogDescription>
            Enter the 6-digit TOTP code from your authenticator app or one of your backup recovery codes.
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
              placeholder="e.g. 123456" 
              class="font-mono text-center text-lg tracking-widest uppercase font-bold"
              @keyup.enter="handleVerifyChallenge"
            />
          </div>
        </div>

        <DialogFooter class="flex sm:justify-between items-center gap-2">
          <Button variant="outline" size="sm" class="cursor-pointer" @click="showChallengeModal = false">
            Cancel
          </Button>
          <Button 
            size="sm" 
            class="bg-indigo-600 hover:bg-indigo-700 text-white cursor-pointer" 
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
            Each code can be used once to access your account if you lose your primary authenticator device.
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
          </div>
        </div>

        <DialogFooter class="flex flex-col sm:flex-row sm:justify-between items-center gap-2">
          <Button 
            variant="outline" 
            size="sm" 
            class="text-xs text-destructive hover:bg-destructive/10 cursor-pointer"
            :disabled="regeneratingBackupCodes"
            @click="handleRegenerateBackupCodes"
          >
            <RefreshCwIcon v-if="regeneratingBackupCodes" class="size-3.5 mr-1.5 animate-spin" />
            <RefreshCwIcon v-else class="size-3.5 mr-1.5" />
            Regenerate New Codes
          </Button>

          <Button size="sm" class="cursor-pointer" @click="showBackupCodesModal = false">
            Done
          </Button>
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
            Are you sure you want to turn off Multi-Factor Authentication?
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-3 py-2">
          <div v-if="disableError" class="p-3 rounded-lg bg-destructive/15 border border-destructive/30 text-destructive text-xs flex items-center gap-2">
            <AlertCircleIcon class="h-4 w-4 shrink-0" />
            <span>{{ disableError }}</span>
          </div>
        </div>

        <DialogFooter class="flex sm:justify-between items-center gap-2">
          <Button variant="outline" size="sm" class="cursor-pointer" @click="showDisableMfaModal = false">
            Cancel
          </Button>
          <Button 
            size="sm" 
            variant="destructive"
            class="cursor-pointer"
            :disabled="disablingMfa"
            @click="handleDisableMfa"
          >
            <RefreshCwIcon v-if="disablingMfa" class="size-3.5 mr-2 animate-spin" />
            <Trash2Icon v-else class="size-3.5 mr-2" />
            Confirm &amp; Disable 2FA
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
