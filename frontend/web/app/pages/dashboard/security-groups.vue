<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import RbacButton from '@/components/common/RbacButton.vue'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'
import { useFeatureFlags } from '~/composables/useFeatureFlags'
import { 
  ShieldCheckIcon, 
  PlusIcon, 
  Trash2Icon, 
  RefreshCwIcon, 
  PlayIcon, 
  CheckCircle2Icon, 
  AlertCircleIcon,
  LayersIcon,
  ServerIcon,
  KeyIcon,
  CopyIcon,
  CheckIcon,
  TerminalIcon,
  TicketIcon,
  CodeIcon,
  ExternalLinkIcon
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

interface SecurityGroupMapping {
  id: string
  identityProvider: string
  groupIdentifier: string
  displayName: string
  mappedRole: string
  organizationId?: string | null
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  sourceTicketId?: string | null
  sourceSystem?: string | null
  requestedBy?: string | null
  approvedBy?: string | null
  reason?: string | null
}

export interface SafeAutomationApiKey {
  id: string
  name: string
  keyPrefix: string
  scopes: string[]
  systemType: 'Jira' | 'ServiceNow' | 'Ansible' | 'PowerAutomate' | 'Custom'
  createdBy: string
  createdAt: string
  expiresAt: string | null
  lastUsedAt: string | null
  isActive: boolean
  description?: string
}

const { enableSimulation } = useFeatureFlags()
const mappings = ref<SecurityGroupMapping[]>([])
const loading = ref(false)
const error = ref('')
const successMsg = ref('')

// External IT Automation API Keys State
const apiKeys = ref<SafeAutomationApiKey[]>([])
const loadingKeys = ref(false)
const isCreatingKey = ref(false)
const newKeyName = ref('')
const newKeySystem = ref<'ServiceNow' | 'Jira' | 'Ansible' | 'PowerAutomate' | 'Custom'>('ServiceNow')
const newKeyScopes = ref<string[]>([
  'security_groups:read',
  'security_groups:write',
  'security_groups:evaluate',
  'tickets:create'
])
const newKeyExpiresDays = ref<number | null>(90)
const newKeyDescription = ref('')
const generatedRawKey = ref<string | null>(null)
const keyCopied = ref(false)

// Automation Interactive Sandbox State
const selectedApiKeyForTest = ref('')
const testAction = ref<'create_mapping' | 'evaluate'>('create_mapping')
const testTicketId = ref('INC0091823')
const testRequester = ref('controls.lead@factory.corp')
const testReason = ref('Emergency Controls Engineer Access for Line 06 Battery Line')
const testGroupId = ref('9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c')
const testDisplayName = ref('OT Plant Battery Line Engineers')
const testMappedRole = ref('controls_engineer')
const testOrg = ref('Line 06 – Battery Module Line')
const automationOutput = ref<any>(null)
const isCallingAutomation = ref(false)
const activeDocTab = ref<'curl' | 'jira' | 'serviceNow' | 'python' | 'powershell'>('curl')


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
    ssoStatus.value = {
      enabled: true,
      provider: 'microsoft',
      providerName: 'Microsoft Entra ID (Azure SSO)',
      tenantId: '72f988bf-86f1-41af-91ab-2d7cd011db47',
      isConfigured: false,
      allowMockSimulation: true
    }
  }
}

// Form state for new mapping
const isCreating = ref(false)
const newIdp = ref('EntraID')
const newGroupId = ref('')
const newDisplayName = ref('')
const newMappedRole = ref('engineer')
const newOrgId = ref('')

// Interactive Evaluation Sandbox
const testInputGroups = ref('9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c\nCN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp')
const testResult = ref<any>(null)
const evaluating = ref(false)

const { isItAdmin, isSystemAdmin } = useAuthSession()
const canApproveOus = computed(() => isItAdmin.value || isSystemAdmin.value)

interface AdOuItem {
  ouPath: string
  name: string
  vlanId: number
  vlanName: string
  subnet: string
  location?: string
  purpose?: string
  machineType?: string
  hostCount: number
  accessLevel?: 'read_write' | 'read_only' | 'unapproved'
  isApproved?: boolean
  approvedBy?: string
  approvedAt?: string
  approvalNotes?: string
}

const ousList = ref<AdOuItem[]>([])
const loadingOus = ref(false)
const approvingOuPath = ref<string | null>(null)

const rolesList = [
  'system_admin',
  'heimdall_admin',
  'it_admin',
  'engineering_admin',
  'admin',
  'manager',
  'group_leader',
  'shift_leader',
  'team_lead',
  'engineer',
  'controls_engineer',
  'lead_engineer',
  'technician',
  'operator'
]

async function fetchOus() {
  loadingOus.value = true
  try {
    const res = await $fetch<AdOuItem[]>('/api/activedirectory/ous')
    ousList.value = res || []
  } catch (e) {
    console.error('Failed to fetch OUs:', e)
  } finally {
    loadingOus.value = false
  }
}

async function handleSetOuGovernance(ouPath: string, level: 'read_write' | 'read_only' | 'unapproved') {
  approvingOuPath.value = ouPath
  error.value = ''
  successMsg.value = ''
  try {
    await $fetch('/api/activedirectory/ous/approve', {
      method: 'POST',
      body: {
        ouPath,
        accessLevel: level,
        notes: `Updated by IT Admin (${level})`
      }
    })
    successMsg.value = `OU '${ouPath}' governance set to '${level}'.`
    await fetchOus()
  } catch (err: any) {
    error.value = err.data?.statusMessage || err.data?.message || err.message || 'Failed to update OU governance'
  } finally {
    approvingOuPath.value = null
  }
}

async function fetchMappings() {
  loading.value = true
  error.value = ''
  try {
    const res = await $fetch<SecurityGroupMapping[]>('/api/security-groups/mappings')
    mappings.value = res || []
  } catch (err: any) {
    try {
      const fallback = await $fetch<SecurityGroupMapping[]>('/api/proxy/v1/securitygroupmapping')
      mappings.value = fallback || []
    } catch {
      // If backend is unavailable during SSR/test, use default seeds
      if (mappings.value.length === 0) {
        mappings.value = [
          {
            id: '1',
            identityProvider: 'EntraID',
            groupIdentifier: '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c',
            displayName: 'OT Plant Administrators',
            mappedRole: 'admin',
            organizationId: null,
            isEnabled: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
          },
          {
            id: '2',
            identityProvider: 'ActiveDirectory',
            groupIdentifier: 'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
            displayName: 'On-Prem Controls Engineers',
            mappedRole: 'engineer',
            organizationId: 'Production Floor B',
            isEnabled: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
          },
          {
            id: '3',
            identityProvider: 'ActiveDirectory',
            groupIdentifier: 'CN=OT-Maintenance-Technicians,OU=Groups,DC=factory,DC=corp',
            displayName: 'Plant Maintenance Technicians',
            mappedRole: 'technician',
            organizationId: null,
            isEnabled: true,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
          }
        ]
      }
    }
  } finally {
    loading.value = false
  }
}

async function createMapping() {
  if (!newGroupId.value || !newDisplayName.value) {
    error.value = 'Group Identifier and Display Name are required.'
    return
  }
  loading.value = true
  error.value = ''
  try {
    await $fetch('/api/security-groups/mappings', {
      method: 'POST',
      body: {
        identityProvider: newIdp.value,
        groupIdentifier: newGroupId.value,
        displayName: newDisplayName.value,
        mappedRole: newMappedRole.value,
        organizationId: newOrgId.value || null,
        isEnabled: true
      }
    })
    successMsg.value = 'Security group mapping saved successfully.'
    isCreating.value = false
    newGroupId.value = ''
    newDisplayName.value = ''
    newOrgId.value = ''
    await fetchMappings()
  } catch (err: any) {
    error.value = err.data?.message || 'Failed to save security group mapping.'
  } finally {
    loading.value = false
  }
}

async function toggleMapping(mapping: SecurityGroupMapping) {
  try {
    await $fetch(`/api/security-groups/mappings/${mapping.id}`, {
      method: 'PUT',
      body: {
        ...mapping,
        isEnabled: !mapping.isEnabled
      }
    })
    mapping.isEnabled = !mapping.isEnabled
  } catch (err) {
    mapping.isEnabled = !mapping.isEnabled
  }
}

async function deleteMapping(id: string) {
  if (!confirm('Are you sure you want to remove this security group mapping?')) return
  try {
    await $fetch(`/api/security-groups/mappings/${id}`, {
      method: 'DELETE'
    })
    mappings.value = mappings.value.filter(m => m.id !== id)
  } catch (err: any) {
    error.value = 'Failed to delete mapping.'
  }
}

// ───────────────────────────────────────────────────────────
// External IT Automation API Keys Management & Sandbox Handlers
// ───────────────────────────────────────────────────────────
async function fetchApiKeys() {
  loadingKeys.value = true
  try {
    const res = await $fetch<SafeAutomationApiKey[]>('/api/security-groups/api-keys')
    apiKeys.value = res || []
    if (!selectedApiKeyForTest.value && apiKeys.value.length > 0) {
      selectedApiKeyForTest.value = apiKeys.value[0].keyPrefix
    }
  } catch (e) {
    console.error('Failed to load API keys:', e)
  } finally {
    loadingKeys.value = false
  }
}

async function handleGenerateKey() {
  if (!newKeyName.value.trim()) {
    error.value = 'Please provide an API Key name.'
    return
  }
  loadingKeys.value = true
  error.value = ''
  try {
    const res = await $fetch<{ success: boolean; apiKey: SafeAutomationApiKey; rawKey: string }>('/api/security-groups/api-keys', {
      method: 'POST',
      body: {
        name: newKeyName.value.trim(),
        systemType: newKeySystem.value,
        scopes: newKeyScopes.value,
        expiresInDays: newKeyExpiresDays.value,
        description: newKeyDescription.value.trim()
      }
    })
    generatedRawKey.value = res.rawKey
    selectedApiKeyForTest.value = res.rawKey
    newKeyName.value = ''
    newKeyDescription.value = ''
    await fetchApiKeys()
    successMsg.value = `API Key '${res.apiKey.name}' generated. Save your secret key now.`
  } catch (err: any) {
    error.value = err.data?.message || 'Failed to generate API Key.'
  } finally {
    loadingKeys.value = false
  }
}

async function handleRevokeKey(id: string) {
  if (!confirm('Are you sure you want to revoke this automation API key? External systems using it will be blocked immediately.')) return
  try {
    await $fetch(`/api/security-groups/api-keys/${id}`, {
      method: 'DELETE'
    })
    await fetchApiKeys()
    successMsg.value = 'API Key revoked successfully.'
  } catch (err: any) {
    error.value = 'Failed to revoke API Key.'
  }
}

function copyToClipboard(text: string) {
  if (typeof navigator !== 'undefined' && navigator.clipboard) {
    navigator.clipboard.writeText(text)
    keyCopied.value = true
    setTimeout(() => {
      keyCopied.value = false
    }, 2500)
  }
}

async function executeAutomationTest() {
  isCallingAutomation.value = true
  automationOutput.value = null
  const authKey = generatedRawKey.value || 'hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c'

  try {
    if (testAction.value === 'create_mapping') {
      const res = await $fetch<any>('/api/v1/automation/security-groups/mappings', {
        method: 'POST',
        headers: {
          'X-API-Key': authKey
        },
        body: {
          identityProvider: 'EntraID',
          groupIdentifier: testGroupId.value,
          displayName: testDisplayName.value,
          mappedRole: testMappedRole.value,
          organizationId: testOrg.value || null,
          ticketId: testTicketId.value,
          requestedBy: testRequester.value,
          reason: testReason.value,
          autoCreateAuditTicket: true
        }
      })
      automationOutput.value = {
        status: 201,
        statusText: 'Created',
        data: res
      }
      successMsg.value = `Automation API call successful: Mapping provisioned with audit ticket ${res.auditTrail?.ticketNumber || ''}`
      await fetchMappings()
    } else {
      const res = await $fetch<any>('/api/v1/automation/security-groups/evaluate', {
        method: 'POST',
        headers: {
          'X-API-Key': authKey
        },
        body: {
          groupIdentifiers: [testGroupId.value],
          ticketId: testTicketId.value,
          userEmail: testRequester.value
        }
      })
      automationOutput.value = {
        status: 200,
        statusText: 'OK',
        data: res
      }
    }
  } catch (err: any) {
    automationOutput.value = {
      status: err.statusCode || 500,
      statusText: err.statusMessage || 'Error',
      data: err.data || err.message
    }
  } finally {
    isCallingAutomation.value = false
  }
}


const standardOrganizations = [
  'Factory Operations',
  'Line 06 – Battery Module Line',
  'Line 09 – Optical Quality Inspection',
  'Powertrain Sub-Assembly'
]

const simulatedUserPresets = [
  {
    name: 'Sally Vance (Lead Controls)',
    groups: 'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp'
  },
  {
    name: 'George Orwell (Senior Tech)',
    groups: 'CN=OT-Maintenance-Technicians,OU=Groups,DC=factory,DC=corp'
  },
  {
    name: 'Alex Novak (Vision Lead)',
    groups: 'b3f81e22-9c1a-4d72-b883-4a11f2c90e55'
  },
  {
    name: 'Elena Rostova (Shift Supervisor)',
    groups: 'CN=Facility-Shift-Leaders,OU=Groups,DC=factory,DC=corp'
  },
  {
    name: 'Root Admin (OT Systems Admin)',
    groups: '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c'
  },
  {
    name: 'Multi-Role Specialist (Controls + Shift)',
    groups: 'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp\nCN=Facility-Shift-Leaders,OU=Groups,DC=factory,DC=corp'
  }
]

const selectedPreset = ref('')

function applyPreset(presetName: string) {
  const p = simulatedUserPresets.find(x => x.name === presetName)
  if (p) {
    testInputGroups.value = p.groups
    runEvaluationTest()
  }
}

async function runEvaluationTest() {
  evaluating.value = true
  testResult.value = null
  const groupList = testInputGroups.value
    .split('\n')
    .map(g => g.trim())
    .filter(g => g.length > 0)

  try {
    const res = await $fetch<any>('/api/security-groups/evaluate', {
      method: 'POST',
      body: { groupIdentifiers: groupList }
    })
    testResult.value = {
      inputCount: groupList.length,
      matchedCount: res.matchedGroups?.length || 0,
      matchedMappings: res.matchedGroups || [],
      resolvedRoles: Array.from(new Set((res.matchedGroups || []).map((m: any) => m.mappedRole))),
      targetOrganizations: res.targetOrganizations || [],
      suggestedActiveOrganization: res.suggestedActiveOrganization || null,
      resolvedOrganizationId: res.targetOrganizations?.[0]?.name || null
    }
  } catch (err) {
    // Fallback evaluation
    const matched = mappings.value.filter(m => m.isEnabled && groupList.some(g => g.toLowerCase() === m.groupIdentifier.toLowerCase()))
    testResult.value = {
      inputCount: groupList.length,
      matchedCount: matched.length,
      matchedMappings: matched,
      resolvedRoles: Array.from(new Set(matched.map(m => m.mappedRole))),
      targetOrganizations: matched.filter(m => m.organizationId).map(m => ({
        name: m.organizationId!,
        slug: m.organizationId!.toLowerCase().replace(/\s+/g, '-'),
        role: m.mappedRole.includes('admin') ? 'admin' : 'member'
      })),
      suggestedActiveOrganization: matched.find(m => m.organizationId)?.organizationId || null,
      resolvedOrganizationId: matched.find(m => m.organizationId)?.organizationId || null
    }
  } finally {
    evaluating.value = false
  }
}

onMounted(() => {
  fetchMappings()
  fetchOus()
  fetchSsoStatus()
  fetchApiKeys()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight flex items-center gap-2">
          <ShieldCheckIcon class="h-7 w-7 text-primary" />
          Active Directory & Entra ID Security Groups
        </h1>
        <p class="text-sm text-muted-foreground mt-1">
          Dynamically map enterprise directory groups to Heimdall RBAC roles and tenant boundary policies.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" @click="fetchMappings" :disabled="loading">
          <RefreshCwIcon class="h-4 w-4 mr-2" :class="{ 'animate-spin': loading }" />
          Refresh
        </Button>
        <RbacButton
          size="sm"
          :has-permission="canApproveOus"
          :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
          @click="isCreating = !isCreating"
        >
          <PlusIcon class="h-4 w-4 mr-2" />
          Add Group Mapping
        </RbacButton>
      </div>
    </div>

    <!-- Microsoft Entra ID (Azure SSO) Integration Banner -->
    <Card class="border-sky-500/30 bg-sky-500/5 dark:bg-sky-950/20 shadow-xs">
      <CardContent class="p-4 flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
        <div class="flex items-center gap-3.5">
          <div class="p-2 rounded-lg bg-background border border-border shadow-xs shrink-0">
            <svg class="size-6" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="1" y="1" width="9" height="9" fill="#F25022"/>
              <rect x="11" y="1" width="9" height="9" fill="#7FBA00"/>
              <rect x="1" y="11" width="9" height="9" fill="#00A4EF"/>
              <rect x="11" y="11" width="9" height="9" fill="#FFB900"/>
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-bold text-foreground">Microsoft Entra ID (Azure SSO) Identity Provider</span>
              <Badge 
                :variant="ssoStatus?.isConfigured ? 'default' : 'outline'" 
                class="text-[10px] font-mono uppercase tracking-wider"
                :class="ssoStatus?.isConfigured ? 'bg-emerald-600 text-white' : 'border-sky-500/40 text-sky-400 bg-sky-500/10'"
              >
                {{ ssoStatus?.isConfigured ? 'Cloud SSO Active' : 'Dev Sandbox Active' }}
              </Badge>
            </div>
            <div class="text-xs text-muted-foreground mt-1 flex flex-wrap items-center gap-x-3 gap-y-1">
              <span>Tenant: <code class="font-mono text-[11px] text-foreground font-semibold">{{ ssoStatus?.tenantId || '72f988bf-86f1-41af-91ab-2d7cd011db47' }}</code></span>
              <span>•</span>
              <span>Transformer: <span class="text-emerald-500 font-medium">DynamicSecurityGroupClaimsTransformer</span></span>
              <span>•</span>
              <span>Scopes: <code class="font-mono text-[11px]">openid, profile, email, User.Read</code></span>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2 self-end md:self-center shrink-0">
          <Button 
            variant="outline" 
            size="sm" 
            class="text-xs h-8"
            @click="applyPreset('Sally Vance (Lead Controls)')"
          >
            Test Entra ID Claim
          </Button>
        </div>
      </CardContent>
    </Card>

    <!-- Alert Notices -->
    <div v-if="error" class="p-4 rounded-lg bg-destructive/15 text-destructive text-sm flex items-center gap-2 border border-destructive/30">
      <AlertCircleIcon class="h-4 w-4 shrink-0" />
      <span>{{ error }}</span>
    </div>
    <div v-if="successMsg" class="p-4 rounded-lg bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 text-sm flex items-center gap-2 border border-emerald-500/30">
      <CheckCircle2Icon class="h-4 w-4 shrink-0" />
      <span>{{ successMsg }}</span>
    </div>

    <!-- Add Mapping Card -->
    <Card v-if="isCreating" class="border-primary/40 bg-card/60 backdrop-blur">
      <CardHeader>
        <CardTitle class="text-lg">Create New Security Group Mapping</CardTitle>
        <CardDescription>
          Specify the provider ID, group Object ID / Distinguished Name, and target Heimdall role.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="text-xs font-semibold text-muted-foreground uppercase">Identity Provider</label>
            <select v-model="newIdp" class="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
              <option value="EntraID">Microsoft Entra ID (Azure AD)</option>
              <option value="ActiveDirectory">On-Prem Active Directory / LDAP</option>
              <option value="OIDC">Generic OpenID Connect</option>
            </select>
          </div>

          <div class="md:col-span-2">
            <label class="text-xs font-semibold text-muted-foreground uppercase">Group Identifier (Object ID / DN / SID)</label>
            <Input v-model="newGroupId" placeholder="e.g. 9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c or CN=OT-Admins..." class="mt-1" />
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label class="text-xs font-semibold text-muted-foreground uppercase">Display Name</label>
            <Input v-model="newDisplayName" placeholder="e.g. OT Plant Controls Engineers" class="mt-1" />
          </div>

          <div>
            <label class="text-xs font-semibold text-muted-foreground uppercase">Mapped Heimdall Role</label>
            <select v-model="newMappedRole" class="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
              <option v-for="r in rolesList" :key="r" :value="r">{{ r }}</option>
            </select>
          </div>

          <div>
            <label class="text-xs font-semibold text-muted-foreground uppercase">Target Organization (Auto-Provisioned)</label>
            <input 
              v-model="newOrgId" 
              list="standard-orgs" 
              placeholder="e.g. Line 06 – Battery Module Line" 
              class="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" 
            />
            <datalist id="standard-orgs">
              <option v-for="org in standardOrganizations" :key="org" :value="org" />
            </datalist>
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-2">
          <Button variant="ghost" size="sm" @click="isCreating = false">Cancel</Button>
          <Button size="sm" @click="createMapping" :disabled="loading">Save Mapping</Button>
        </div>
      </CardContent>
    </Card>

    <!-- Mappings Table -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base flex items-center justify-between">
          <span>Configured Directory Group Mappings ({{ mappings.length }})</span>
          <Badge variant="outline" class="text-xs">Dynamic Claims Transform Active</Badge>
        </CardTitle>
      </CardHeader>
      <CardContent class="p-0">
        <div class="overflow-x-auto">
          <table class="w-full text-sm text-left">
            <thead class="bg-muted/50 text-muted-foreground text-xs uppercase border-b border-border">
              <tr>
                <th class="px-4 py-3">Group Name & Provider</th>
                <th class="px-4 py-3">Group Identifier (GUID / DN)</th>
                <th class="px-4 py-3">Mapped Role</th>
                <th class="px-4 py-3">Tenant / Floor</th>
                <th class="px-4 py-3">Status</th>
                <th class="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="m in mappings" :key="m.id" class="hover:bg-muted/30 transition-colors">
                <td class="px-4 py-3">
                  <div class="font-medium text-foreground flex items-center gap-1.5 flex-wrap">
                    <span>{{ m.displayName }}</span>
                    <Badge 
                      v-if="m.sourceTicketId" 
                      variant="outline" 
                      class="text-[10px] font-mono border-sky-500/40 text-sky-500 bg-sky-500/10 flex items-center gap-1 px-1.5 py-0 h-4"
                      :title="`Automated via ${m.sourceSystem || 'Ticketing'}: ${m.sourceTicketId}`"
                    >
                      <TicketIcon class="size-2.5" />
                      {{ m.sourceTicketId }}
                    </Badge>
                  </div>
                  <div class="text-xs text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-0.5 mt-0.5">
                    <span class="flex items-center gap-1">
                      <ServerIcon class="h-3 w-3" />
                      {{ m.identityProvider }}
                    </span>
                    <span v-if="m.sourceSystem" class="text-[11px] text-muted-foreground/80">• {{ m.sourceSystem }}</span>
                    <span v-if="m.requestedBy" class="text-[11px] text-muted-foreground/80">• By: {{ m.requestedBy }}</span>
                  </div>
                </td>

                <td class="px-4 py-3 font-mono text-xs text-muted-foreground truncate max-w-[280px]">
                  {{ m.groupIdentifier }}
                </td>
                <td class="px-4 py-3">
                  <Badge 
                    :variant="m.mappedRole.includes('admin') ? 'destructive' : m.mappedRole.includes('engineer') ? 'default' : 'secondary'"
                    class="capitalize"
                  >
                    {{ m.mappedRole }}
                  </Badge>
                </td>
                <td class="px-4 py-3 text-xs text-muted-foreground">
                  {{ m.organizationId || 'Global (All Floors)' }}
                </td>
                <td class="px-4 py-3">
                  <button 
                    @click="toggleMapping(m)"
                    class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium cursor-pointer transition-colors"
                    :class="m.isEnabled ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400' : 'bg-muted text-muted-foreground'"
                  >
                    <span class="h-1.5 w-1.5 rounded-full" :class="m.isEnabled ? 'bg-emerald-500' : 'bg-muted-foreground'" />
                    {{ m.isEnabled ? 'Active' : 'Disabled' }}
                  </button>
                </td>
                <td class="px-4 py-3 text-right">
                  <RbacButton
                    :has-permission="canApproveOus"
                    :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
                    variant="ghost"
                    size="icon"
                    class="h-8 w-8 text-destructive hover:text-destructive"
                    @click="deleteMapping(m.id)"
                  >
                    <Trash2Icon class="h-4 w-4" />
                  </RbacButton>
                </td>
              </tr>
              <tr v-if="mappings.length === 0">
                <td colspan="6" class="px-4 py-8 text-center text-muted-foreground text-sm">
                  No directory group mappings defined yet. Click "Add Group Mapping" to register Entra ID / AD groups.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>

    <!-- Active Directory & Entra ID Organizational Unit (OU) Access Governance -->
    <Card class="border-border/80">
      <CardHeader>
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-2">
          <div>
            <CardTitle class="text-base flex items-center gap-2">
              <ServerIcon class="h-5 w-5 text-cyan-500" />
              <span>Active Directory & Entra ID Organizational Unit (OU) Governance</span>
            </CardTitle>
            <CardDescription class="mt-1">
              IT Administrators control and approve OUs for Read/Write ingestion or Read-Only discovery into Heimdall inventory.
            </CardDescription>
          </div>
          <div class="flex items-center gap-2">
            <Badge variant="outline" class="text-xs uppercase font-mono tracking-wide text-cyan-400 border-cyan-500/40">
              IT Administrator Authority
            </Badge>
            <Button variant="outline" size="sm" @click="fetchOus" :disabled="loadingOus">
              <RefreshCwIcon class="h-3.5 w-3.5 mr-1.5" :class="{ 'animate-spin': loadingOus }" />
              Sync OUs
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent class="space-y-4">
        <div v-if="!canApproveOus" class="p-3 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-400 text-xs flex items-center gap-2">
          <AlertCircleIcon class="h-4 w-4 shrink-0" />
          <span>Restricted Access: Organizational Unit approval actions require IT Administrator (it_admin) or System Administrator (system_admin) privileges.</span>
        </div>

        <div class="overflow-x-auto rounded-md border border-border">
          <table class="w-full text-sm text-left">
            <thead class="bg-muted/50 text-muted-foreground text-xs uppercase border-b border-border">
              <tr>
                <th class="px-4 py-3">Discovered OU & Path</th>
                <th class="px-4 py-3">Network & Subnet</th>
                <th class="px-4 py-3 text-center">Discovered Hosts</th>
                <th class="px-4 py-3">Governance Status</th>
                <th class="px-4 py-3">Approval Details</th>
                <th class="px-4 py-3 text-right">IT Approval Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="ou in ousList" :key="ou.ouPath" class="hover:bg-muted/30 transition-colors">
                <td class="px-4 py-3">
                  <div class="font-semibold text-foreground text-xs">{{ ou.name || ou.ouPath }}</div>
                  <div class="font-mono text-[11px] text-muted-foreground break-all">{{ ou.ouPath }}</div>
                  <div v-if="ou.purpose" class="text-[11px] text-muted-foreground mt-0.5">Role: {{ ou.purpose }}</div>
                </td>
                <td class="px-4 py-3">
                  <div class="text-xs font-medium">{{ ou.vlanName }}</div>
                  <div class="font-mono text-[11px] text-muted-foreground">{{ ou.subnet }}</div>
                </td>
                <td class="px-4 py-3 text-center">
                  <Badge variant="secondary" class="font-mono text-xs">
                    {{ ou.hostCount }} {{ ou.hostCount === 1 ? 'host' : 'hosts' }}
                  </Badge>
                </td>
                <td class="px-4 py-3">
                  <Badge 
                    v-if="ou.accessLevel === 'read_write' && ou.isApproved"
                    variant="outline" 
                    class="bg-emerald-500/15 text-emerald-400 border-emerald-500/40 text-[10px] font-bold uppercase tracking-wider"
                  >
                    Read / Write Approved
                  </Badge>
                  <Badge 
                    v-else-if="ou.accessLevel === 'read_only' && ou.isApproved"
                    variant="outline" 
                    class="bg-amber-500/15 text-amber-400 border-amber-500/40 text-[10px] font-bold uppercase tracking-wider"
                  >
                    Read-Only (Discovery)
                  </Badge>
                  <Badge 
                    v-else 
                    variant="outline" 
                    class="bg-rose-500/15 text-rose-400 border-rose-500/40 text-[10px] font-bold uppercase tracking-wider"
                  >
                    Unapproved / Blocked
                  </Badge>
                </td>
                <td class="px-4 py-3 text-xs text-muted-foreground">
                  <div v-if="ou.isApproved">
                    <div>By: <span class="font-semibold text-foreground">{{ ou.approvedBy || 'it_admin' }}</span></div>
                    <div v-if="ou.approvedAt" class="text-[10px]">{{ new Date(ou.approvedAt).toLocaleString() }}</div>
                  </div>
                  <div v-else class="italic text-[11px]">Pending IT Approval</div>
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="inline-flex items-center gap-1.5">
                    <RbacButton 
                      size="sm" 
                      variant="outline" 
                      class="h-7 text-[11px] hover:bg-emerald-500/20 hover:text-emerald-400 hover:border-emerald-500/50"
                      :has-permission="canApproveOus"
                      :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
                      :disabled="approvingOuPath === ou.ouPath || (ou.accessLevel === 'read_write' && ou.isApproved)"
                      @click="handleSetOuGovernance(ou.ouPath, 'read_write')"
                    >
                      <CheckCircle2Icon class="h-3.5 w-3.5 mr-1 text-emerald-400" />
                      Approve R/W
                    </RbacButton>
                    <RbacButton 
                      size="sm" 
                      variant="outline" 
                      class="h-7 text-[11px] hover:bg-amber-500/20 hover:text-amber-400 hover:border-amber-500/50"
                      :has-permission="canApproveOus"
                      :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
                      :disabled="approvingOuPath === ou.ouPath || (ou.accessLevel === 'read_only' && ou.isApproved)"
                      @click="handleSetOuGovernance(ou.ouPath, 'read_only')"
                    >
                      Read-Only
                    </RbacButton>
                    <RbacButton 
                      size="sm" 
                      variant="ghost" 
                      class="h-7 text-[11px] text-destructive hover:text-destructive"
                      :has-permission="canApproveOus"
                      :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
                      :disabled="approvingOuPath === ou.ouPath || (!ou.isApproved && ou.accessLevel === 'unapproved')"
                      @click="handleSetOuGovernance(ou.ouPath, 'unapproved')"
                    >
                      Revoke
                    </RbacButton>
                  </div>
                </td>
              </tr>
              <tr v-if="ousList.length === 0">
                <td colspan="6" class="px-4 py-8 text-center text-muted-foreground text-sm">
                  <div v-if="loadingOus" class="flex items-center justify-center gap-2">
                    <RefreshCwIcon class="h-4 w-4 animate-spin" />
                    <span>Loading Organizational Units...</span>
                  </div>
                  <span v-else>No Active Directory OUs discovered.</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>

    <!-- Outside IT Automation & External Ticketing System API -->
    <Card class="border-border/80 shadow-xs">
      <CardHeader>
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-3">
          <div>
            <CardTitle class="text-base flex items-center gap-2">
              <KeyIcon class="h-5 w-5 text-amber-500" />
              <span>Outside IT Automation & External Ticketing API</span>
            </CardTitle>
            <CardDescription class="mt-1">
              Issue secure API Keys for external IT automation platforms (ServiceNow, Jira Service Management, Ansible, PowerShell) to dynamically provision security group mappings, pre-flight evaluate user claims, and auto-link change tickets.
            </CardDescription>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <Badge variant="outline" class="text-[11px] font-mono border-amber-500/40 text-amber-500 bg-amber-500/10">
              X-API-Key & Bearer Auth
            </Badge>
            <Button variant="outline" size="sm" @click="fetchApiKeys" :disabled="loadingKeys">
              <RefreshCwIcon class="h-3.5 w-3.5 mr-1.5" :class="{ 'animate-spin': loadingKeys }" />
              Refresh
            </Button>
            <RbacButton
              size="sm"
              :has-permission="canApproveOus"
              :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
              class="bg-amber-600 hover:bg-amber-700 text-white"
              @click="isCreatingKey = !isCreatingKey"
            >
              <PlusIcon class="h-4 w-4 mr-1.5" />
              Generate API Key
            </RbacButton>
          </div>
        </div>
      </CardHeader>
      <CardContent class="space-y-5">
        <!-- One-Time Secret Key Reveal Alert -->
        <div 
          v-if="generatedRawKey" 
          class="p-4 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-900 dark:text-emerald-200 space-y-2"
        >
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 font-semibold text-sm">
              <CheckCircle2Icon class="h-4 w-4 text-emerald-500 shrink-0" />
              <span>New API Key Generated Successfully</span>
            </div>
            <Button variant="ghost" size="sm" class="h-6 text-xs" @click="generatedRawKey = null">
              Dismiss
            </Button>
          </div>
          <p class="text-xs text-muted-foreground">
            Please copy this secret key now. Heimdall stores only a cryptographic SHA-256 hash; this plain secret cannot be retrieved again.
          </p>
          <div class="flex items-center gap-2 pt-1">
            <input 
              readonly 
              :value="generatedRawKey" 
              class="flex-1 font-mono text-xs bg-background/80 border border-border px-3 py-1.5 rounded text-foreground select-all focus:outline-none focus:ring-1 focus:ring-emerald-500" 
            />
            <Button size="sm" variant="outline" class="h-8 shrink-0 text-xs" @click="copyToClipboard(generatedRawKey)">
              <CheckIcon v-if="keyCopied" class="h-3.5 w-3.5 mr-1.5 text-emerald-500" />
              <CopyIcon v-else class="h-3.5 w-3.5 mr-1.5" />
              {{ keyCopied ? 'Copied!' : 'Copy Key' }}
            </Button>
          </div>
        </div>

        <!-- Key Generation Form Card -->
        <div v-if="isCreatingKey" class="p-4 rounded-lg bg-muted/40 border border-border space-y-4">
          <div class="flex items-center justify-between">
            <h4 class="text-sm font-semibold text-foreground flex items-center gap-2">
              <KeyIcon class="h-4 w-4 text-amber-500" />
              Generate Automation API Key for Outside IT
            </h4>
            <Button variant="ghost" size="sm" class="h-7 text-xs" @click="isCreatingKey = false">
              Cancel
            </Button>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="md:col-span-2">
              <label class="text-xs font-semibold text-muted-foreground uppercase">Key Name / Integration Label</label>
              <Input 
                v-model="newKeyName" 
                placeholder="e.g. ServiceNow ITSM - Emergency Delegation Workflow" 
                class="mt-1" 
              />
            </div>
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Target IT System</label>
              <select v-model="newKeySystem" class="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
                <option value="ServiceNow">ServiceNow (ITSM / REST Message)</option>
                <option value="Jira">Jira Service Management (Webhooks)</option>
                <option value="Ansible">Ansible / DevOps Pipeline</option>
                <option value="PowerAutomate">Microsoft Power Automate</option>
                <option value="Custom">Custom IT Script / Python / PowerShell</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Granted Scopes / Capabilities</label>
              <div class="mt-1.5 space-y-1.5 p-3 rounded border border-input bg-background text-xs">
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" value="security_groups:read" v-model="newKeyScopes" class="rounded border-input text-amber-600 focus:ring-amber-500" />
                  <span><code>security_groups:read</code> - Query active group mappings & OUs</span>
                </label>
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" value="security_groups:write" v-model="newKeyScopes" class="rounded border-input text-amber-600 focus:ring-amber-500" />
                  <span><code>security_groups:write</code> - Create, update, deprovision group mappings</span>
                </label>
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" value="security_groups:evaluate" v-model="newKeyScopes" class="rounded border-input text-amber-600 focus:ring-amber-500" />
                  <span><code>security_groups:evaluate</code> - Pre-flight claims check for ticket approvals</span>
                </label>
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" value="tickets:create" v-model="newKeyScopes" class="rounded border-input text-amber-600 focus:ring-amber-500" />
                  <span><code>tickets:create</code> - Auto-link & record Heimdall audit governance tickets</span>
                </label>
              </div>
            </div>

            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Key Expiration Period</label>
              <select v-model="newKeyExpiresDays" class="mt-1.5 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
                <option :value="30">30 Days (Recommended for contractor/temp flows)</option>
                <option :value="90">90 Days (Enterprise Standard Rotation)</option>
                <option :value="365">1 Year</option>
                <option :value="null">Never Expire (Continuous production daemon)</option>
              </select>

              <label class="text-xs font-semibold text-muted-foreground uppercase mt-3 block">Description / Change Note (Optional)</label>
              <Input 
                v-model="newKeyDescription" 
                placeholder="e.g. Used by ServiceNow Change Request automation runner" 
                class="mt-1" 
              />
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-1">
            <Button variant="ghost" size="sm" @click="isCreatingKey = false">Cancel</Button>
            <Button size="sm" class="bg-amber-600 hover:bg-amber-700 text-white" @click="handleGenerateKey" :disabled="loadingKeys">
              <KeyIcon class="h-3.5 w-3.5 mr-1.5" />
              Generate Secret Key
            </Button>
          </div>
        </div>

        <!-- Registered API Keys Table -->
        <div class="overflow-x-auto rounded-md border border-border">
          <table class="w-full text-sm text-left">
            <thead class="bg-muted/50 text-muted-foreground text-xs uppercase border-b border-border">
              <tr>
                <th class="px-4 py-3">Key Label & Target System</th>
                <th class="px-4 py-3">Key Prefix (Masked)</th>
                <th class="px-4 py-3">Permissions / Scopes</th>
                <th class="px-4 py-3">Created & Expiration</th>
                <th class="px-4 py-3">Last Activity</th>
                <th class="px-4 py-3">Status</th>
                <th class="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="k in apiKeys" :key="k.id" class="hover:bg-muted/30 transition-colors">
                <td class="px-4 py-3">
                  <div class="font-semibold text-foreground text-xs">{{ k.name }}</div>
                  <div class="text-[11px] text-muted-foreground flex items-center gap-1 mt-0.5">
                    <span class="inline-block w-1.5 h-1.5 rounded-full bg-amber-500"></span>
                    <span>{{ k.systemType }}</span>
                    <span v-if="k.description" class="truncate max-w-[200px]" :title="k.description">({{ k.description }})</span>
                  </div>
                </td>
                <td class="px-4 py-3">
                  <div class="inline-flex items-center gap-1.5 font-mono text-xs bg-muted/60 px-2 py-0.5 rounded border border-border/50 text-muted-foreground">
                    <span>{{ k.keyPrefix }}</span>
                    <button 
                      class="hover:text-foreground transition-colors cursor-pointer" 
                      title="Copy Key Prefix"
                      @click="copyToClipboard(k.keyPrefix)"
                    >
                      <CopyIcon class="size-3" />
                    </button>
                  </div>
                </td>
                <td class="px-4 py-3">
                  <div class="flex flex-wrap gap-1">
                    <Badge 
                      v-for="s in k.scopes" 
                      :key="s" 
                      variant="secondary" 
                      class="text-[10px] font-mono font-normal px-1.5 py-0"
                    >
                      {{ s.replace('security_groups:', 'sg:') }}
                    </Badge>
                  </div>
                </td>
                <td class="px-4 py-3 text-xs text-muted-foreground">
                  <div>{{ new Date(k.createdAt).toLocaleDateString() }}</div>
                  <div v-if="k.expiresAt" class="text-[10px] text-amber-500 font-mono">
                    Exp: {{ new Date(k.expiresAt).toLocaleDateString() }}
                  </div>
                  <div v-else class="text-[10px] text-emerald-500 font-mono">
                    Never Expires
                  </div>
                </td>
                <td class="px-4 py-3 text-xs text-muted-foreground font-mono">
                  {{ k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleString() : 'Never used' }}
                </td>
                <td class="px-4 py-3">
                  <Badge 
                    :variant="k.isActive ? 'default' : 'secondary'"
                    class="text-[10px] uppercase font-bold"
                    :class="k.isActive ? 'bg-emerald-600 text-white' : 'bg-muted text-muted-foreground'"
                  >
                    {{ k.isActive ? 'Active' : 'Revoked' }}
                  </Badge>
                </td>
                <td class="px-4 py-3 text-right">
                  <RbacButton
                    :has-permission="canApproveOus"
                    :tooltip="RBAC_TOOLTIPS.IT_ADMIN"
                    variant="ghost"
                    size="icon"
                    class="h-7 w-7 text-destructive hover:text-destructive"
                    title="Revoke API Key"
                    @click="handleRevokeKey(k.id)"
                  >
                    <Trash2Icon class="h-3.5 w-3.5" />
                  </RbacButton>
                </td>
              </tr>
              <tr v-if="apiKeys.length === 0">
                <td colspan="7" class="px-4 py-6 text-center text-muted-foreground text-xs">
                  <div v-if="loadingKeys" class="flex items-center justify-center gap-2">
                    <RefreshCwIcon class="h-3.5 w-3.5 animate-spin" />
                    <span>Loading API Keys...</span>
                  </div>
                  <span v-else>No outside IT automation API keys registered yet. Click "Generate API Key" to authorize ticketing systems.</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Ticketing Integration Docs & Interactive Sandbox Tabs -->
        <div class="border border-border/80 rounded-lg p-4 bg-muted/20 space-y-4">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 border-b border-border/60 pb-3">
            <div>
              <h4 class="text-sm font-semibold text-foreground flex items-center gap-2">
                <TerminalIcon class="h-4 w-4 text-primary" />
                Integration Code Snippets & Interactive Ticketing Sandbox
              </h4>
              <p class="text-xs text-muted-foreground mt-0.5">
                Ready-to-use payloads and scripts for ServiceNow Flow Designer, Jira Webhooks, and direct REST clients.
              </p>
            </div>
            <!-- Doc Language Selector -->
            <div class="flex items-center gap-1 bg-muted/60 p-1 rounded-md border border-border/50 text-xs">
              <button 
                v-for="tab in (['curl', 'jira', 'serviceNow', 'python', 'powershell'] as const)" 
                :key="tab"
                class="px-2.5 py-1 rounded transition-colors capitalize text-xs"
                :class="activeDocTab === tab ? 'bg-background font-semibold text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                @click="activeDocTab = tab"
              >
                {{ tab === 'serviceNow' ? 'ServiceNow' : tab === 'jira' ? 'Jira' : tab === 'powershell' ? 'PowerShell' : tab }}
              </button>
            </div>
          </div>

          <!-- Tab Content: Code Snippet -->
          <div class="relative">
            <pre class="p-3.5 rounded-md bg-zinc-950 text-zinc-100 font-mono text-xs overflow-x-auto border border-zinc-800 leading-relaxed selection:bg-primary/30"><code><template v-if="activeDocTab === 'curl'">curl -X POST https://heimdall.plant.corp/api/v1/automation/security-groups/mappings \
  -H "X-API-Key: {{ generatedRawKey || 'hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c' }}" \
  -H "Content-Type: application/json" \
  -d '{
    "identityProvider": "EntraID",
    "groupIdentifier": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
    "displayName": "OT Plant Battery Line Engineers",
    "mappedRole": "controls_engineer",
    "organizationId": "Line 06 – Battery Module Line",
    "ticketId": "INC0091823",
    "requestedBy": "controls.lead@factory.corp",
    "reason": "Emergency Controls Engineer Access for Line 06 Battery Line",
    "autoCreateAuditTicket": true
  }'</template><template v-else-if="activeDocTab === 'serviceNow'">// In ServiceNow Scripted REST Message / Flow Designer Action
var request = new sn_ws.RESTMessageV2();
request.setEndpoint('https://heimdall.plant.corp/api/v1/automation/security-groups/mappings');
request.setHttpMethod('POST');
request.setRequestHeader('X-API-Key', '{{ generatedRawKey || "hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c" }}');
request.setRequestHeader('Content-Type', 'application/json');

var payload = {
  identityProvider: 'EntraID',
  groupIdentifier: current.variables.ad_group_id.toString(),
  displayName: current.variables.group_name.toString(),
  mappedRole: current.variables.target_role.toString(),
  organizationId: 'Line 06 – Battery Module Line',
  ticketId: current.getValue('number'), // e.g. RITM004128
  requestedBy: current.requested_for.getDisplayValue(),
  reason: current.getValue('short_description'),
  autoCreateAuditTicket: true
};

request.setRequestBody(JSON.stringify(payload));
var response = request.execute();</template><template v-else-if="activeDocTab === 'jira'">// Jira Automation Rule -> Action: "Send web request"
// URL: https://heimdall.plant.corp/api/v1/automation/security-groups/mappings
// Method: POST
// Headers:
//   X-API-Key: {{ generatedRawKey || "hmd_auto_jira_demo_1a2b3c4d5e6f7a8b9c" }}
//   Content-Type: application/json
// Body: Custom data:
{
  "identityProvider": "EntraID",
  "groupIdentifier": "{{issue.customfield_10020}}",
  "displayName": "{{issue.summary}}",
  "mappedRole": "controls_engineer",
  "organizationId": "Line 06 – Battery Module Line",
  "ticketId": "{{issue.key}}",
  "requestedBy": "{{issue.reporter.emailAddress}}",
  "reason": "{{issue.description}}",
  "autoCreateAuditTicket": true
}</template><template v-else-if="activeDocTab === 'python'">import requests

HEIMDALL_URL = "https://heimdall.plant.corp"
API_KEY = "{{ generatedRawKey || 'hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c' }}"

response = requests.post(
    f"{HEIMDALL_URL}/api/v1/automation/security-groups/mappings",
    headers={
        "X-API-Key": API_KEY,
        "Content-Type": "application/json"
    },
    json={
        "identityProvider": "EntraID",
        "groupIdentifier": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
        "displayName": "OT Plant Battery Line Engineers",
        "mappedRole": "controls_engineer",
        "organizationId": "Line 06 – Battery Module Line",
        "ticketId": "CHG0028192",
        "requestedBy": "lead.controls@plant.corp",
        "reason": "Scheduled change request approval",
        "autoCreateAuditTicket": True
    },
    timeout=10
)
print("Response:", response.status_code, response.json())</template><template v-else-if="activeDocTab === 'powershell'">$headers = @{
    "X-API-Key" = "{{ generatedRawKey || 'hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c' }}"
    "Content-Type" = "application/json"
}

$body = @{
    identityProvider = "ActiveDirectory"
    groupIdentifier = "CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp"
    displayName = "On-Prem Controls Engineers"
    mappedRole = "engineer"
    organizationId = "Production Floor B"
    ticketId = "INC-10928"
    requestedBy = "it.support@factory.corp"
    reason = "Automated Active Directory sync from ticketing pipeline"
    autoCreateAuditTicket = $true
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://heimdall.plant.corp/api/v1/automation/security-groups/mappings" -Method Post -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 4</template></code></pre>
          </div>

          <!-- Interactive Test Runner -->
          <div class="pt-2 border-t border-border/50">
            <h5 class="text-xs font-semibold text-muted-foreground uppercase mb-3 flex items-center gap-1.5">
              <PlayIcon class="size-3 text-amber-500" />
              Live Interactive Automation API Sandbox
            </h5>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div class="space-y-3">
                <div class="grid grid-cols-2 gap-2">
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">Target Action</label>
                    <select v-model="testAction" class="mt-1 flex h-8 w-full rounded-md border border-input bg-background px-2 text-xs">
                      <option value="create_mapping">Fulfill Ticket & Provision Mapping</option>
                      <option value="evaluate">Pre-flight Evaluate User Claims</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">External Ticket ID</label>
                    <Input v-model="testTicketId" placeholder="e.g. INC0091823" class="mt-1 h-8 text-xs font-mono" />
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2">
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">Group Identifier (GUID/DN)</label>
                    <Input v-model="testGroupId" class="mt-1 h-8 text-xs font-mono" />
                  </div>
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">Display Name</label>
                    <Input v-model="testDisplayName" class="mt-1 h-8 text-xs" />
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-2">
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">Target Role</label>
                    <select v-model="testMappedRole" class="mt-1 flex h-8 w-full rounded-md border border-input bg-background px-2 text-xs">
                      <option v-for="r in rolesList" :key="r" :value="r">{{ r }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[11px] font-semibold text-muted-foreground uppercase">Target Plant Floor / Org</label>
                    <Input v-model="testOrg" class="mt-1 h-8 text-xs" />
                  </div>
                </div>

                <div>
                  <label class="text-[11px] font-semibold text-muted-foreground uppercase">Requester Email & Justification</label>
                  <div class="grid grid-cols-2 gap-2 mt-1">
                    <Input v-model="testRequester" placeholder="engineer@plant.corp" class="h-8 text-xs" />
                    <Input v-model="testReason" placeholder="Reason for change..." class="h-8 text-xs" />
                  </div>
                </div>

                <Button 
                  size="sm" 
                  class="w-full bg-amber-600 hover:bg-amber-700 text-white text-xs h-8"
                  :disabled="isCallingAutomation"
                  @click="executeAutomationTest"
                >
                  <RefreshCwIcon v-if="isCallingAutomation" class="size-3.5 mr-1.5 animate-spin" />
                  <PlayIcon v-else class="size-3.5 mr-1.5" />
                  Execute Automation Webhook Request
                </Button>
              </div>

              <!-- Output Pane -->
              <div class="bg-background/80 rounded-md border border-border p-3 flex flex-col justify-between">
                <div>
                  <div class="flex items-center justify-between pb-2 border-b border-border/50 text-xs font-mono">
                    <span class="text-muted-foreground">HTTP Response:</span>
                    <Badge 
                      v-if="automationOutput" 
                      :variant="automationOutput.status < 300 ? 'default' : 'destructive'"
                      class="text-[10px] font-mono"
                      :class="automationOutput.status < 300 ? 'bg-emerald-600 text-white' : ''"
                    >
                      {{ automationOutput.status }} {{ automationOutput.statusText }}
                    </Badge>
                    <span v-else class="text-[11px] text-muted-foreground">Idle</span>
                  </div>

                  <div v-if="automationOutput" class="mt-2 text-xs font-mono overflow-auto max-h-[190px]">
                    <pre class="text-[11px] text-foreground leading-snug"><code>{{ JSON.stringify(automationOutput.data, null, 2) }}</code></pre>
                  </div>
                  <div v-else class="text-xs text-muted-foreground text-center py-10 italic">
                    Click "Execute Automation Webhook Request" to simulate how ServiceNow or Jira executes this API call with the API key.
                  </div>
                </div>

                <div v-if="automationOutput?.data?.auditTrail" class="pt-2 border-t border-border/40 text-[11px] text-emerald-500 font-mono flex items-center justify-between">
                  <span>Linked Governance Ticket:</span>
                  <span class="font-bold underline">{{ automationOutput.data.auditTrail.ticketNumber }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Interactive Claims Evaluation Sandbox -->
    <Card v-if="enableSimulation" class="border-border/80">

      <CardHeader>
        <CardTitle class="text-base flex items-center gap-2">
          <LayersIcon class="h-5 w-5 text-primary" />
          Interactive Claims Evaluation Sandbox
        </CardTitle>
        <CardDescription>
          Simulate incoming JWT directory claims (such as Entra ID groups or AD SIDs) to verify resolved roles and tenant assignments in real-time.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <!-- Preset Selector -->
        <div class="flex flex-wrap items-center gap-2 p-3 bg-muted/40 rounded-lg border border-border/60">
          <span class="text-xs font-semibold text-muted-foreground uppercase mr-2">Quick Test Presets:</span>
          <Button 
            v-for="p in simulatedUserPresets" 
            :key="p.name"
            variant="outline" 
            size="sm"
            class="h-7 text-xs"
            @click="applyPreset(p.name)"
          >
            {{ p.name }}
          </Button>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label class="text-xs font-semibold text-muted-foreground uppercase">Simulated Directory Group IDs (One per line)</label>
            <textarea 
              v-model="testInputGroups" 
              rows="5" 
              class="mt-1.5 w-full rounded-md border border-input bg-background p-2.5 font-mono text-xs focus:outline-none focus:ring-1 focus:ring-primary"
              placeholder="Paste Group Object IDs or Distinguished Names..."
            />
            <div class="flex gap-2 mt-2">
              <Button size="sm" @click="runEvaluationTest" :disabled="evaluating">
                <PlayIcon class="h-3.5 w-3.5 mr-2" />
                Evaluate Claims & Org Provisioning
              </Button>
            </div>
          </div>

          <div class="bg-muted/30 rounded-lg p-4 border border-border">
            <div class="text-xs font-semibold text-muted-foreground uppercase mb-2">Evaluation Outcome</div>
            <div v-if="testResult" class="space-y-3 font-mono text-xs">
              <div class="flex justify-between items-center py-1 border-b border-border/50">
                <span class="text-muted-foreground">Input Groups:</span>
                <span class="font-bold">{{ testResult.inputCount }}</span>
              </div>
              <div class="flex justify-between items-center py-1 border-b border-border/50">
                <span class="text-muted-foreground">Matched Rules:</span>
                <span class="font-bold text-primary">{{ testResult.matchedCount }}</span>
              </div>
              <div>
                <span class="text-muted-foreground block mb-1">Resolved Effective Roles:</span>
                <div class="flex flex-wrap gap-1.5 mt-1">
                  <Badge v-for="r in testResult.resolvedRoles" :key="r" variant="default" class="font-mono text-xs capitalize">
                    {{ r }}
                  </Badge>
                  <span v-if="testResult.resolvedRoles?.length === 0" class="text-muted-foreground italic">No roles resolved (Fallback to default user)</span>
                </div>
              </div>

              <!-- Auto-Provisioned Organizations -->
              <div class="pt-1 border-t border-border/50">
                <span class="text-muted-foreground block mb-1.5">Auto-Provisioned Organizations:</span>
                <div v-if="testResult.targetOrganizations?.length > 0" class="space-y-1.5">
                  <div 
                    v-for="org in testResult.targetOrganizations" 
                    :key="org.name"
                    class="flex items-center justify-between p-2 rounded bg-background/60 border border-border/40"
                  >
                    <div>
                      <div class="font-semibold text-foreground text-xs">{{ org.name }}</div>
                      <div class="text-[10px] text-muted-foreground font-sans">Slug: {{ org.slug }}</div>
                    </div>
                    <Badge variant="outline" class="text-[10px] uppercase font-bold text-primary border-primary/40">
                      {{ org.role }}
                    </Badge>
                  </div>
                </div>
                <div v-else class="text-muted-foreground italic text-xs">
                  {{ testResult.resolvedOrganizationId || 'Global / Unrestricted Tenant' }}
                </div>
              </div>

              <div v-if="testResult.suggestedActiveOrganization" class="pt-1 text-[11px] text-emerald-500">
                Active Organization: <span class="font-bold">{{ testResult.suggestedActiveOrganization }}</span>
              </div>
            </div>
            <div v-else class="text-xs text-muted-foreground text-center py-8">
              Click a Quick Test Preset or "Evaluate Claims & Org Provisioning" to simulate group resolution.
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
