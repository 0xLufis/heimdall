<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { 
  NetworkIcon, 
  FolderTreeIcon, 
  CpuIcon, 
  TagIcon, 
  EyeIcon, 
  CheckCircle2Icon, 
  AlertCircleIcon, 
  SparklesIcon,
  DownloadIcon,
  PlusIcon,
  Trash2Icon,
  LayersIcon
} from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  ous: any[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'imported', result: any): void
}>()

interface TagRuleEntry {
  id: string
  keyTemplate: string
  valueTemplate: string
}

const selectedOuPaths = ref<string[]>([])
const namingPattern = ref('{NAME}')
const tagRules = ref<TagRuleEntry[]>([
  { id: '1', keyTemplate: 'location', valueTemplate: '{LOCATION}' },
  { id: '2', keyTemplate: 'purpose', valueTemplate: '{PURPOSE}' },
  { id: '3', keyTemplate: 'machine_type', valueTemplate: '{MACHINE_TYPE}' },
  { id: '4', keyTemplate: 'network.vlan', valueTemplate: 'VLAN-{VLAN_ID}' },
  { id: '5', keyTemplate: 'network.subnet', valueTemplate: '{SUBNET}' },
])

const availableTokens = [
  '{LOCATION}',
  '{PURPOSE}',
  '{MACHINE_TYPE}',
  '{VLAN_ID}',
  '{SUBNET}',
  '{OU[0]}',
  '{OU[1]}',
  '{HOSTNAME}',
  '{NAME}',
]

const previewLoading = ref(false)
const importing = ref(false)
const previewResults = ref<any[]>([])
const totalFound = ref(0)
const errorMessage = ref('')
const successMessage = ref('')
const copiedToken = ref('')

onMounted(() => {
  if (props.ous && props.ous.length > 0) {
    selectedOuPaths.value = props.ous.map(o => o.ouPath)
    generatePreview()
  }
})

function toggleOu(path: string) {
  const idx = selectedOuPaths.value.indexOf(path)
  if (idx >= 0) {
    selectedOuPaths.value.splice(idx, 1)
  } else {
    selectedOuPaths.value.push(path)
  }
}

function selectAllOus() {
  selectedOuPaths.value = props.ous.map(o => o.ouPath)
}

function deselectAllOus() {
  selectedOuPaths.value = []
}

function addTagRule() {
  tagRules.value.push({
    id: `rule-${Date.now()}-${Math.random().toString(36).substring(2, 5)}`,
    keyTemplate: '',
    valueTemplate: '',
  })
}

function removeTagRule(id: string) {
  tagRules.value = tagRules.value.filter(r => r.id !== id)
}

function loadStandardPreset() {
  tagRules.value = [
    { id: '1', keyTemplate: 'location', valueTemplate: '{LOCATION}' },
    { id: '2', keyTemplate: 'purpose', valueTemplate: '{PURPOSE}' },
    { id: '3', keyTemplate: 'machine_type', valueTemplate: '{MACHINE_TYPE}' },
    { id: '4', keyTemplate: 'network.vlan', valueTemplate: 'VLAN-{VLAN_ID}' },
    { id: '5', keyTemplate: 'network.subnet', valueTemplate: '{SUBNET}' },
  ]
  generatePreview()
}

function loadHierarchicalPreset() {
  tagRules.value = [
    { id: '1', keyTemplate: 'factory.zone', valueTemplate: '{LOCATION}' },
    { id: '2', keyTemplate: 'workstation.role', valueTemplate: '{PURPOSE}' },
    { id: '3', keyTemplate: 'equipment.class', valueTemplate: '{MACHINE_TYPE}' },
    { id: '4', keyTemplate: 'network.vlan_id', valueTemplate: '{VLAN_ID}' },
    { id: '5', keyTemplate: 'network.subnet', valueTemplate: '{SUBNET}' },
    { id: '6', keyTemplate: 'device.host_fqdn', valueTemplate: '{HOSTNAME}.factory.corp' },
  ]
  generatePreview()
}

function copyToken(token: string) {
  if (typeof navigator !== 'undefined' && navigator.clipboard) {
    navigator.clipboard.writeText(token)
    copiedToken.value = token
    setTimeout(() => {
      if (copiedToken.value === token) copiedToken.value = ''
    }, 2000)
  }
}

async function generatePreview() {
  previewLoading.value = true
  errorMessage.value = ''
  try {
    const validRules = tagRules.value
      .filter(r => r.keyTemplate.trim().length > 0)
      .map(r => ({
        keyTemplate: r.keyTemplate.trim(),
        valueTemplate: r.valueTemplate.trim(),
      }))

    const payload = {
      selectedOuPaths: selectedOuPaths.value,
      namingPattern: namingPattern.value,
      tagRules: validRules,
      tagTemplates: Object.fromEntries(validRules.map(r => [r.keyTemplate, r.valueTemplate])),
    }

    let res: any = null
    try {
      res = await $fetch('/api/proxy/v1/activedirectory/preview-import', {
        method: 'POST',
        body: payload,
      })
    } catch {
      res = await $fetch('/api/activedirectory/preview-import', {
        method: 'POST',
        body: payload,
      })
    }

    if (res) {
      previewResults.value = res.preview || []
      totalFound.value = res.totalFound || previewResults.value.length
    }
  } catch (err: any) {
    errorMessage.value = err?.data?.message || err?.message || 'Failed to evaluate ingestion preview.'
  } finally {
    previewLoading.value = false
  }
}

async function executeImport() {
  if (previewResults.value.length === 0) {
    errorMessage.value = 'No hosts available to import.'
    return
  }

  importing.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    let res: any = null
    try {
      res = await $fetch('/api/proxy/v1/activedirectory/import-hosts', {
        method: 'POST',
        body: { hosts: previewResults.value },
      })
    } catch {
      res = await $fetch('/api/activedirectory/import-hosts', {
        method: 'POST',
        body: { hosts: previewResults.value },
      })
    }

    successMessage.value = res?.message || `Successfully ingested ${previewResults.value.length} hosts into fleet!`
    emit('imported', res)
    setTimeout(() => {
      emit('close')
    }, 1500)
  } catch (err: any) {
    errorMessage.value = err?.data?.message || err?.message || 'Failed to ingest hosts into fleet.'
  } finally {
    importing.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="(val: boolean) => { if (!val) emit('close') }">
    <DialogContent class="max-w-4xl max-h-[92vh] overflow-y-auto bg-slate-900 border-slate-800 text-slate-100">
      <DialogHeader>
        <DialogTitle class="flex items-center gap-2 text-lg text-cyan-400">
          <NetworkIcon class="h-5 w-5" />
          Enterprise Host Ingestion & Metadata Mapping Engine
        </DialogTitle>
        <DialogDescription class="text-xs text-slate-400">
          Discover factory floor IPCs, PLCs, and edge nodes partitioned across Active Directory Organizational Units, isolate by VLAN, and map dynamic metadata tags.
        </DialogDescription>
      </DialogHeader>

      <div class="space-y-5 py-2">
        <!-- Messages -->
        <div v-if="errorMessage" class="p-3 rounded-lg bg-rose-500/15 border border-rose-500/30 text-rose-300 text-xs flex items-center gap-2">
          <AlertCircleIcon class="h-4 w-4 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>
        <div v-if="successMessage" class="p-3 rounded-lg bg-emerald-500/15 border border-emerald-500/30 text-emerald-300 text-xs flex items-center gap-2">
          <CheckCircle2Icon class="h-4 w-4 shrink-0" />
          <span>{{ successMessage }}</span>
        </div>

        <!-- Step 1: OU Selection with VLAN Badges -->
        <div class="rounded-xl border border-slate-800 bg-slate-950/60 p-4 space-y-3">
          <div class="flex items-center justify-between">
            <h4 class="text-xs font-semibold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <FolderTreeIcon class="h-4 w-4 text-cyan-400" />
              1. Select Target Organizational Units (Network Segments)
            </h4>
            <div class="flex gap-2">
              <Button variant="ghost" size="sm" class="text-xs h-7 text-cyan-400 hover:text-cyan-300" @click="selectAllOus">
                Select All Segments
              </Button>
              <Button variant="ghost" size="sm" class="text-xs h-7 text-slate-400 hover:text-slate-200" @click="deselectAllOus">
                Deselect All
              </Button>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-2.5">
            <div
              v-for="ou in props.ous"
              :key="ou.ouPath"
              @click="toggleOu(ou.ouPath)"
              class="flex items-center justify-between p-2.5 rounded-lg border cursor-pointer transition-all"
              :class="selectedOuPaths.includes(ou.ouPath) ? 'border-cyan-500/50 bg-cyan-950/20' : 'border-slate-800 bg-slate-900/40 opacity-60 hover:opacity-100'"
            >
              <div class="flex items-center gap-2 min-w-0">
                <input
                  type="checkbox"
                  :checked="selectedOuPaths.includes(ou.ouPath)"
                  @click.stop="toggleOu(ou.ouPath)"
                  class="h-4 w-4 rounded border-slate-700 text-cyan-600 focus:ring-cyan-500"
                />
                <div class="truncate">
                  <div class="text-xs font-medium text-slate-200 flex items-center gap-1.5">
                    {{ ou.name }}
                    <span class="text-[10px] text-slate-400">({{ ou.candidateHosts?.length || 0 }} hosts)</span>
                  </div>
                  <div class="text-[10px] font-mono text-slate-400 truncate">{{ ou.ouPath }}</div>
                </div>
              </div>

              <Badge
                variant="outline"
                class="shrink-0 text-[10px] border-cyan-500/40 text-cyan-400 font-mono"
              >
                VLAN {{ ou.vlanId }}
              </Badge>
            </div>
          </div>
        </div>

        <!-- Step 2: Templating Engine Configuration -->
        <div class="rounded-xl border border-slate-800 bg-slate-950/60 p-4 space-y-4">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <h4 class="text-xs font-semibold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <SparklesIcon class="h-4 w-4 text-cyan-400" />
              2. Configure Ingestion Metadata Schema & Token Templates
            </h4>
            <div class="flex items-center gap-2">
              <Button size="sm" variant="outline" class="h-7 text-xs border-cyan-500/40 text-cyan-400" @click="generatePreview" :disabled="previewLoading">
                <EyeIcon class="h-3.5 w-3.5 mr-1" />
                {{ previewLoading ? 'Evaluating...' : 'Evaluate Ingestion Preview' }}
              </Button>
            </div>
          </div>

          <!-- Dynamic Substitution Tokens Toolbar -->
          <div class="p-2.5 rounded-lg bg-slate-900 border border-slate-800 space-y-1.5">
            <div class="flex items-center justify-between">
              <span class="text-[11px] font-medium text-slate-400">Available Substitution Tokens (Click to Copy):</span>
              <span v-if="copiedToken" class="text-[10px] text-emerald-400 font-mono">Copied {{ copiedToken }}!</span>
            </div>
            <div class="flex flex-wrap gap-1.5">
              <button
                v-for="tok in availableTokens"
                :key="tok"
                type="button"
                @click="copyToken(tok)"
                class="px-2 py-0.5 rounded bg-slate-950 hover:bg-slate-800 border border-slate-700/60 text-[10px] font-mono text-cyan-300 transition-colors"
              >
                {{ tok }}
              </button>
            </div>
          </div>

          <!-- Device Hostname Template Input -->
          <div>
            <label class="text-[11px] font-medium text-slate-300">Device Hostname / Identity Template</label>
            <Input v-model="namingPattern" class="mt-1 h-8 bg-slate-950 border-slate-800 text-xs font-mono text-slate-200" />
            <div class="text-[10px] text-slate-500 mt-0.5">Example: <code>{NAME}</code> or <code>CPC-{VLAN_ID}-{HOSTNAME}</code></div>
          </div>

          <!-- Key and Value Dynamic Rules Table -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <label class="text-[11px] font-semibold text-slate-300 uppercase tracking-wider">
                Metadata Tag Mapping Rules (Key & Value Templating)
              </label>
              <div class="flex items-center gap-2">
                <button
                  type="button"
                  @click="loadStandardPreset"
                  class="text-[10px] text-cyan-400 hover:text-cyan-300 underline"
                >
                  Standard Preset
                </button>
                <span class="text-slate-600 text-[10px]">•</span>
                <button
                  type="button"
                  @click="loadHierarchicalPreset"
                  class="text-[10px] text-cyan-400 hover:text-cyan-300 underline"
                >
                  Hierarchical Dot-Notation Preset
                </button>
              </div>
            </div>

            <div class="rounded-lg border border-slate-800 overflow-hidden">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-900 text-slate-400 text-[11px] uppercase border-b border-slate-800">
                  <tr>
                    <th class="px-3 py-2 w-5/12">Tag Key Expression</th>
                    <th class="px-3 py-2 w-6/12">Tag Value Expression</th>
                    <th class="px-3 py-2 text-right w-1/12">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 bg-slate-950 font-sans">
                  <tr v-for="rule in tagRules" :key="rule.id" class="hover:bg-slate-900/40">
                    <td class="px-3 py-1.5">
                      <Input
                        v-model="rule.keyTemplate"
                        placeholder="e.g. factory.zone or zone.{LOCATION}"
                        class="h-7 bg-slate-900 border-slate-800 text-xs font-mono text-cyan-300"
                        @blur="generatePreview"
                      />
                    </td>
                    <td class="px-3 py-1.5">
                      <Input
                        v-model="rule.valueTemplate"
                        placeholder="e.g. {LOCATION} or VLAN-{VLAN_ID}"
                        class="h-7 bg-slate-900 border-slate-800 text-xs font-mono text-slate-200"
                        @blur="generatePreview"
                      />
                    </td>
                    <td class="px-3 py-1.5 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        class="h-7 w-7 p-0 text-rose-400 hover:text-rose-300 hover:bg-rose-950/30"
                        @click="removeTagRule(rule.id); generatePreview()"
                      >
                        <Trash2Icon class="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="flex items-center justify-between pt-1">
              <Button
                variant="outline"
                size="sm"
                class="h-7 text-xs border-slate-800 bg-slate-900 text-slate-300 hover:text-slate-100"
                @click="addTagRule"
              >
                <PlusIcon class="h-3 w-3 mr-1 text-cyan-400" />
                Add Metadata Tag Rule
              </Button>
              <span class="text-[10px] text-slate-500">
                Both Key and Value resolve substitution tokens dynamically.
              </span>
            </div>
          </div>
        </div>

        <!-- Step 3: Discovered Candidate Hosts Live Preview -->
        <div class="rounded-xl border border-slate-800 bg-slate-950/60 p-4 space-y-3">
          <div class="flex items-center justify-between">
            <h4 class="text-xs font-semibold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <CpuIcon class="h-4 w-4 text-cyan-400" />
              3. Discovered Candidate Hosts Verification ({{ previewResults.length }} Provisionable Hosts)
            </h4>
            <Badge variant="outline" class="border-slate-700 text-slate-300 text-xs">
              {{ selectedOuPaths.length }} Segments Selected
            </Badge>
          </div>

          <div class="overflow-x-auto rounded-lg border border-slate-800">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-900/80 text-slate-400 uppercase tracking-wider border-b border-slate-800">
                <tr>
                  <th class="px-3 py-2.5">Hostname & Name</th>
                  <th class="px-3 py-2.5">VLAN & Subnet</th>
                  <th class="px-3 py-2.5">IP & MAC</th>
                  <th class="px-3 py-2.5">Extracted Templated Tags</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60 font-sans">
                <tr v-for="host in previewResults" :key="host.hostname" class="hover:bg-slate-800/30">
                  <td class="px-3 py-2.5">
                    <div class="font-semibold text-slate-200">{{ host.hostname }}</div>
                    <div class="text-[11px] text-slate-400">{{ host.name }}</div>
                  </td>
                  <td class="px-3 py-2.5">
                    <div class="flex items-center gap-1.5">
                      <Badge class="bg-cyan-500/20 text-cyan-300 border-cyan-500/30 text-[10px]">
                        VLAN {{ host.vlanId }}
                      </Badge>
                      <span class="text-[11px] font-mono text-slate-400">{{ host.subnet }}</span>
                    </div>
                    <div class="text-[10px] text-slate-500 truncate max-w-[180px]">{{ host.vlanName }}</div>
                  </td>
                  <td class="px-3 py-2.5 font-mono text-[11px]">
                    <div class="text-slate-300">{{ host.ipAddress }}</div>
                    <div class="text-[10px] text-slate-500">{{ host.macAddress }}</div>
                  </td>
                  <td class="px-3 py-2.5">
                    <div class="flex flex-wrap gap-1">
                      <span
                        v-for="(val, key) in host.ouTags"
                        :key="key"
                        class="px-1.5 py-0.5 rounded bg-slate-800/80 border border-slate-700/50 text-[10px] text-slate-300"
                      >
                        <strong class="text-cyan-400">{{ key }}:</strong> {{ val }}
                      </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <DialogFooter class="flex items-center justify-between border-t border-slate-800 pt-3">
        <Button variant="ghost" size="sm" @click="emit('close')" class="text-slate-400 hover:text-slate-200">
          Dismiss
        </Button>
        <Button
          size="sm"
          class="bg-cyan-600 hover:bg-cyan-500 text-white font-medium shadow-lg shadow-cyan-950/40"
          :disabled="importing || previewResults.length === 0"
          @click="executeImport"
        >
          <DownloadIcon class="h-4 w-4 mr-1.5" />
          {{ importing ? 'Ingesting Hosts...' : `Execute Fleet Ingestion (${previewResults.length} Hosts)` }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
