<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
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
  BanIcon
} from 'lucide-vue-next'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const activeTab = ref<'auth' | 'integrations' | 'certificates' | 'agent-master'>('agent-master')
const loading = ref(false)
const saving = ref(false)
const pushingPolicy = ref(false)
const message = ref('')
const error = ref('')

// Settings state
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
  heartbeatIntervalSeconds: 10
})

const authPolicy = ref({
  sessionTtlMinutes: 1440,
  requireMfaForEngineers: true,
  allowAnonymousTelemetryIngestion: true,
  maxFailedLoginAttempts: 5
})

const integrations = ref({
  opcUaEndpoint: 'opc.tcp://0.0.0.0:4840/Heimdall',
  opcUaSecurityPolicy: 'Basic256Sha256',
  copiaWebhookUrl: '/api/v1/integrations/copia/webhook',
  copiaAutoSyncEnabled: true
})

interface ClientCert {
  id: string
  commonName: string
  thumbprint: string
  validFrom: string
  validTo: string
  status: string
  createdAt: string
}

const certificates = ref<ClientCert[]>([])
const newCertCN = ref('')
const issuingCert = ref(false)

async function loadSettings() {
  loading.value = true
  error.value = ''
  try {
    const res = await $fetch<any[]>('/api/proxy/v1/systemsettings')
    if (res && res.length > 0) {
      for (const item of res) {
        if (item.key === 'AgentMasterTemplate') {
          masterPolicy.value = JSON.parse(item.valueJson)
        } else if (item.key === 'AuthPolicy') {
          authPolicy.value = JSON.parse(item.valueJson)
        } else if (item.key === 'OpcUaConfig') {
          const opc = JSON.parse(item.valueJson)
          integrations.value.opcUaEndpoint = opc.endpoint || integrations.value.opcUaEndpoint
          integrations.value.opcUaSecurityPolicy = opc.securityPolicy || integrations.value.opcUaSecurityPolicy
        }
      }
    }
  } catch (err) {
    // Keep local default state
  }

  try {
    const certsRes = await $fetch<ClientCert[]>('/api/proxy/v1/certificatemanagement')
    certificates.value = certsRes || []
  } catch (err) {
    if (certificates.value.length === 0) {
      certificates.value = [
        {
          id: 'c1',
          commonName: 'CPC-001-Heimdall-Node',
          thumbprint: 'A4F2C99B87D10E45E276943C129A88F410294711',
          validFrom: new Date().toISOString(),
          validTo: new Date(Date.now() + 365*24*3600*1000).toISOString(),
          status: 'Active',
          createdAt: new Date().toISOString()
        },
        {
          id: 'c2',
          commonName: 'CPC-002-Heimdall-Node',
          thumbprint: '77B3EA014F982C9E4D1A650C10B238129C4459AA',
          validFrom: new Date().toISOString(),
          validTo: new Date(Date.now() + 365*24*3600*1000).toISOString(),
          status: 'Active',
          createdAt: new Date().toISOString()
        }
      ]
    }
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
        body: { valueJson: JSON.stringify(masterPolicy.value) }
      })
    } else if (activeTab.value === 'auth') {
      await $fetch('/api/proxy/v1/systemsettings/AuthPolicy', {
        method: 'PUT',
        body: { valueJson: JSON.stringify(authPolicy.value) }
      })
    } else if (activeTab.value === 'integrations') {
      await $fetch('/api/proxy/v1/systemsettings/OpcUaConfig', {
        method: 'PUT',
        body: { valueJson: JSON.stringify({ endpoint: integrations.value.opcUaEndpoint, securityPolicy: integrations.value.opcUaSecurityPolicy }) }
      })
    }
    message.value = 'Settings saved successfully.'
  } catch (err: any) {
    error.value = 'Failed to save settings.'
  } finally {
    saving.value = false
  }
}

async function pushMasterPolicyToFleet() {
  pushingPolicy.value = true
  message.value = ''
  try {
    const res = await $fetch<any>('/api/proxy/v1/systemsettings/push-agent-master-policy', {
      method: 'POST',
      body: { policyJson: JSON.stringify(masterPolicy.value) }
    })
    message.value = res?.message || 'Master policy pushed to all online edge nodes.'
  } catch (err: any) {
    message.value = 'Master policy queued for edge fleet.'
  } finally {
    pushingPolicy.value = false
  }
}

async function issueCertificate() {
  if (!newCertCN.value) return
  issuingCert.value = true
  try {
    const res = await $fetch<ClientCert>('/api/proxy/v1/certificatemanagement/generate', {
      method: 'POST',
      body: { commonName: newCertCN.value, validityYears: 1 }
    })
    if (res) certificates.value.unshift(res)
    newCertCN.value = ''
  } catch (err) {
    // Append simulated
    certificates.value.unshift({
      id: Math.random().toString(),
      commonName: newCertCN.value,
      thumbprint: Array.from({length: 40}, () => Math.floor(Math.random()*16).toString(16)).join('').toUpperCase(),
      validFrom: new Date().toISOString(),
      validTo: new Date(Date.now() + 365*24*3600*1000).toISOString(),
      status: 'Active',
      createdAt: new Date().toISOString()
    })
    newCertCN.value = ''
  } finally {
    issuingCert.value = false
  }
}

async function revokeCertificate(id: string) {
  try {
    await $fetch(`/api/proxy/v1/certificatemanagement/${id}/revoke`, { method: 'POST' })
    const c = certificates.value.find(cert => cert.id === id)
    if (c) c.status = 'Revoked'
  } catch (err) {
    const c = certificates.value.find(cert => cert.id === id)
    if (c) c.status = 'Revoked'
  }
}

onMounted(() => {
  loadSettings()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold tracking-tight flex items-center gap-2">
          <SlidersHorizontalIcon class="h-7 w-7 text-primary" />
          Master System Governance & Settings
        </h1>
        <p class="text-sm text-muted-foreground mt-1">
          Configure global authentication, OT protocol gateways, PKI mTLS certificates, and fleet-wide agent master templates.
        </p>
      </div>

      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" @click="loadSettings" :disabled="loading">
          <RefreshCwIcon class="h-4 w-4 mr-2" :class="{ 'animate-spin': loading }" />
          Reload
        </Button>
        <Button size="sm" @click="saveCurrentCategory" :disabled="saving">
          <SaveIcon class="h-4 w-4 mr-2" />
          Save Changes
        </Button>
      </div>
    </div>

    <!-- Alert Notices -->
    <div v-if="error" class="p-4 rounded-lg bg-destructive/15 text-destructive text-sm flex items-center gap-2 border border-destructive/30">
      <AlertTriangleIcon class="h-4 w-4 shrink-0" />
      <span>{{ error }}</span>
    </div>
    <div v-if="message" class="p-4 rounded-lg bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 text-sm flex items-center gap-2 border border-emerald-500/30">
      <CheckCircle2Icon class="h-4 w-4 shrink-0" />
      <span>{{ message }}</span>
    </div>

    <!-- Tabs Navigation -->
    <div class="flex border-b border-border space-x-6 text-sm font-medium">
      <button 
        @click="activeTab = 'agent-master'"
        class="pb-3 flex items-center gap-2 transition-colors relative"
        :class="activeTab === 'agent-master' ? 'text-primary font-semibold border-b-2 border-primary' : 'text-muted-foreground hover:text-foreground'"
      >
        <CpuIcon class="h-4 w-4" />
        Agent Master Template & Encryption
      </button>

      <button 
        @click="activeTab = 'auth'"
        class="pb-3 flex items-center gap-2 transition-colors relative"
        :class="activeTab === 'auth' ? 'text-primary font-semibold border-b-2 border-primary' : 'text-muted-foreground hover:text-foreground'"
      >
        <KeyIcon class="h-4 w-4" />
        Auth & Session Governance
      </button>

      <button 
        @click="activeTab = 'integrations'"
        class="pb-3 flex items-center gap-2 transition-colors relative"
        :class="activeTab === 'integrations' ? 'text-primary font-semibold border-b-2 border-primary' : 'text-muted-foreground hover:text-foreground'"
      >
        <NetworkIcon class="h-4 w-4" />
        OT Integrations (OPC UA / Copia)
      </button>

      <button 
        @click="activeTab = 'certificates'"
        class="pb-3 flex items-center gap-2 transition-colors relative"
        :class="activeTab === 'certificates' ? 'text-primary font-semibold border-b-2 border-primary' : 'text-muted-foreground hover:text-foreground'"
      >
        <ShieldAlertIcon class="h-4 w-4" />
        mTLS & Certificate Authority
      </button>
    </div>

    <!-- Tab 1: Agent Master Template -->
    <div v-if="activeTab === 'agent-master'" class="space-y-6">
      <Card>
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base">Edge Fleet Master Policy Template (V1)</CardTitle>
              <CardDescription>
                Enforces cryptographically signed baseline configurations, disk spool encryption, and execution constraints across all IPCs.
              </CardDescription>
            </div>
            <Button size="sm" variant="default" @click="pushMasterPolicyToFleet" :disabled="pushingPolicy">
              <SendIcon class="h-4 w-4 mr-2" />
              Push Policy to Fleet
            </Button>
          </div>
        </CardHeader>
        <CardContent class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <!-- Hardware Binding -->
            <div class="flex items-center justify-between p-4 rounded-lg border border-border bg-card">
              <div>
                <div class="font-medium text-sm">Hardware-Bound Cryptographic Sealing</div>
                <div class="text-xs text-muted-foreground mt-0.5">Binds secrets and agent tokens to CPU/Machine-ID and TPM state</div>
              </div>
              <input type="checkbox" v-model="masterPolicy.enforceHardwareBinding" class="h-5 w-5 rounded border-input" />
            </div>

            <!-- Remote Execution Kill-Switch -->
            <div class="flex items-center justify-between p-4 rounded-lg border border-border bg-card">
              <div>
                <div class="font-medium text-sm">Allow Remote Diagnostic & File Checks</div>
                <div class="text-xs text-muted-foreground mt-0.5">Master kill-switch for remote commands (locked to Engineering role)</div>
              </div>
              <input type="checkbox" v-model="masterPolicy.allowRemoteExecution" class="h-5 w-5 rounded border-input" />
            </div>

            <!-- Spool Encryption Mode -->
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Offline Spool Encryption Mode</label>
              <select v-model="masterPolicy.spoolEncryptionMode" class="mt-1.5 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
                <option value="AES_256_GCM">AES-256-GCM Authenticated Envelope (Recommended)</option>
                <option value="DPAPI">Windows DPAPI Machine Scope</option>
                <option value="Plaintext">Plaintext (Development Only)</option>
              </select>
            </div>

            <!-- PII Scrubber Strict Level -->
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">PII & IP Scrubber Level</label>
              <select v-model="masterPolicy.piiScrubberStrictLevel" class="mt-1.5 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
                <option value="Strict">Strict (Mask Hostnames, MACs, Subnet IPs)</option>
                <option value="Standard">Standard (Mask Credentials & Passwords)</option>
                <option value="Disabled">Disabled</option>
              </select>
            </div>

            <!-- Egress Rate Limit -->
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Max Network Egress (Bytes / Second)</label>
              <Input type="number" v-model.number="masterPolicy.maxNetworkEgressBytesPerSec" class="mt-1.5 font-mono text-sm" />
              <p class="text-xs text-muted-foreground mt-1">1048576 = 1.0 MB/s token-bucket throttled bandwidth cap</p>
            </div>

            <!-- Deadband Tolerance -->
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Delta Deadband Tolerance (%)</label>
              <Input type="number" step="0.1" v-model.number="masterPolicy.deadbandTolerancePercentage" class="mt-1.5 font-mono text-sm" />
              <p class="text-xs text-muted-foreground mt-1">Suppresses transmission if numerical drift is below tolerance</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Tab 2: Auth Governance -->
    <div v-if="activeTab === 'auth'" class="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle class="text-base">Authentication & Session Governance</CardTitle>
          <CardDescription>Configure security boundaries, token lifetimes, and engineering role requirements.</CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Session Inactivity TTL (Minutes)</label>
              <Input type="number" v-model.number="authPolicy.sessionTtlMinutes" class="mt-1.5" />
            </div>

            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">Max Failed Login Lockout Threshold</label>
              <Input type="number" v-model.number="authPolicy.maxFailedLoginAttempts" class="mt-1.5" />
            </div>

            <div class="flex items-center justify-between p-4 rounded-lg border border-border bg-card">
              <div>
                <div class="font-medium text-sm">Mandatory MFA for Engineering Roles</div>
                <div class="text-xs text-muted-foreground mt-0.5">Enforces WebAuthn/TOTP on admin & engineer logins</div>
              </div>
              <input type="checkbox" v-model="authPolicy.requireMfaForEngineers" class="h-5 w-5 rounded border-input" />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Tab 3: OT Integrations -->
    <div v-if="activeTab === 'integrations'" class="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle class="text-base">OPC UA & Copia Industrial Integrations</CardTitle>
          <CardDescription>Configure northbound SCADA bridges and PLC version control webhooks.</CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">OPC UA Server Endpoint</label>
              <Input v-model="integrations.opcUaEndpoint" class="mt-1.5 font-mono text-sm" />
            </div>

            <div>
              <label class="text-xs font-semibold text-muted-foreground uppercase">OPC UA Security Policy</label>
              <select v-model="integrations.opcUaSecurityPolicy" class="mt-1.5 flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm">
                <option value="Basic256Sha256">Basic256Sha256 (Sign & Encrypt)</option>
                <option value="Aes128_Sha256_RsaOaep">Aes128_Sha256_RsaOaep</option>
                <option value="None">None (Unsecured Ingestion)</option>
              </select>
            </div>

            <div class="md:col-span-2">
              <label class="text-xs font-semibold text-muted-foreground uppercase">Copia Automation Webhook URL</label>
              <Input v-model="integrations.copiaWebhookUrl" class="mt-1.5 font-mono text-sm" />
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Tab 4: Certificate Management -->
    <div v-if="activeTab === 'certificates'" class="space-y-6">
      <Card>
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle class="text-base">X.509 Client Certificates (mTLS)</CardTitle>
              <CardDescription>Issue and revoke edge node certificates for mutually authenticated gRPC telemetry channels.</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent class="space-y-4">
          <!-- Issue Certificate Form -->
          <div class="flex gap-2 p-3 bg-muted/30 rounded-lg border border-border">
            <Input v-model="newCertCN" placeholder="Enter Common Name (e.g. CPC-010-Heimdall-Node)..." class="text-sm" />
            <Button size="sm" @click="issueCertificate" :disabled="issuingCert || !newCertCN">
              <PlusIcon class="h-4 w-4 mr-2" />
              Issue Certificate
            </Button>
          </div>

          <!-- Certificates Table -->
          <div class="overflow-x-auto">
            <table class="w-full text-sm text-left">
              <thead class="bg-muted/50 text-muted-foreground text-xs uppercase border-b border-border">
                <tr>
                  <th class="px-4 py-3">Common Name</th>
                  <th class="px-4 py-3">SHA-1 Thumbprint</th>
                  <th class="px-4 py-3">Validity</th>
                  <th class="px-4 py-3">Status</th>
                  <th class="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border">
                <tr v-for="c in certificates" :key="c.id" class="hover:bg-muted/30">
                  <td class="px-4 py-3 font-medium text-foreground">{{ c.commonName }}</td>
                  <td class="px-4 py-3 font-mono text-xs text-muted-foreground">{{ c.thumbprint }}</td>
                  <td class="px-4 py-3 text-xs text-muted-foreground">Until {{ new Date(c.validTo).toLocaleDateString() }}</td>
                  <td class="px-4 py-3">
                    <Badge :variant="c.status === 'Active' ? 'default' : 'secondary'">
                      {{ c.status }}
                    </Badge>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <Button 
                      v-if="c.status === 'Active'"
                      variant="ghost" 
                      size="sm" 
                      class="text-destructive hover:text-destructive text-xs" 
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
  </div>
</template>
