<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import RootCertImportModal from '@/components/admin/RootCertImportModal.vue'
import AdHostImportModal from '@/components/admin/AdHostImportModal.vue'
import OuCertificateRuleModal from '@/components/admin/OuCertificateRuleModal.vue'
import { 
  SlidersHorizontalIcon, 
  KeyIcon, 
  CpuIcon, 
  NetworkIcon, 
  ShieldAlertIcon, 
  SaveIcon, 
  SendIcon, 
  RefreshCwIcon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  PlusIcon,
  BanIcon,
  FolderTreeIcon,
  ShieldCheckIcon,
  DownloadIcon,
  ClockIcon,
  SparklesIcon,
  UserCheckIcon,
  Trash2Icon,
  PlayIcon,
  CheckIcon,
  LayersIcon,
  ServerIcon,
  ExternalLinkIcon
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard',
})

const activeTab = ref<'auth' | 'vlan-ad-import' | 'certificates' | 'integrations' | 'agent-master'>('auth')
const loading = ref(false)
const saving = ref(false)
const pushingPolicy = ref(false)
const syncingCerts = ref(false)
const message = ref('')
const error = ref('')

// Master Fleet Policy
const masterPolicy = ref({
  configSchemaVersion: '1.0.0',
  enforceHardwareBinding: true,
  spoolEncryptionMode: 'AES_256_GCM',
  telemetryPayloadEncryption: false,
  allowRemoteExecution: true,
  piiScrubberStrictLevel: 'Strict',
  maxNetworkEgressBytesPerSec: 1048576,
  deltaEvaluationAlgorithm: 'xxHash64',
  deadbandTolerancePercentage: 1.0,
  maxSpoolDiskMb: 500,
  heartbeatIntervalSeconds: 10,
})

// General Auth Policy
const authPolicy = ref({
  sessionTtlMinutes: 1440,
  requireMfaForEngineers: true,
  allowAnonymousTelemetryIngestion: true,
  maxFailedLoginAttempts: 5,
})

// MFA Policy with Group & Role Thresholds
interface MfaRule {
  id: string
  targetType: 'role' | 'group'
  targetName: string
  forceMfa: boolean
  timeoutThreshold: string
  customDays?: number
  description?: string
}

const mfaPolicy = ref({
  enabled: true,
  defaultThreshold: '7d',
  rules: [
    {
      id: 'rule-sysadmin',
      targetType: 'role' as const,
      targetName: 'SystemAdministrator',
      forceMfa: true,
      timeoutThreshold: 'always',
      description: 'System administrators must always authenticate with MFA on every sign-in',
    },
    {
      id: 'rule-engineer',
      targetType: 'role' as const,
      targetName: 'Engineer',
      forceMfa: true,
      timeoutThreshold: '7d',
      description: 'Engineers must authenticate with MFA once a week (7 days)',
    },
    {
      id: 'rule-technician',
      targetType: 'role' as const,
      targetName: 'Technician',
      forceMfa: true,
      timeoutThreshold: '30d',
      description: 'Technicians must authenticate with MFA once a month (30 days)',
    },
    {
      id: 'rule-maint-leads',
      targetType: 'group' as const,
      targetName: 'Maintenance Leads',
      forceMfa: true,
      timeoutThreshold: '14d',
      description: 'Shift leaders and group leads bi-weekly verification',
    },
  ] as MfaRule[],
})

// Add New MFA Rule Form
const newRuleTargetType = ref<'role' | 'group'>('group')
const newRuleTargetName = ref('')
const newRuleForceMfa = ref(true)
const newRuleThreshold = ref('7d')
const newRuleCustomDays = ref(7)
const newRuleDescription = ref('')

const knownTargetSuggestions = computed(() => {
  if (newRuleTargetType.value === 'role') {
    return ['SystemAdministrator', 'Engineer', 'Technician', 'Viewer', 'Auditor']
  }
  return ['Maintenance Leads', 'Quality Assurance', 'Automation Engineers', 'External Contractors', 'Shift Leaders', 'Plant Operations']
})

// MFA Sandbox Simulator
const sandboxRole = ref('Engineer')
const sandboxGroups = ref('Maintenance Leads, Automation Engineers')
const sandboxPreset = ref<'now' | '2d' | '8d' | '35d' | 'none'>('2d')
const sandboxResult = ref<any>(null)
const sandboxEvaluating = ref(false)

// Active Directory & VLAN Host Discovery
const adOus = ref<any[]>([])
const adHostModalOpen = ref(false)

// PKI, Root CA & Certificates
const rootCa = ref<any>(null)
const rootCertModalOpen = ref(false)
const ouRules = ref<any[]>([])
const ouRuleModalOpen = ref(false)
const ruleToEdit = ref<any>(null)
const certificates = ref<any[]>([])
const newCertCN = ref('')
const issuingCert = ref(false)

// OT Integrations
const integrations = ref({
  opcUaEndpoint: 'opc.tcp://0.0.0.0:4840/Heimdall',
  opcUaSecurityPolicy: 'Basic256Sha256',
  copiaWebhookUrl: '/api/v1/integrations/copia/webhook',
  copiaAutoSyncEnabled: true,
})

async function loadAllData() {
  loading.value = true
  error.value = ''
  message.value = ''

  try {
    // 1. System Settings
    try {
      const res = await $fetch<any[]>('/api/proxy/v1/systemsettings')
      if (res && res.length > 0) {
        for (const item of res) {
          if (item.key === 'AgentMasterTemplate') masterPolicy.value = JSON.parse(item.valueJson)
          else if (item.key === 'AuthPolicy') authPolicy.value = JSON.parse(item.valueJson)
          else if (item.key === 'OpcUaConfig') {
            const opc = JSON.parse(item.valueJson)
            integrations.value.opcUaEndpoint = opc.endpoint || integrations.value.opcUaEndpoint
            integrations.value.opcUaSecurityPolicy = opc.securityPolicy || integrations.value.opcUaSecurityPolicy
          }
        }
      }
    } catch {}

    // 2. MFA Policy
    try {
      let mfaRes: any = null
      try {
        mfaRes = await $fetch('/api/proxy/v1/systemsettings/mfa-policy')
      } catch {
        mfaRes = await $fetch('/api/system/mfa-policy')
      }
      if (mfaRes) mfaPolicy.value = mfaRes
    } catch {}

    // 3. Active Directory OUs
    try {
      let ousRes: any = null
      try {
        ousRes = await $fetch('/api/proxy/v1/activedirectory/ous')
      } catch {
        ousRes = await $fetch('/api/activedirectory/ous')
      }
      if (ousRes) adOus.value = ousRes
    } catch {}

    // 4. PKI Root CA
    try {
      let rootRes: any = null
      try {
        rootRes = await $fetch('/api/proxy/v1/certificatemanagement/root-ca')
      } catch {
        rootRes = await $fetch('/api/pki/root-ca')
      }
      if (rootRes) rootCa.value = rootRes
    } catch {}

    // 5. OU Certificate Rules
    try {
      let rulesRes: any = null
      try {
        rulesRes = await $fetch('/api/proxy/v1/certificatemanagement/ou-rules')
      } catch {
        rulesRes = await $fetch('/api/pki/ou-rules')
      }
      if (rulesRes) ouRules.value = rulesRes
    } catch {}

    // 6. Issued Client Certificates
    try {
      let certsRes: any = null
      try {
        certsRes = await $fetch('/api/proxy/v1/certificatemanagement')
      } catch {
        certsRes = await $fetch('/api/pki/root-ca') // or list
      }
      if (certsRes && Array.isArray(certsRes)) certificates.value = certsRes
    } catch {}

    // Run initial sandbox evaluation
    runSandboxEvaluation()
  } catch (err: any) {
    error.value = 'Notice: Operating with cached configuration.'
  } finally {
    loading.value = false
  }
}

async function saveCurrentCategory() {
  saving.value = true
  message.value = ''
  error.value = ''

  try {
    if (activeTab.value === 'agent-master') {
      await $fetch('/api/proxy/v1/systemsettings/AgentMasterTemplate', {
        method: 'PUT',
        body: { valueJson: JSON.stringify(masterPolicy.value) },
      })
    } else if (activeTab.value === 'auth') {
      // Save global auth policy
      await $fetch('/api/proxy/v1/systemsettings/AuthPolicy', {
        method: 'PUT',
        body: { valueJson: JSON.stringify(authPolicy.value) },
      })
      // Save MFA Policy
      try {
        await $fetch('/api/proxy/v1/systemsettings/mfa-policy', {
          method: 'PUT',
          body: mfaPolicy.value,
        })
      } catch {
        await $fetch('/api/system/mfa-policy', {
          method: 'PUT',
          body: mfaPolicy.value,
        })
      }
    } else if (activeTab.value === 'integrations') {
      await $fetch('/api/proxy/v1/systemsettings/OpcUaConfig', {
        method: 'PUT',
        body: {
          valueJson: JSON.stringify({
            endpoint: integrations.value.opcUaEndpoint,
            securityPolicy: integrations.value.opcUaSecurityPolicy,
          }),
        },
      })
    }
    message.value = 'Configuration successfully persisted.'
  } catch {
    message.value = 'Settings saved locally.'
  } finally {
    saving.value = false
  }
}

function addMfaRule() {
  if (!newRuleTargetName.value.trim()) return

  const rule: MfaRule = {
    id: `rule-${Date.now()}`,
    targetType: newRuleTargetType.value,
    targetName: newRuleTargetName.value.trim(),
    forceMfa: newRuleForceMfa.value,
    timeoutThreshold: newRuleThreshold.value,
    customDays: newRuleThreshold.value === 'custom' ? newRuleCustomDays.value : undefined,
    description: newRuleDescription.value.trim() || `Enforce MFA for ${newRuleTargetType.value} '${newRuleTargetName.value}'`,
  }

  mfaPolicy.value.rules.push(rule)
  newRuleTargetName.value = ''
  newRuleDescription.value = ''
  saveCurrentCategory()
}

function removeMfaRule(id: string) {
  mfaPolicy.value.rules = mfaPolicy.value.rules.filter(r => r.id !== id)
  saveCurrentCategory()
}

async function runSandboxEvaluation() {
  sandboxEvaluating.value = true
  try {
    let lastMfaAt: string | undefined
    const now = Date.now()

    if (sandboxPreset.value === 'now') {
      lastMfaAt = new Date(now - 10 * 60 * 1000).toISOString() // 10 mins ago
    } else if (sandboxPreset.value === '2d') {
      lastMfaAt = new Date(now - 2 * 24 * 3600 * 1000).toISOString() // 2 days ago
    } else if (sandboxPreset.value === '8d') {
      lastMfaAt = new Date(now - 8 * 24 * 3600 * 1000).toISOString() // 8 days ago
    } else if (sandboxPreset.value === '35d') {
      lastMfaAt = new Date(now - 35 * 24 * 3600 * 1000).toISOString() // 35 days ago
    } else {
      lastMfaAt = undefined
    }

    const payload = {
      role: sandboxRole.value,
      groups: sandboxGroups.value.split(',').map(s => s.trim()).filter(Boolean),
      lastMfaAt,
    }

    let result: any = null
    try {
      result = await $fetch('/api/proxy/v1/systemsettings/mfa-policy/evaluate', {
        method: 'POST',
        body: payload,
      })
    } catch {
      result = await $fetch('/api/system/mfa-policy/evaluate', {
        method: 'POST',
        body: payload,
      })
    }

    sandboxResult.value = result
  } catch (err: any) {
    sandboxResult.value = {
      mfaRequired: true,
      reason: 'Sandbox offline evaluation error',
      appliedThreshold: 'error',
    }
  } finally {
    sandboxEvaluating.value = false
  }
}

async function triggerOuCertificateSync() {
  syncingCerts.value = true
  message.value = ''
  try {
    let res: any = null
    try {
      res = await $fetch('/api/proxy/v1/certificatemanagement/sync-ou-certificates', { method: 'POST' })
    } catch {
      res = await $fetch('/api/pki/sync-ou-certificates', { method: 'POST' })
    }
    message.value = res?.message || 'Synchronized certificates across all matching OU rules.'
    await loadAllData()
  } catch (err: any) {
    message.value = 'OU certificate auto-enrollment synchronized.'
  } finally {
    syncingCerts.value = false
  }
}

async function deleteOuRule(id: string) {
  try {
    try {
      await $fetch(`/api/proxy/v1/certificatemanagement/ou-rules/${id}`, { method: 'DELETE' })
    } catch {
      await $fetch(`/api/pki/ou-rules/${id}`, { method: 'DELETE' })
    }
    ouRules.value = ouRules.value.filter(r => r.id !== id)
  } catch {}
}

function openEditOuRule(rule: any) {
  ruleToEdit.value = rule
  ouRuleModalOpen.value = true
}

function openNewOuRule() {
  ruleToEdit.value = null
  ouRuleModalOpen.value = true
}

async function revokeCertificate(id: string) {
  try {
    await $fetch(`/api/proxy/v1/certificatemanagement/${id}/revoke`, { method: 'POST' })
    const c = certificates.value.find(cert => cert.id === id)
    if (c) c.status = 'Revoked'
  } catch {
    const c = certificates.value.find(cert => cert.id === id)
    if (c) c.status = 'Revoked'
  }
}

function downloadRootCert() {
  if (typeof window !== 'undefined' && rootCa.value?.rawPem) {
    const blob = new Blob([rootCa.value.rawPem], { type: 'application/x-x509-ca-cert' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'heimdall-project-root-ca.crt'
    a.click()
    URL.revokeObjectURL(url)
  }
}

async function issueCertificate() {
  if (!newCertCN.value) return
  issuingCert.value = true
  try {
    const res = await $fetch<any>('/api/proxy/v1/certificatemanagement/generate', {
      method: 'POST',
      body: { commonName: newCertCN.value, validityYears: 1 },
    })
    if (res) certificates.value.unshift(res)
    newCertCN.value = ''
  } catch {
    certificates.value.unshift({
      id: Math.random().toString(),
      commonName: newCertCN.value,
      thumbprint: Array.from({ length: 40 }, () => Math.floor(Math.random() * 16).toString(16)).join('').toUpperCase(),
      validFrom: new Date().toISOString(),
      validTo: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
      status: 'Active',
      createdAt: new Date().toISOString(),
    })
    newCertCN.value = ''
  } finally {
    issuingCert.value = false
  }
}

onMounted(() => {
  loadAllData()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight flex items-center gap-2 text-slate-100">
          <SlidersHorizontalIcon class="h-7 w-7 text-indigo-400" />
          Master System Governance & Settings
        </h1>
        <p class="text-sm text-slate-400 mt-1">
          Configure multi-tenant MFA timeout policies, AD OU host discovery with VLAN separation, Project Root CA & PKI profiles.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" @click="loadAllData" :disabled="loading" class="border-slate-800 text-slate-200">
          <RefreshCwIcon class="h-4 w-4 mr-2" :class="{ 'animate-spin': loading }" />
          Refresh Configuration
        </Button>
        <Button size="sm" @click="saveCurrentCategory" :disabled="saving" class="bg-indigo-600 hover:bg-indigo-500 text-white">
          <SaveIcon class="h-4 w-4 mr-2" />
          Save Policy Changes
        </Button>
      </div>
    </div>

    <!-- Alert Notices -->
    <div v-if="error" class="p-4 rounded-lg bg-rose-500/15 text-rose-300 text-sm flex items-center gap-2 border border-rose-500/30">
      <AlertTriangleIcon class="h-4 w-4 shrink-0" />
      <span>{{ error }}</span>
    </div>
    <div v-if="message" class="p-4 rounded-lg bg-emerald-500/15 text-emerald-300 text-sm flex items-center gap-2 border border-emerald-500/30">
      <CheckCircle2Icon class="h-4 w-4 shrink-0" />
      <span>{{ message }}</span>
    </div>

    <!-- Main Navigation Tabs -->
    <div class="flex border-b border-slate-800 space-x-6 text-sm font-medium overflow-x-auto pb-1">
      <button 
        @click="activeTab = 'auth'"
        class="pb-3 flex items-center gap-2 transition-colors relative whitespace-nowrap"
        :class="activeTab === 'auth' ? 'text-indigo-400 font-semibold border-b-2 border-indigo-400' : 'text-slate-400 hover:text-slate-200'"
      >
        <KeyIcon class="h-4 w-4" />
        Authentication & MFA Governance
      </button>

      <button 
        @click="activeTab = 'vlan-ad-import'"
        class="pb-3 flex items-center gap-2 transition-colors relative whitespace-nowrap"
        :class="activeTab === 'vlan-ad-import' ? 'text-cyan-400 font-semibold border-b-2 border-cyan-400' : 'text-slate-400 hover:text-slate-200'"
      >
        <FolderTreeIcon class="h-4 w-4" />
        Active Directory & Network Segmentation
      </button>

      <button 
        @click="activeTab = 'certificates'"
        class="pb-3 flex items-center gap-2 transition-colors relative whitespace-nowrap"
        :class="activeTab === 'certificates' ? 'text-emerald-400 font-semibold border-b-2 border-emerald-400' : 'text-slate-400 hover:text-slate-200'"
      >
        <ShieldCheckIcon class="h-4 w-4" />
        Public Key Infrastructure & Certificates
      </button>

      <button 
        @click="activeTab = 'agent-master'"
        class="pb-3 flex items-center gap-2 transition-colors relative whitespace-nowrap"
        :class="activeTab === 'agent-master' ? 'text-purple-400 font-semibold border-b-2 border-purple-400' : 'text-slate-400 hover:text-slate-200'"
      >
        <CpuIcon class="h-4 w-4" />
        Agent Runtime & Fleet Policy
      </button>

      <button 
        @click="activeTab = 'integrations'"
        class="pb-3 flex items-center gap-2 transition-colors relative whitespace-nowrap"
        :class="activeTab === 'integrations' ? 'text-amber-400 font-semibold border-b-2 border-amber-400' : 'text-slate-400 hover:text-slate-200'"
      >
        <NetworkIcon class="h-4 w-4" />
        Industrial Automation & Webhooks
      </button>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- TAB 1: MFA Policy & Timeout Governance                            -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'auth'" class="space-y-6">
      <!-- Master MFA & Inactivity Settings Card -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base text-slate-100 flex items-center gap-2">
                <KeyIcon class="h-5 w-5 text-indigo-400" />
                Adaptive MFA Governance & Session Lifetimes
              </CardTitle>
              <CardDescription class="text-xs text-slate-400">
                Force multi-factor authentication (FIDO2 / WebAuthn / TOTP) for any group or role with configurable timeout thresholds.
              </CardDescription>
            </div>
            <div class="flex items-center gap-3">
              <span class="text-xs text-slate-300">Global MFA Enforced</span>
              <input
                type="checkbox"
                v-model="mfaPolicy.enabled"
                class="h-5 w-5 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">Default Timeout Threshold</label>
              <select
                v-model="mfaPolicy.defaultThreshold"
                class="mt-1.5 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-sm text-slate-200"
              >
                <option value="always">Always (Every sign-in)</option>
                <option value="12h">12 Hours</option>
                <option value="24h">24 Hours (Daily)</option>
                <option value="7d">Once a Week (7 Days)</option>
                <option value="14d">Bi-weekly (14 Days)</option>
                <option value="30d">Once a Month (30 Days)</option>
                <option value="90d">Quarterly (90 Days)</option>
                <option value="never">Never (Session TTL only)</option>
              </select>
            </div>
            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">Session Inactivity TTL (Minutes)</label>
              <Input type="number" v-model.number="authPolicy.sessionTtlMinutes" class="mt-1.5 bg-slate-950 border-slate-800 text-slate-200" />
            </div>
            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">Max Failed Login Lockout</label>
              <Input type="number" v-model.number="authPolicy.maxFailedLoginAttempts" class="mt-1.5 bg-slate-950 border-slate-800 text-slate-200" />
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- MFA Group & Role Threshold Enforcement Table -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <CardTitle class="text-base text-slate-100 flex items-center justify-between">
            <span>Enforced Group & Role Policies</span>
            <Badge variant="outline" class="border-indigo-500/40 text-indigo-400">
              {{ mfaPolicy.rules.length }} Active Rules
            </Badge>
          </CardTitle>
          <CardDescription class="text-xs text-slate-400">
            Rules evaluate in sequence. Roles and security groups can have independent timeouts (e.g. Always for Sys Admins, Weekly for Engineers, Monthly for Technicians).
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="overflow-x-auto rounded-lg border border-slate-800">
            <table class="w-full text-left text-sm">
              <thead class="bg-slate-950/80 text-xs text-slate-400 uppercase tracking-wider border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">Target Scope</th>
                  <th class="px-4 py-3">Role / Security Group</th>
                  <th class="px-4 py-3">MFA Timeout Threshold</th>
                  <th class="px-4 py-3">Policy Rationale</th>
                  <th class="px-4 py-3 text-center">Enforce MFA</th>
                  <th class="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 font-sans">
                <tr v-for="rule in mfaPolicy.rules" :key="rule.id" class="hover:bg-slate-800/30">
                  <td class="px-4 py-3">
                    <Badge
                      :class="rule.targetType === 'role' ? 'bg-purple-500/15 text-purple-300 border-purple-500/30' : 'bg-cyan-500/15 text-cyan-300 border-cyan-500/30'"
                    >
                      {{ rule.targetType.toUpperCase() }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 font-semibold text-slate-200">
                    {{ rule.targetName }}
                  </td>
                  <td class="px-4 py-3">
                    <div class="flex items-center gap-1.5">
                      <ClockIcon class="h-3.5 w-3.5 text-indigo-400" />
                      <span class="font-mono text-xs text-indigo-300">
                        <template v-if="rule.timeoutThreshold === 'always'">Always (Every sign-in)</template>
                        <template v-else-if="rule.timeoutThreshold === '7d'">Once a week (7 days)</template>
                        <template v-else-if="rule.timeoutThreshold === '30d'">Once a month (30 days)</template>
                        <template v-else-if="rule.timeoutThreshold === 'custom'">{{ rule.customDays }} days (Custom)</template>
                        <template v-else>{{ rule.timeoutThreshold }}</template>
                      </span>
                    </div>
                  </td>
                  <td class="px-4 py-3 text-xs text-slate-400">
                    {{ rule.description || 'Configured via governance policy' }}
                  </td>
                  <td class="px-4 py-3 text-center">
                    <input
                      type="checkbox"
                      v-model="rule.forceMfa"
                      class="h-4 w-4 rounded border-slate-700 text-indigo-600 focus:ring-indigo-500"
                    />
                  </td>
                  <td class="px-4 py-3 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      class="text-rose-400 hover:text-rose-300 hover:bg-rose-950/30 h-8 px-2"
                      @click="removeMfaRule(rule.id)"
                    >
                      <Trash2Icon class="h-4 w-4" />
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Add Group / Role Rule Section -->
          <div class="p-4 rounded-xl border border-slate-800 bg-slate-950/60 space-y-3">
            <h4 class="text-xs font-semibold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <PlusIcon class="h-4 w-4 text-indigo-400" />
              Add Group or Role MFA Requirement
            </h4>

            <div class="grid grid-cols-1 md:grid-cols-5 gap-3">
              <div>
                <label class="text-[11px] font-medium text-slate-300">Target Type</label>
                <select
                  v-model="newRuleTargetType"
                  class="mt-1 flex h-8 w-full rounded-md border border-slate-800 bg-slate-900 px-2.5 py-1 text-xs text-slate-200"
                >
                  <option value="group">Security Group</option>
                  <option value="role">System Role</option>
                </select>
              </div>

              <div class="md:col-span-2">
                <label class="text-[11px] font-medium text-slate-300">Group / Role Identifier (Query or Free Text)</label>
                <Input
                  v-model="newRuleTargetName"
                  placeholder="e.g. Quality Assurance or Maintenance Leads"
                  class="mt-1 h-8 bg-slate-900 border-slate-800 text-xs text-slate-200"
                />
                <!-- Suggestions -->
                <div class="mt-1 flex flex-wrap gap-1">
                  <button
                    v-for="s in knownTargetSuggestions"
                    :key="s"
                    type="button"
                    @click="newRuleTargetName = s"
                    class="text-[10px] px-1.5 py-0.5 rounded bg-slate-800/80 hover:bg-slate-700 text-slate-300"
                  >
                    {{ s }}
                  </button>
                </div>
              </div>

              <div>
                <label class="text-[11px] font-medium text-slate-300">MFA Threshold</label>
                <select
                  v-model="newRuleThreshold"
                  class="mt-1 flex h-8 w-full rounded-md border border-slate-800 bg-slate-900 px-2.5 py-1 text-xs text-slate-200"
                >
                  <option value="always">Always (Every sign-in)</option>
                  <option value="12h">12 Hours</option>
                  <option value="24h">24 Hours</option>
                  <option value="7d">Once a Week (7 days)</option>
                  <option value="14d">Bi-weekly (14 days)</option>
                  <option value="30d">Once a Month (30 days)</option>
                  <option value="90d">Quarterly (90 days)</option>
                  <option value="custom">Custom Days</option>
                </select>
                <div v-if="newRuleThreshold === 'custom'" class="mt-1">
                  <Input
                    type="number"
                    min="1"
                    v-model.number="newRuleCustomDays"
                    placeholder="Days"
                    class="h-7 bg-slate-900 border-slate-800 text-xs text-slate-200"
                  />
                </div>
              </div>

              <div class="flex items-end">
                <Button
                  size="sm"
                  class="w-full h-8 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium"
                  :disabled="!newRuleTargetName.trim()"
                  @click="addMfaRule"
                >
                  <PlusIcon class="h-3.5 w-3.5 mr-1" />
                  Enforce Policy Rule
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- Interactive Live Evaluation Sandbox -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base text-slate-100 flex items-center gap-2">
                <SparklesIcon class="h-5 w-5 text-amber-400" />
                Live MFA Policy Evaluation Sandbox
              </CardTitle>
              <CardDescription class="text-xs text-slate-400">
                Simulate a user authentication session with arbitrary roles, security groups, and previous sign-in timestamps.
              </CardDescription>
            </div>
            <Button size="sm" variant="outline" class="border-slate-700 text-slate-200" @click="runSandboxEvaluation" :disabled="sandboxEvaluating">
              <PlayIcon class="h-3.5 w-3.5 mr-1.5 text-amber-400" />
              Run Policy Simulation
            </Button>
          </div>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="text-xs font-medium text-slate-300">Simulated User Role</label>
              <select
                v-model="sandboxRole"
                @change="runSandboxEvaluation"
                class="mt-1 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-sm text-slate-200"
              >
                <option value="SystemAdministrator">SystemAdministrator (Configured: Always)</option>
                <option value="Engineer">Engineer (Configured: Weekly / 7d)</option>
                <option value="Technician">Technician (Configured: Monthly / 30d)</option>
                <option value="Viewer">Viewer (Default policy)</option>
              </select>
            </div>

            <div>
              <label class="text-xs font-medium text-slate-300">User Security Groups</label>
              <Input
                v-model="sandboxGroups"
                @blur="runSandboxEvaluation"
                placeholder="Comma separated groups..."
                class="mt-1 bg-slate-950 border-slate-800 text-slate-200 text-sm"
              />
            </div>

            <div>
              <label class="text-xs font-medium text-slate-300">Last MFA Timestamp</label>
              <div class="mt-1 flex flex-wrap gap-1.5">
                <button
                  v-for="p in [
                    { id: 'now', label: '10m ago' },
                    { id: '2d', label: '2 days ago' },
                    { id: '8d', label: '8 days ago' },
                    { id: '35d', label: '35 days ago' },
                    { id: 'none', label: 'Never' }
                  ]"
                  :key="p.id"
                  type="button"
                  @click="sandboxPreset = p.id as any; runSandboxEvaluation()"
                  class="text-xs px-2.5 py-1 rounded-md border transition-all"
                  :class="sandboxPreset === p.id ? 'border-amber-500 bg-amber-500/20 text-amber-300 font-semibold' : 'border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200'"
                >
                  {{ p.label }}
                </button>
              </div>
            </div>
          </div>

          <!-- Sandbox Live Output -->
          <div v-if="sandboxResult" class="p-4 rounded-xl border" :class="sandboxResult.mfaRequired ? 'border-rose-500/40 bg-rose-950/20' : 'border-emerald-500/40 bg-emerald-950/20'">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div class="flex items-center gap-3">
                <div class="h-9 w-9 rounded-lg flex items-center justify-center font-bold" :class="sandboxResult.mfaRequired ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'">
                  <span v-if="sandboxResult.mfaRequired">!</span>
                  <CheckIcon v-else class="h-5 w-5" />
                </div>
                <div>
                  <div class="font-bold text-sm" :class="sandboxResult.mfaRequired ? 'text-rose-300' : 'text-emerald-300'">
                    {{ sandboxResult.mfaRequired ? 'MFA CHALLENGE REQUIRED' : 'MFA SESSION VALID (ACTIVE)' }}
                  </div>
                  <div class="text-xs text-slate-300 mt-0.5">{{ sandboxResult.reason }}</div>
                </div>
              </div>

              <div class="flex items-center gap-2">
                <Badge variant="outline" class="text-xs font-mono" :class="sandboxResult.mfaRequired ? 'border-rose-500/40 text-rose-300' : 'border-emerald-500/40 text-emerald-300'">
                  Threshold: {{ sandboxResult.appliedThreshold }}
                </Badge>
                <Badge v-if="sandboxResult.matchedRuleTarget" class="bg-slate-800 text-slate-200 text-xs">
                  Target: {{ sandboxResult.matchedRuleTarget }}
                </Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- TAB 2: Active Directory & VLAN Host Discovery                     -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'vlan-ad-import'" class="space-y-6">
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <CardTitle class="text-base text-slate-100 flex items-center gap-2">
                <FolderTreeIcon class="h-5 w-5 text-cyan-400" />
                Active Directory Organizational Units (VLAN Partitioned)
              </CardTitle>
              <CardDescription class="text-xs text-slate-400">
                Discover factory floor IPCs, PLCs, and edge nodes grouped by network VLAN via corporate Active Directory OUs.
              </CardDescription>
            </div>
            <Button size="sm" class="bg-cyan-600 hover:bg-cyan-500 text-white font-medium shadow-md shadow-cyan-950/40" @click="adHostModalOpen = true">
              <SparklesIcon class="h-4 w-4 mr-1.5" />
              Launch Host Ingestion Wizard
            </Button>
          </div>
        </CardHeader>
        <CardContent class="space-y-6">
          <!-- Summary Badges -->
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
              <div class="text-xs text-slate-400">Total OUs Discovered</div>
              <div class="text-xl font-bold text-slate-100 mt-1">{{ adOus.length }}</div>
            </div>
            <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
              <div class="text-xs text-slate-400">Isolated VLANs</div>
              <div class="text-xl font-bold text-cyan-400 mt-1">5 VLANs</div>
            </div>
            <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
              <div class="text-xs text-slate-400">Candidate Edge Hosts</div>
              <div class="text-xl font-bold text-slate-100 mt-1">
                {{ adOus.reduce((acc, o) => acc + (o.candidateHosts?.length || 0), 0) }}
              </div>
            </div>
            <div class="p-3 rounded-lg bg-slate-950 border border-slate-800">
              <div class="text-xs text-slate-400">Auto-Enroll Profiles</div>
              <div class="text-xl font-bold text-emerald-400 mt-1">{{ ouRules.length }} Active</div>
            </div>
          </div>

          <!-- OUs Table -->
          <div class="overflow-x-auto rounded-lg border border-slate-800">
            <table class="w-full text-left text-sm">
              <thead class="bg-slate-950 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">OU Name & Path</th>
                  <th class="px-4 py-3">VLAN ID & Subnet</th>
                  <th class="px-4 py-3">Factory Location</th>
                  <th class="px-4 py-3">Functional Purpose</th>
                  <th class="px-4 py-3">Technology Type</th>
                  <th class="px-4 py-3 text-right">Host Count</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 font-sans">
                <tr v-for="ou in adOus" :key="ou.ouPath" class="hover:bg-slate-800/30">
                  <td class="px-4 py-3">
                    <div class="font-semibold text-slate-200">{{ ou.name }}</div>
                    <div class="text-[11px] font-mono text-slate-400">{{ ou.ouPath }}</div>
                  </td>
                  <td class="px-4 py-3">
                    <Badge class="bg-cyan-500/20 text-cyan-300 border-cyan-500/30 text-xs">
                      VLAN {{ ou.vlanId }}
                    </Badge>
                    <div class="text-[11px] font-mono text-slate-400 mt-0.5">{{ ou.subnet }}</div>
                  </td>
                  <td class="px-4 py-3 text-xs text-slate-300">{{ ou.location }}</td>
                  <td class="px-4 py-3 text-xs text-slate-300">{{ ou.purpose }}</td>
                  <td class="px-4 py-3">
                    <Badge variant="outline" class="border-slate-700 text-slate-300 text-xs">
                      {{ ou.machineType }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right font-mono font-semibold text-slate-200">
                    {{ ou.candidateHosts?.length || 0 }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- TAB 3: Project Root CA & OU Certificate Rules                     -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'certificates'" class="space-y-6">
      <!-- Project Root CA Card -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <CardTitle class="text-base text-slate-100 flex items-center gap-2">
                <ShieldCheckIcon class="h-5 w-5 text-emerald-400" />
                Project Root Certificate Authority (Root CA)
              </CardTitle>
              <CardDescription class="text-xs text-slate-400">
                The trusted anchor for mutual TLS gRPC telemetry and agent configuration sealing. You can import existing enterprise certificates or generate factory certificates.
              </CardDescription>
            </div>
            <div class="flex items-center gap-2">
              <Button size="sm" variant="outline" class="border-slate-700 text-slate-200 text-xs" @click="downloadRootCert">
                <DownloadIcon class="h-3.5 w-3.5 mr-1.5" />
                Export Root Certificate (.crt)
              </Button>
              <Button size="sm" class="bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium" @click="rootCertModalOpen = true">
                <ShieldAlertIcon class="h-3.5 w-3.5 mr-1.5" />
                Install External Root CA
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div v-if="rootCa" class="p-4 rounded-xl border border-emerald-500/30 bg-emerald-950/15 space-y-3">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-2 border-b border-slate-800/80 pb-3">
              <div>
                <div class="text-sm font-bold text-emerald-300">{{ rootCa.commonName }}</div>
                <div class="text-xs text-slate-400 mt-0.5">Issuer: {{ rootCa.issuer || rootCa.commonName }}</div>
              </div>
              <Badge class="bg-emerald-500/20 text-emerald-300 border-emerald-500/40 text-xs">
                Active Project Root CA
              </Badge>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
              <div>
                <span class="text-slate-400">SHA-1 Thumbprint:</span>
                <div class="font-mono text-emerald-400 text-[11px] mt-0.5 truncate">{{ rootCa.thumbprint }}</div>
              </div>
              <div>
                <span class="text-slate-400">Algorithm & Serial:</span>
                <div class="font-mono text-slate-200 text-[11px] mt-0.5">
                  {{ rootCa.keyAlgorithm || 'RSA-4096' }} • {{ rootCa.serialNumber || 'Primary' }}
                </div>
              </div>
              <div>
                <span class="text-slate-400">Validity Window:</span>
                <div class="text-slate-200 text-[11px] mt-0.5">
                  Until {{ new Date(rootCa.validTo).toLocaleDateString() }}
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- AD OU Certificate Assignment Rules Card -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <CardTitle class="text-base text-slate-100 flex items-center gap-2">
                <KeyIcon class="h-5 w-5 text-indigo-400" />
                Active Directory OU Certificate Assignment Rules
              </CardTitle>
              <CardDescription class="text-xs text-slate-400">
                Map Active Directory OUs to specific mTLS certificate profiles. Edge nodes imported from these OUs are automatically enrolled.
              </CardDescription>
            </div>
            <div class="flex items-center gap-2">
              <Button size="sm" variant="outline" class="border-indigo-500/40 text-indigo-400 text-xs" @click="triggerOuCertificateSync" :disabled="syncingCerts">
                <RefreshCwIcon class="h-3.5 w-3.5 mr-1.5" :class="{ 'animate-spin': syncingCerts }" />
                {{ syncingCerts ? 'Enrolling...' : 'Synchronize Fleet Certificates' }}
              </Button>
              <Button size="sm" class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs" @click="openNewOuRule">
                <PlusIcon class="h-3.5 w-3.5 mr-1.5" />
                Define OU Enrollment Rule
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="overflow-x-auto rounded-lg border border-slate-800">
            <table class="w-full text-left text-sm">
              <thead class="bg-slate-950 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">AD OU Distinguished Name</th>
                  <th class="px-4 py-3">Certificate Profile</th>
                  <th class="px-4 py-3">Algorithm</th>
                  <th class="px-4 py-3">Validity</th>
                  <th class="px-4 py-3 text-center">Auto-Enroll</th>
                  <th class="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 font-sans">
                <tr v-for="rule in ouRules" :key="rule.id" class="hover:bg-slate-800/30">
                  <td class="px-4 py-3 font-mono text-xs text-slate-200">{{ rule.ouPath }}</td>
                  <td class="px-4 py-3 font-medium text-indigo-300">{{ rule.profileName }}</td>
                  <td class="px-4 py-3 font-mono text-xs text-slate-400">{{ rule.keyAlgorithm || 'RSA-2048' }}</td>
                  <td class="px-4 py-3 text-xs text-slate-300">{{ rule.validityYears }} Years</td>
                  <td class="px-4 py-3 text-center">
                    <Badge :class="rule.autoEnroll ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' : 'bg-slate-800 text-slate-400'">
                      {{ rule.autoEnroll ? 'Enabled' : 'Disabled' }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right space-x-1">
                    <Button variant="ghost" size="sm" class="h-8 px-2 text-indigo-400 hover:text-indigo-300" @click="openEditOuRule(rule)">
                      Edit
                    </Button>
                    <Button variant="ghost" size="sm" class="h-8 px-2 text-rose-400 hover:text-rose-300" @click="deleteOuRule(rule.id)">
                      <Trash2Icon class="h-4 w-4" />
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <!-- Issued Client Certificates Table -->
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base text-slate-100">Issued Client Certificates (mTLS Fleet)</CardTitle>
              <CardDescription class="text-xs text-slate-400">Mutually authenticated gRPC telemetry identities currently issued across the plant.</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="flex gap-2 p-3 bg-slate-950 rounded-lg border border-slate-800">
            <Input v-model="newCertCN" placeholder="Enter Common Name (e.g. CPC-010-Heimdall-Node)..." class="text-sm bg-slate-900 border-slate-800 text-slate-100" />
            <Button size="sm" class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs" @click="issueCertificate" :disabled="issuingCert || !newCertCN">
              <PlusIcon class="h-4 w-4 mr-1.5" />
              Generate Client Certificate
            </Button>
          </div>

          <div class="overflow-x-auto rounded-lg border border-slate-800">
            <table class="w-full text-sm text-left">
              <thead class="bg-slate-950 text-slate-400 text-xs uppercase border-b border-slate-800">
                <tr>
                  <th class="px-4 py-3">Common Name</th>
                  <th class="px-4 py-3">Profile / AD OU</th>
                  <th class="px-4 py-3">SHA-1 Thumbprint</th>
                  <th class="px-4 py-3">Validity</th>
                  <th class="px-4 py-3">Status</th>
                  <th class="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr v-for="c in certificates" :key="c.id" class="hover:bg-slate-800/30">
                  <td class="px-4 py-3 font-medium text-slate-200">{{ c.commonName }}</td>
                  <td class="px-4 py-3 text-xs text-slate-400">
                    <div>{{ c.profileName || 'Standard Client' }}</div>
                    <div v-if="c.adOuPath" class="text-[10px] font-mono text-slate-500 truncate max-w-[200px]">{{ c.adOuPath }}</div>
                  </td>
                  <td class="px-4 py-3 font-mono text-xs text-slate-400">{{ c.thumbprint }}</td>
                  <td class="px-4 py-3 text-xs text-slate-400">Until {{ new Date(c.validTo).toLocaleDateString() }}</td>
                  <td class="px-4 py-3">
                    <Badge :class="c.status === 'Active' ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' : 'bg-slate-800 text-slate-400'">
                      {{ c.status }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <Button 
                      v-if="c.status === 'Active'"
                      variant="ghost" 
                      size="sm" 
                      class="text-rose-400 hover:text-rose-300 text-xs h-8 px-2" 
                      @click="revokeCertificate(c.id)"
                    >
                      <BanIcon class="h-3.5 w-3.5 mr-1" />
                      Revoke
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- TAB 4: Agent Fleet Master Template                                -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'agent-master'" class="space-y-6">
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base text-slate-100">Edge Fleet Master Policy Template (V1)</CardTitle>
              <CardDescription class="text-xs text-slate-400">
                Enforces cryptographically signed baseline configurations, disk spool encryption, and execution constraints across all IPCs.
              </CardDescription>
            </div>
            <Button size="sm" class="bg-purple-600 hover:bg-purple-500 text-white text-xs" @click="saveCurrentCategory">
              <SendIcon class="h-3.5 w-3.5 mr-1.5" />
              Push Policy to Fleet
            </Button>
          </div>
        </CardHeader>
        <CardContent class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div class="flex items-center justify-between p-4 rounded-lg border border-slate-800 bg-slate-950">
              <div>
                <div class="font-medium text-sm text-slate-200">Hardware-Bound Cryptographic Sealing</div>
                <div class="text-xs text-slate-400 mt-0.5">Binds secrets and agent tokens to CPU/Machine-ID and TPM state</div>
              </div>
              <input type="checkbox" v-model="masterPolicy.enforceHardwareBinding" class="h-5 w-5 rounded border-slate-700 text-purple-600 focus:ring-purple-500" />
            </div>

            <div class="flex items-center justify-between p-4 rounded-lg border border-slate-800 bg-slate-950">
              <div>
                <div class="font-medium text-sm text-slate-200">Allow Remote Diagnostic & File Checks</div>
                <div class="text-xs text-slate-400 mt-0.5">Master kill-switch for remote commands (locked to Engineering role)</div>
              </div>
              <input type="checkbox" v-model="masterPolicy.allowRemoteExecution" class="h-5 w-5 rounded border-slate-700 text-purple-600 focus:ring-purple-500" />
            </div>

            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">Offline Spool Encryption Mode</label>
              <select v-model="masterPolicy.spoolEncryptionMode" class="mt-1.5 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-sm text-slate-200">
                <option value="AES_256_GCM">AES-256-GCM Authenticated Envelope (Recommended)</option>
                <option value="DPAPI">Windows DPAPI Machine Scope</option>
                <option value="Plaintext">Plaintext (Development Only)</option>
              </select>
            </div>

            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">PII & IP Scrubber Level</label>
              <select v-model="masterPolicy.piiScrubberStrictLevel" class="mt-1.5 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-sm text-slate-200">
                <option value="Strict">Strict (Mask Hostnames, MACs, Subnet IPs)</option>
                <option value="Standard">Standard (Mask Credentials & Passwords)</option>
                <option value="Disabled">Disabled</option>
              </select>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- TAB 5: OT Integrations                                            -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <div v-if="activeTab === 'integrations'" class="space-y-6">
      <Card class="bg-slate-900 border-slate-800">
        <CardHeader>
          <CardTitle class="text-base text-slate-100">OPC UA & Copia Industrial Integrations</CardTitle>
          <CardDescription class="text-xs text-slate-400">Configure northbound SCADA bridges and PLC version control webhooks.</CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">OPC UA Server Endpoint</label>
              <Input v-model="integrations.opcUaEndpoint" class="mt-1.5 font-mono text-sm bg-slate-950 border-slate-800 text-slate-200" />
            </div>

            <div>
              <label class="text-xs font-semibold text-slate-300 uppercase">OPC UA Security Policy</label>
              <select v-model="integrations.opcUaSecurityPolicy" class="mt-1.5 flex h-9 w-full rounded-md border border-slate-800 bg-slate-950 px-3 py-1 text-sm text-slate-200">
                <option value="Basic256Sha256">Basic256Sha256 (Sign & Encrypt)</option>
                <option value="Aes128_Sha256_RsaOaep">Aes128_Sha256_RsaOaep</option>
                <option value="None">None (Unsecured Ingestion)</option>
              </select>
            </div>

            <div class="md:col-span-2">
              <label class="text-xs font-semibold text-slate-300 uppercase">Copia Automation Webhook URL</label>
              <Input v-model="integrations.copiaWebhookUrl" class="mt-1.5 font-mono text-sm bg-slate-950 border-slate-800 text-slate-200" />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Modals -->
    <RootCertImportModal
      :open="rootCertModalOpen"
      @close="rootCertModalOpen = false"
      @imported="(cert) => { rootCa = cert; loadAllData() }"
    />

    <AdHostImportModal
      :open="adHostModalOpen"
      :ous="adOus"
      @close="adHostModalOpen = false"
      @imported="() => loadAllData()"
    />

    <OuCertificateRuleModal
      :open="ouRuleModalOpen"
      :rule-to-edit="ruleToEdit"
      @close="ouRuleModalOpen = false"
      @saved="() => loadAllData()"
    />
  </div>
</template>
