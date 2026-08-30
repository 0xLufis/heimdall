<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue'
import {
  Tag,
  Network,
  DollarSign,
  Cpu,
  FileJson,
  Zap,
  Plus,
  Trash2,
  Copy,
  Check,
  RefreshCw,
  Sliders,
  Layers,
  Sparkles,
  Code,
  HardDrive,
  Monitor,
  CheckCircle2,
  AlertCircle,
  X,
  Bot,
  Eye,
  Activity,
  Pipette,
  Save,
  Download,
  Upload,
  ChevronRight,
  ShieldCheck,
  ExternalLink,
  Database
} from 'lucide-vue-next'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { Badge } from '~/components/ui/badge'
import SearchableSelect from '~/components/ui/searchable-select/SearchableSelect.vue'
import { useAssetReferenceCache } from '~/composables/useAssetReferenceCache'
import { useJsonTemplateEngine } from '~/composables/useJsonTemplateEngine'
import { generateRandomHex } from '~/utils/jsonTemplatingEngine'

const props = withDefaults(defineProps<{
  open: boolean
  mode?: 'create' | 'edit'
  initialType?: 'hardware' | 'software'
  item?: any | null
}>(), {
  mode: 'edit',
  initialType: 'hardware',
  item: null
})

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'close'): void
  (e: 'save', payload: any): void
}>()

// Active Editor Tab
type EditorTab = 'identity' | 'topology' | 'commercial' | 'specs' | 'templates'
const activeTab = ref<EditorTab>('identity')

// Asset Reference Cache (Single Source of Truth for Unique Reference Keys)
const {
  oems,
  importers,
  parentPcs,
  stations,
  components,
  technologies,
  modelNumbers,
  metadataKeys,
  metadataValuesByKey,
  responsibleTeams: cachedTeams,
  isLoading: isCacheLoading,
  fetchReferenceCache,
  registerAssetValues,
  getSuggestionsForKey
} = useAssetReferenceCache()

// SearchableSelect options mapped from Cache
const manufacturerOptions = computed(() => oems.value.map(m => ({ id: m.id, label: m.label || m.name || m.id })))
const supplierOptions = computed(() => importers.value.map(s => ({ id: s.id, label: s.label || s.name || s.id })))
const machineOptions = computed(() => stations.value.map(m => ({ id: m.id, label: m.customIdentifier || m.label || m.name })))
const pcOptions = computed(() => parentPcs.value.map(pc => ({ id: pc.id, label: pc.label || pc.hostname })))
const componentOptions = computed(() => components.value.map(c => ({ id: c.id, label: c.label || c.name })))
const technologyOptions = computed(() => technologies.value.map(t => ({ id: t.id, label: t.label })))
const modelNumberOptions = computed(() => modelNumbers.value.map(m => ({ id: m.id, label: m.label })))

// Combine cached metadata keys with baseline industrial spec keys
const baselineSpecKeys = [
  'Voltage', 'Power', 'Current', 'Resolution', 'FPS', 'Protocol',
  'IPAddress', 'Port', 'Firmware', 'PressureRange', 'CycleTime', 'Interface'
]

const availableSpecKeys = computed(() => {
  const keySet = new Set<string>()
  // Add all discovered unique keys from database cache
  for (const mk of metadataKeys.value) {
    if (mk.key) keySet.add(mk.key)
  }
  // Add baseline common keys
  for (const bk of baselineSpecKeys) {
    keySet.add(bk)
  }
  return Array.from(keySet)
})

// Template Engine Composable
const {
  builtInTemplates,
  customTemplates,
  filteredTemplates,
  selectedTemplateId,
  selectedCategory,
  activeTemplate,
  activeVariables,
  variableValues,
  rawJsonText,
  rawJsonError,
  evaluationResult,
  evaluatedJsonString,
  selectTemplate,
  setVariable,
  resetVariables,
  updateFromRawJson,
  saveAsCustomTemplate,
  deleteCustomTemplate,
  exportTemplates,
  importTemplates,
  mapTemplateToAssetForm
} = useJsonTemplateEngine()

// Engineering Teams list for selection
const defaultTeams = [
  { id: 'team-mech', name: 'Mechanical Maintenance', color: 'text-amber-400 border-amber-900/40 bg-amber-900/10' },
  { id: 'team-elec', name: 'Electrical Engineering', color: 'text-blue-400 border-blue-900/40 bg-blue-900/10' },
  { id: 'team-quality', name: 'Quality Automation', color: 'text-emerald-400 border-emerald-900/40 bg-emerald-900/10' },
  { id: 'team-it', name: 'Industrial IT', color: 'text-indigo-400 border-indigo-900/40 bg-indigo-900/10' }
]

const availableTeams = computed(() => {
  if (!cachedTeams.value || cachedTeams.value.length === 0) return defaultTeams
  const list: any[] = [...defaultTeams]
  for (const ct of cachedTeams.value) {
    if (!list.some(t => t.id === ct.id || t.name === ct.label)) {
      list.push({ id: ct.id, name: ct.label, color: 'text-indigo-400 border-indigo-900/40 bg-indigo-900/10' })
    }
  }
  return list
})

// Primary Asset Form State
const form = ref({
  id: '',
  name: '',
  displayName: '',
  technology: '',
  serialNumber: '',
  modelNumber: '',
  costInHUF: 0,
  quantity: 1,
  itemType: 'HardwareComponent',
  manufacturerId: null as string | null,
  supplierId: null as string | null,
  machineId: null as string | null,
  clientPcId: null as string | null,
  parentId: null as string | null,
  lateralLinkId: null as string | null,
  purchaseDate: '',
  responsibleTeamIds: [] as string[]
})

// Custom Metadata parameters
const customData = ref<Record<string, any>>({})
const newFieldKey = ref('')
const newFieldValue = ref('')

// Template application toast indicator
const templateAppliedBanner = ref(false)
const copyJsonFeedback = ref(false)

// Custom template creation state
const showSaveTemplateModal = ref(false)
const newTemplateName = ref('')
const newTemplateCategory = ref<string>('General')
const newTemplateDesc = ref('')

// Populate form from item prop or initialType
watch(() => props.item, (item) => {
  if (item) {
    form.value = {
      id: item.id || '',
      name: item.name || '',
      displayName: item.displayName || '',
      technology: item.technology || item.metadata?.Technology || '',
      serialNumber: item.serialNumber || item.data?.SerialNumber || item.metadata?.SerialNumber || '',
      modelNumber: item.modelNumber || item.data?.ModelNumber || item.metadata?.ModelNumber || '',
      costInHUF: item.costInHUF || item.cost || item.data?.CostInHUF || item.metadata?.CostInHUF || 0,
      quantity: item.quantity || 1,
      itemType: item.itemType || (props.initialType === 'software' ? 'SoftwareComponent' : 'HardwareComponent'),
      manufacturerId: item.manufacturerId || item.manufacturer?.id || (typeof item.manufacturer === 'string' ? item.manufacturer : null),
      supplierId: item.supplierId || item.supplier?.id || (typeof item.supplier === 'string' ? item.supplier : null),
      machineId: item.machineId || null,
      clientPcId: item.clientPcId || null,
      parentId: item.parentId || null,
      lateralLinkId: item.lateralLinkId || null,
      purchaseDate: item.purchaseDate ? item.purchaseDate.split('T')[0] : '',
      responsibleTeamIds: Array.isArray(item.responsibleTeams) 
        ? item.responsibleTeams.map((t: any) => typeof t === 'string' ? t : (t.id || t.name))
        : []
    }

    const rawMeta = { ...(item.metadata || item.data || {}) }
    delete rawMeta.SerialNumber
    delete rawMeta.CostInHUF
    delete rawMeta.ModelNumber
    delete rawMeta.Technology
    customData.value = rawMeta
  } else {
    // Reset for new item
    form.value = {
      id: '',
      name: '',
      displayName: '',
      technology: '',
      serialNumber: '',
      modelNumber: '',
      costInHUF: 0,
      quantity: 1,
      itemType: props.initialType === 'software' ? 'SoftwareComponent' : 'HardwareComponent',
      manufacturerId: null,
      supplierId: null,
      machineId: null,
      clientPcId: null,
      parentId: null,
      lateralLinkId: null,
      purchaseDate: new Date().toISOString().split('T')[0],
      responsibleTeamIds: []
    }
    customData.value = {}
  }
}, { immediate: true })

onMounted(() => {
  if (props.open) {
    fetchReferenceCache()
  }
})

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    fetchReferenceCache()
  }
})

// Auto-generate serial helper
const generateSerial = () => {
  const prefix = form.value.itemType === 'SoftwareComponent' ? 'LIC' : 'SN'
  form.value.serialNumber = `${prefix}-${generateRandomHex(8)}`
}

// Toggle attribute in custom parameter schema
const addSuggestedField = (key: string) => {
  if (customData.value[key] === undefined) {
    // If cache has a preferred sample value, use empty or default
    customData.value[key] = ''
  } else {
    delete customData.value[key]
  }
}

const addCustomField = () => {
  if (newFieldKey.value.trim()) {
    const key = newFieldKey.value.trim()
    customData.value[key] = newFieldValue.value
    newFieldKey.value = ''
    newFieldValue.value = ''
  }
}

const removeCustomField = (key: string) => {
  delete customData.value[key]
}

// Toggle team responsibility
const toggleTeam = (teamId: string) => {
  if (form.value.responsibleTeamIds.includes(teamId)) {
    form.value.responsibleTeamIds = form.value.responsibleTeamIds.filter(id => id !== teamId)
  } else {
    form.value.responsibleTeamIds.push(teamId)
  }
}

// Apply Template to Form state
const handleApplyTemplate = () => {
  if (!evaluationResult.value.data) return

  const mapped = mapTemplateToAssetForm(evaluationResult.value.data)
  
  if (mapped.name) form.value.name = mapped.name
  if (mapped.displayName) form.value.displayName = mapped.displayName
  if (mapped.technology) form.value.technology = mapped.technology
  if (mapped.serialNumber) form.value.serialNumber = mapped.serialNumber
  if (mapped.modelNumber) form.value.modelNumber = mapped.modelNumber
  if (mapped.costInHUF) form.value.costInHUF = mapped.costInHUF
  if (mapped.itemType) form.value.itemType = mapped.itemType
  if (mapped.quantity) form.value.quantity = mapped.quantity
  
  // Merge custom metadata
  customData.value = {
    ...customData.value,
    ...mapped.metadata
  }

  // Visual feedback
  templateAppliedBanner.value = true
  setTimeout(() => {
    templateAppliedBanner.value = false
  }, 4000)
}

// Copy JSON to clipboard
const copyEvaluatedJson = async () => {
  try {
    await navigator.clipboard.writeText(evaluatedJsonString.value)
    copyJsonFeedback.value = true
    setTimeout(() => {
      copyJsonFeedback.value = false
    }, 2000)
  } catch {
    // Ignore clipboard error
  }
}

// Save Current Asset as Template
const handleSaveAsCustomTemplate = () => {
  if (!newTemplateName.value.trim()) return

  const templatePayload = {
    name: form.value.name,
    displayName: form.value.displayName,
    technology: form.value.technology,
    modelNumber: form.value.modelNumber,
    costInHUF: form.value.costInHUF,
    itemType: form.value.itemType,
    metadata: { ...customData.value }
  }

  saveAsCustomTemplate({
    name: newTemplateName.value.trim(),
    category: newTemplateCategory.value as any,
    description: newTemplateDesc.value.trim() || `Custom template based on ${form.value.name || 'Asset'}`,
    targetType: form.value.itemType as any,
    template: templatePayload
  })

  showSaveTemplateModal.value = false
  newTemplateName.value = ''
  newTemplateDesc.value = ''
}

// Save Full Form
const handleSave = () => {
  const payload = {
    id: form.value.id || undefined,
    name: form.value.name,
    displayName: form.value.displayName,
    technology: form.value.technology,
    serialNumber: form.value.serialNumber,
    modelNumber: form.value.modelNumber,
    costInHUF: form.value.costInHUF,
    quantity: form.value.quantity,
    itemType: form.value.itemType,
    manufacturerId: form.value.manufacturerId || null,
    supplierId: form.value.supplierId || null,
    machineId: form.value.machineId || null,
    clientPcId: form.value.clientPcId || null,
    parentId: (form.value.parentId === 'none' || !form.value.parentId) ? null : form.value.parentId,
    lateralLinkId: form.value.lateralLinkId || null,
    purchaseDate: form.value.purchaseDate || undefined,
    responsibleTeamIds: form.value.responsibleTeamIds,
    metadata: {
      ...customData.value,
      SerialNumber: form.value.serialNumber,
      CostInHUF: form.value.costInHUF,
      ModelNumber: form.value.modelNumber,
      Technology: form.value.technology
    },
    data: {
      ...customData.value,
      SerialNumber: form.value.serialNumber,
      CostInHUF: form.value.costInHUF
    }
  }

  // Ingest any newly created / typed OEM, Importer, Technology, or Metadata into global cache
  registerAssetValues(payload)

  emit('save', payload)
  emit('update:open', false)
  emit('close')
}

// Format HUF Currency preview
const formatHuf = (val: number) => {
  if (!val && val !== 0) return '0'
  return new Intl.NumberFormat('hu-HU').format(val)
}

// Category tabs for template browser
const templateCategories = [
  { id: 'all', label: 'All Templates' },
  { id: 'controller', label: 'Controllers & IPCs' },
  { id: 'vision', label: 'Vision Systems' },
  { id: 'motion', label: 'Motion & Servos' },
  { id: 'sensor', label: 'Sensors & I/O' },
  { id: 'software', label: 'Software Licenses' },
  { id: 'network', label: 'Fieldbus & Network' },
  { id: 'dispensing', label: 'Dispensing' },
  { id: 'custom', label: 'Custom Presets' }
]
</script>

<template>
  <Dialog :open="open" @update:open="(val) => emit('update:open', val)">
    <DialogContent class="max-w-4xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-[2rem] shadow-2xl flex flex-col max-h-[92vh]">
      
      <!-- Top Modal Header -->
      <DialogHeader class="bg-indigo-950/40 p-6 sm:p-7 border-b border-slate-900 shrink-0">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3.5">
            <div class="p-3 bg-indigo-500/20 rounded-2xl text-indigo-400 border border-indigo-500/30 shadow-inner">
              <Cpu v-if="form.itemType === 'HardwareComponent'" class="h-6 w-6" />
              <FileJson v-else-if="form.itemType === 'SoftwareComponent'" class="h-6 w-6" />
              <Layers v-else class="h-6 w-6" />
            </div>
            <div>
              <DialogTitle class="text-xl sm:text-2xl font-black uppercase tracking-tight text-slate-100 flex items-center gap-3">
                <span>{{ mode === 'create' ? 'Provision Asset Node' : 'Edit Asset Record' }}</span>
                <Badge variant="outline" class="text-[8px] font-mono font-black uppercase tracking-widest bg-indigo-500/10 text-indigo-400 border-indigo-500/30 px-3.5 py-1.5 rounded-full inline-flex items-center justify-center shrink-0 whitespace-nowrap leading-none shadow-sm">
                  {{ form.itemType }}
                </Badge>
              </DialogTitle>
              <DialogDescription class="text-indigo-400/70 text-xs font-bold uppercase tracking-widest mt-1">
                Industrial OT/IT graph infrastructure, parameter specifications & unique key cache
              </DialogDescription>
            </div>
          </div>
        </div>

        <!-- Template Applied Alert Toast -->
        <div v-if="templateAppliedBanner" class="mt-4 p-3 bg-emerald-950/60 border border-emerald-500/30 rounded-xl flex items-center justify-between animate-in fade-in slide-in-from-top-2">
          <div class="flex items-center gap-2 text-emerald-400 text-xs font-bold">
            <CheckCircle2 class="size-4 shrink-0" />
            <span>Template parameters and metadata successfully mapped to asset!</span>
          </div>
          <Button variant="ghost" size="sm" @click="activeTab = 'identity'" class="h-7 text-[10px] font-black uppercase text-emerald-300 hover:bg-emerald-900/50">
            View Details
          </Button>
        </div>
      </DialogHeader>

      <!-- Navigation Tabs Bar -->
      <div class="bg-slate-900/90 border-b border-slate-800/80 px-6 pt-3 flex items-center justify-between shrink-0 overflow-x-auto custom-scrollbar gap-2">
        <div class="flex items-center gap-1.5 min-w-max pb-3">
          <button
            @click="activeTab = 'identity'"
            :class="activeTab === 'identity' ? 'bg-indigo-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'"
            class="px-3.5 py-2 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all flex items-center gap-2 border border-transparent shrink-0 whitespace-nowrap leading-none"
          >
            <Tag class="size-3.5 shrink-0" />
            <span>Identity & Core</span>
          </button>

          <button
            @click="activeTab = 'topology'"
            :class="activeTab === 'topology' ? 'bg-indigo-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'"
            class="px-3.5 py-2 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all flex items-center gap-2 border border-transparent shrink-0 whitespace-nowrap leading-none"
          >
            <Network class="size-3.5 shrink-0" />
            <span>Topology & Graph</span>
          </button>

          <button
            @click="activeTab = 'commercial'"
            :class="activeTab === 'commercial' ? 'bg-indigo-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'"
            class="px-3.5 py-2 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all flex items-center gap-2 border border-transparent shrink-0 whitespace-nowrap leading-none"
          >
            <DollarSign class="size-3.5 shrink-0" />
            <span>Commercial</span>
          </button>

          <button
            @click="activeTab = 'specs'"
            :class="activeTab === 'specs' ? 'bg-indigo-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'"
            class="px-3.5 py-2 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all flex items-center gap-2 border border-transparent shrink-0 whitespace-nowrap leading-none"
          >
            <Sliders class="size-3.5 shrink-0" />
            <span>Specs & Params</span>
            <span v-if="Object.keys(customData).length > 0" class="h-5 px-2 text-[7.5px] bg-slate-800 text-slate-300 rounded-full inline-flex items-center justify-center leading-none font-black ml-1">
              {{ Object.keys(customData).length }}
            </span>
          </button>

          <button
            @click="activeTab = 'templates'"
            :class="activeTab === 'templates' ? 'bg-indigo-600 text-white shadow-md' : 'text-indigo-400 hover:text-indigo-200 hover:bg-indigo-950/40 bg-indigo-950/20 border-indigo-800/30'"
            class="px-3.5 py-2 rounded-xl text-[11px] font-black uppercase tracking-wider transition-all flex items-center gap-2 border shrink-0 whitespace-nowrap leading-none"
          >
            <Sparkles class="size-3.5 text-indigo-300 shrink-0" />
            <span>JSON & Templates</span>
          </button>
        </div>

        <div class="hidden sm:flex items-center gap-2 pb-3 shrink-0">
          <span class="text-[9px] font-mono font-bold text-slate-400 border border-slate-800 bg-slate-900/80 px-3.5 py-1.5 rounded-full inline-flex items-center justify-center leading-none tracking-wider shadow-sm">
            Valuation: {{ formatHuf(form.costInHUF) }} HUF
          </span>
        </div>
      </div>

      <!-- Tab Content Area (Scrollable) -->
      <div class="p-6 sm:p-8 overflow-y-auto custom-scrollbar flex-1 space-y-6">
        
        <!-- TAB 1: IDENTITY & CORE -->
        <div v-show="activeTab === 'identity'" class="space-y-6 animate-in fade-in duration-200">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            
            <!-- Asset Class / Type -->
            <div class="space-y-2 col-span-1 sm:col-span-2">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Asset Classification</Label>
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <Button
                  type="button"
                  variant="outline"
                  @click="form.itemType = 'HardwareComponent'"
                  :class="form.itemType === 'HardwareComponent' ? 'bg-indigo-600 text-white border-indigo-500 shadow-md' : 'bg-slate-900 border-slate-800 text-slate-400 hover:text-slate-200'"
                  class="rounded-xl h-11 text-xs font-black uppercase tracking-wider flex items-center justify-center gap-2 px-3 text-center"
                >
                  <Cpu class="size-4 shrink-0" />
                  <span class="truncate">Hardware Node</span>
                </Button>

                <Button
                  type="button"
                  variant="outline"
                  @click="form.itemType = 'SoftwareComponent'"
                  :class="form.itemType === 'SoftwareComponent' ? 'bg-indigo-600 text-white border-indigo-500 shadow-md' : 'bg-slate-900 border-slate-800 text-slate-400 hover:text-slate-200'"
                  class="rounded-xl h-11 text-xs font-black uppercase tracking-wider flex items-center justify-center gap-2 px-3 text-center"
                >
                  <FileJson class="size-4 shrink-0" />
                  <span class="truncate">Software / License</span>
                </Button>

                <Button
                  type="button"
                  variant="outline"
                  @click="form.itemType = 'Machine'"
                  :class="form.itemType === 'Machine' ? 'bg-indigo-600 text-white border-indigo-500 shadow-md' : 'bg-slate-900 border-slate-800 text-slate-400 hover:text-slate-200'"
                  class="rounded-xl h-11 text-xs font-black uppercase tracking-wider flex items-center justify-center gap-2 px-3 text-center"
                >
                  <Layers class="size-4 shrink-0" />
                  <span class="truncate">Process Station</span>
                </Button>
              </div>
            </div>

            <!-- Internal Handle / Name -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Internal Identifier / Handle</Label>
                <span class="text-[9px] text-indigo-400 font-mono">Unique Tag</span>
              </div>
              <Input
                v-model="form.name"
                placeholder="e.g. S7-1500-ROOT or MTR-OP10-01"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-bold focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <!-- Display Name -->
            <div class="space-y-2">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">User Display Name</Label>
              <Input
                v-model="form.displayName"
                placeholder="e.g. Main CNC Spindle Motor"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-bold focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <!-- Technology Stack (Select or Type with Cached Search Source) -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Technology Stack</Label>
                <span class="text-[9px] text-slate-500">Cached source</span>
              </div>
              <SearchableSelect
                v-model="form.technology"
                :options="technologyOptions"
                placeholder="Select or Type Technology Stack"
              />
            </div>

            <!-- Quantity -->
            <div class="space-y-2">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Quantity / Unit Count</Label>
              <Input
                v-model.number="form.quantity"
                type="number"
                min="1"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-bold font-mono"
              />
            </div>

            <!-- Serial Number with Generator Helper -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Serial Number</Label>
                <button
                  type="button"
                  @click="generateSerial"
                  class="text-[9px] font-mono text-indigo-400 hover:text-indigo-300 flex items-center gap-1 uppercase"
                >
                  <RefreshCw class="size-3" /> Auto-Gen SN
                </button>
              </div>
              <Input
                v-model="form.serialNumber"
                placeholder="e.g. SN-SPINDLE-994"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-mono text-xs"
              />
            </div>

            <!-- Model / Part Number (Select or Type with Cached Search Source) -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Model / Part Number</Label>
                <span class="text-[9px] text-slate-500">Cached catalog</span>
              </div>
              <SearchableSelect
                v-model="form.modelNumber"
                :options="modelNumberOptions"
                placeholder="Select or Type Model / Part Number"
              />
            </div>
          </div>
        </div>

        <!-- TAB 2: TOPOLOGY & GRAPH LINKS -->
        <div v-show="activeTab === 'topology'" class="space-y-6 animate-in fade-in duration-200">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            
            <!-- Linked Host PC / IPC (Search source: parentPcs cache) -->
            <div class="space-y-2">
              <div class="flex items-center gap-1.5">
                <Monitor class="size-3.5 text-blue-400 shrink-0" />
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Reporting Host IPC / PC</Label>
              </div>
              <SearchableSelect
                v-model="form.clientPcId"
                :options="pcOptions"
                placeholder="Select or Search Reporting Host PC"
              />
              <p class="text-[9px] text-slate-500 leading-tight">Link this component to a reporting Edge industrial PC.</p>
            </div>

            <!-- Linked Machine / Station (Search source: stations cache) -->
            <div class="space-y-2">
              <div class="flex items-center gap-1.5">
                <Layers class="size-3.5 text-indigo-400 shrink-0" />
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Production Station / Cell</Label>
              </div>
              <SearchableSelect
                v-model="form.machineId"
                :options="machineOptions"
                placeholder="Select or Type Production Station"
              />
              <p class="text-[9px] text-slate-500 leading-tight">Assign node to a station process envelope.</p>
            </div>

            <!-- Parent Asset Link (Hierarchical Sub-assembly) (Search source: components cache) -->
            <div class="space-y-2">
              <div class="flex items-center gap-1.5">
                <ChevronRight class="size-3.5 text-emerald-400 shrink-0" />
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Parent Assembly / Enclosure</Label>
              </div>
              <SearchableSelect
                v-model="form.parentId"
                :options="componentOptions"
                placeholder="Select Parent Assembly (Optional)"
              />
              <p class="text-[9px] text-slate-500 leading-tight">For recursive hierarchy (e.g. servo drive inside electrical cabinet).</p>
            </div>

            <!-- Lateral Dependency Link (Search source: components cache) -->
            <div class="space-y-2">
              <div class="flex items-center gap-1.5">
                <Network class="size-3.5 text-amber-400 shrink-0" />
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Lateral Link / Dependency</Label>
              </div>
              <SearchableSelect
                v-model="form.lateralLinkId"
                :options="componentOptions"
                placeholder="Select Dependent Fieldbus Peer"
              />
              <p class="text-[9px] text-slate-500 leading-tight">Interconnect link for OT fieldbus peers.</p>
            </div>
          </div>

          <!-- Responsible Engineering Teams (Search source: responsibleTeams cache) -->
          <div class="space-y-3 pt-4 border-t border-slate-900">
            <div class="flex items-center justify-between">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Responsible Engineering Teams</Label>
              <span class="text-[9px] text-slate-500">Multi-select team ownership</span>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
              <div
                v-for="team in availableTeams"
                :key="team.id"
                @click="toggleTeam(team.id)"
                :class="form.responsibleTeamIds.includes(team.id) ? 'border-indigo-500 bg-indigo-950/30 text-indigo-300' : 'border-slate-800 bg-slate-900/60 text-slate-400 hover:border-slate-700'"
                class="p-3 rounded-xl border cursor-pointer transition-all flex items-center justify-between gap-2"
              >
                <span class="text-[11px] font-bold uppercase truncate">{{ team.name }}</span>
                <div
                  class="size-4 rounded border flex items-center justify-center shrink-0"
                  :class="form.responsibleTeamIds.includes(team.id) ? 'bg-indigo-600 border-indigo-600 text-white' : 'border-slate-700 bg-slate-950'"
                >
                  <Check v-if="form.responsibleTeamIds.includes(team.id)" class="size-3" />
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 3: COMMERCIAL & FINANCIALS -->
        <div v-show="activeTab === 'commercial'" class="space-y-6 animate-in fade-in duration-200">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-5">
            
            <!-- OEM / Manufacturer (Search source: oems cache) -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">OEM / Manufacturer</Label>
                <span class="text-[9px] text-slate-500">Cached OEMs</span>
              </div>
              <SearchableSelect
                v-model="form.manufacturerId"
                :options="manufacturerOptions"
                placeholder="Select or Type OEM Manufacturer"
              />
            </div>

            <!-- Importer / Vendor / Supplier (Search source: importers cache) -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Importer / Vendor / Supplier</Label>
                <span class="text-[9px] text-slate-500">Cached Vendors</span>
              </div>
              <SearchableSelect
                v-model="form.supplierId"
                :options="supplierOptions"
                placeholder="Select or Type Importer / Vendor"
              />
            </div>

            <!-- Capital Valuation (HUF) -->
            <div class="space-y-2">
              <div class="flex items-center justify-between">
                <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Capital Valuation (HUF)</Label>
                <span class="text-xs font-mono font-black text-emerald-400">{{ formatHuf(form.costInHUF) }} HUF</span>
              </div>
              <Input
                v-model.number="form.costInHUF"
                type="number"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-mono font-bold"
              />
            </div>

            <!-- Purchase / Commissioning Date -->
            <div class="space-y-2">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Commissioning / Purchase Date</Label>
              <Input
                v-model="form.purchaseDate"
                type="date"
                class="rounded-xl h-11 bg-slate-900 border-slate-800 text-slate-100 font-mono text-xs"
              />
            </div>
          </div>
        </div>

        <!-- TAB 4: SPECS & PARAMETERS -->
        <div v-show="activeTab === 'specs'" class="space-y-6 animate-in fade-in duration-200">
          
          <!-- Quick Add Suggestion Chips (Populated from Cached Metadata Keys) -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Select or Type Industrial Parameters</Label>
              <span class="text-[9px] text-slate-500">Click pill to toggle attribute</span>
            </div>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="key in availableSpecKeys"
                :key="key"
                type="button"
                @click="addSuggestedField(key)"
                :class="customData[key] !== undefined ? 'bg-indigo-600/30 text-indigo-300 border-indigo-500/50 ring-1 ring-indigo-500/40 shadow-sm' : 'bg-slate-900 hover:bg-indigo-950/40 border-slate-800 hover:border-indigo-500/40 text-slate-400 hover:text-indigo-300'"
                class="px-3.5 py-1.5 rounded-full border text-[9px] font-mono font-bold uppercase tracking-wider transition-all inline-flex items-center justify-center gap-1.5 shrink-0 whitespace-nowrap leading-none shadow-sm cursor-pointer"
              >
                <Check v-if="customData[key] !== undefined" class="size-2.5 text-indigo-400 shrink-0" />
                <Plus v-else class="size-2.5 shrink-0" />
                <span class="whitespace-nowrap">{{ key }}</span>
              </button>
            </div>
          </div>

          <!-- Parameter Table / List with Value Autocomplete Suggestions -->
          <div class="space-y-3 pt-2">
            <div class="flex items-center justify-between">
              <Label class="text-[10px] font-black text-slate-500 uppercase tracking-widest">Custom Metadata Schema Attributes ({{ Object.keys(customData).length }})</Label>
            </div>

            <div v-if="Object.keys(customData).length === 0" class="p-8 text-center bg-slate-900/30 rounded-2xl border border-dashed border-slate-800">
              <Sliders class="size-6 text-slate-700 mx-auto mb-2" />
              <p class="text-xs font-bold text-slate-500 uppercase tracking-widest">No custom parameters configured</p>
              <p class="text-[10px] text-slate-600 mt-1">Use the quick chips above or add custom key/value parameters below.</p>
            </div>

            <div v-else class="space-y-2.5">
              <div
                v-for="(val, key) in customData"
                :key="key"
                class="p-2.5 bg-slate-900/50 rounded-xl border border-slate-800/80 hover:border-slate-700 transition-colors space-y-2"
              >
                <div class="flex items-center gap-3">
                  <span class="h-9 px-3.5 bg-slate-950 border border-slate-800 text-indigo-400 font-mono text-[9px] font-black uppercase tracking-wider shrink-0 max-w-[180px] inline-flex items-center justify-center rounded-full whitespace-nowrap leading-none shadow-sm">
                    <span class="truncate">{{ key }}</span>
                  </span>
                  <Input
                    v-model="customData[key]"
                    placeholder="Parameter value..."
                    class="flex-1 rounded-xl h-8 bg-slate-950 border-slate-800 text-slate-200 text-xs font-mono"
                  />
                  <Button
                    variant="ghost"
                    size="icon"
                    class="h-8 w-8 text-slate-600 hover:text-rose-400 shrink-0"
                    @click="removeCustomField(key)"
                  >
                    <Trash2 class="w-3.5 h-3.5" />
                  </Button>
                </div>

                <!-- Cached Value Suggestions for this specific Key -->
                <div v-if="getSuggestionsForKey(key).length > 0" class="flex flex-wrap gap-1.5 pl-2 items-center">
                  <span class="text-[8px] font-mono text-slate-600 uppercase">Cached:</span>
                  <button
                    v-for="sVal in getSuggestionsForKey(key).slice(0, 6)"
                    :key="sVal"
                    type="button"
                    @click="customData[key] = sVal"
                    :class="customData[key] === sVal ? 'bg-indigo-600 text-white border-indigo-500 shadow-sm' : 'bg-slate-950 hover:bg-slate-900 text-slate-400 hover:text-indigo-200 border-slate-800'"
                    class="px-2.5 py-0.5 rounded-full border text-[8px] font-mono transition-all inline-flex items-center justify-center whitespace-nowrap leading-none cursor-pointer"
                  >
                    {{ sVal }}
                  </button>
                </div>
              </div>
            </div>

            <!-- Add Parameter Input Row -->
            <div class="flex gap-2 pt-3 border-t border-slate-900">
              <Input
                v-model="newFieldKey"
                placeholder="Parameter Name (e.g. Voltage, Protocol, Firmware)"
                class="flex-1 rounded-xl h-9 bg-slate-950 border-slate-800 text-slate-300 text-xs uppercase font-bold"
                @keyup.enter="addCustomField"
              />
              <Input
                v-model="newFieldValue"
                placeholder="Default Value..."
                class="flex-1 rounded-xl h-9 bg-slate-950 border-slate-800 text-slate-300 text-xs font-mono"
                @keyup.enter="addCustomField"
              />
              <Button
                variant="outline"
                size="sm"
                class="h-9 px-4 rounded-xl border-slate-800 bg-slate-900 text-xs font-bold uppercase tracking-wider hover:bg-slate-800"
                @click="addCustomField"
              >
                <Plus class="size-3.5 mr-1" />
                Add Field
              </Button>
            </div>
          </div>
        </div>

        <!-- TAB 5: JSON & TEMPLATE ENGINE -->
        <div v-show="activeTab === 'templates'" class="space-y-6 animate-in fade-in duration-200">
          
          <!-- Category Filter Bar -->
          <div class="flex items-center gap-2 overflow-x-auto custom-scrollbar pb-2">
            <button
              v-for="cat in templateCategories"
              :key="cat.id"
              @click="selectedCategory = cat.id"
              :class="selectedCategory === cat.id ? 'bg-indigo-600 text-white shadow-md' : 'bg-slate-900 text-slate-400 hover:text-slate-200 border border-slate-800'"
              class="px-4 py-2 rounded-full text-[9px] font-black uppercase tracking-widest shrink-0 transition-all whitespace-nowrap inline-flex items-center justify-center leading-none shadow-sm"
            >
              {{ cat.label }}
            </button>
          </div>

          <!-- Template Cards Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 max-h-56 overflow-y-auto custom-scrollbar p-1">
            <div
              v-for="tpl in filteredTemplates"
              :key="tpl.id"
              @click="selectTemplate(tpl.id)"
              :class="selectedTemplateId === tpl.id ? 'border-indigo-500 bg-indigo-950/40 ring-1 ring-indigo-500/50' : 'border-slate-800 bg-slate-900/60 hover:bg-slate-900 hover:border-slate-700'"
              class="p-3.5 rounded-2xl border cursor-pointer transition-all flex flex-col justify-between group"
            >
              <div>
                <div class="flex items-center justify-between gap-2 mb-1.5">
                  <span class="text-[8px] font-black uppercase tracking-wider px-2.5 py-1 rounded-full border border-indigo-500/30 text-indigo-400 bg-indigo-500/10 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">
                    {{ tpl.category }}
                  </span>
                  <span v-if="tpl.isCustom" class="text-[8px] font-mono font-black uppercase tracking-wider px-2.5 py-1 rounded-full border border-amber-500/30 text-amber-400 bg-amber-500/10 inline-flex items-center justify-center whitespace-nowrap leading-none shadow-sm">Custom</span>
                </div>
                <h4 class="text-xs font-black text-slate-100 group-hover:text-indigo-300 transition-colors uppercase leading-snug">
                  {{ tpl.name }}
                </h4>
                <p class="text-[10px] text-slate-400 line-clamp-2 mt-1 leading-relaxed">
                  {{ tpl.description }}
                </p>
              </div>

              <div class="flex items-center justify-between mt-3 pt-2 border-t border-slate-800/60 text-[9px] font-mono text-slate-500">
                <span>{{ tpl.variables.length }} Variables</span>
                <span class="text-indigo-400 group-hover:underline flex items-center gap-1 font-bold uppercase">
                  Select <ChevronRight class="size-3" />
                </span>
              </div>
            </div>
          </div>

          <!-- Active Template Variable Interpolation Form & Preview -->
          <div v-if="activeTemplate" class="p-5 rounded-2xl bg-slate-900/80 border border-slate-800 space-y-5">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div class="flex items-center gap-2.5">
                <Sparkles class="size-4 text-indigo-400 shrink-0" />
                <div>
                  <h4 class="text-xs font-black text-slate-200 uppercase tracking-wider">
                    Template: {{ activeTemplate.name }}
                  </h4>
                  <p class="text-[10px] text-slate-500 mt-0.5">{{ activeTemplate.description }}</p>
                </div>
              </div>

              <div class="flex items-center gap-2">
                <Button variant="ghost" size="sm" @click="resetVariables" class="h-7 text-[10px] font-bold text-slate-400 uppercase">
                  <RefreshCw class="size-3 mr-1" /> Reset
                </Button>
                <Button
                  @click="handleApplyTemplate"
                  class="h-8 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-4 shadow-lg shadow-emerald-600/20 flex items-center gap-1.5"
                >
                  <Check class="size-3.5" />
                  <span>Apply Template to Asset</span>
                </Button>
              </div>
            </div>

            <!-- Dynamic Variables Input Grid -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div
                v-for="v in activeVariables"
                :key="v.name"
                class="space-y-1.5"
              >
                <div class="flex items-center justify-between">
                  <Label class="text-[10px] font-black text-slate-400 uppercase tracking-wider">
                    {{ v.label }} <span v-if="v.required" class="text-rose-400">*</span>
                  </Label>
                  <code class="text-[9px] text-slate-600 font-mono">&#123;&#123;{{ v.name }}&#125;&#125;</code>
                </div>

                <!-- Select option if variable has options -->
                <div v-if="v.type === 'select' && v.options" class="space-y-2">
                  <select
                    :value="variableValues[v.name]"
                    @change="setVariable(v.name, ($event.target as HTMLSelectElement).value)"
                    class="w-full rounded-xl h-10 px-3 bg-slate-950 border border-slate-800 text-slate-200 text-xs font-bold focus:border-indigo-500 focus:outline-none"
                  >
                    <option v-for="opt in v.options" :key="opt" :value="opt">{{ opt }}</option>
                  </select>
                  <!-- Option Pills Suggestion List -->
                  <div class="flex flex-wrap gap-1.5 pt-0.5">
                    <button
                      v-for="opt in v.options"
                      :key="opt"
                      type="button"
                      @click="setVariable(v.name, opt)"
                      :class="variableValues[v.name] === opt ? 'bg-indigo-600 text-white border-indigo-500 shadow-sm' : 'bg-slate-900 text-slate-400 hover:text-slate-200 border-slate-800 hover:border-slate-700'"
                      class="px-3 py-1 rounded-full border text-[8px] font-black uppercase tracking-wider transition-all inline-flex items-center justify-center whitespace-nowrap leading-none cursor-pointer"
                    >
                      {{ opt }}
                    </button>
                  </div>
                </div>

                <!-- Number Input -->
                <Input
                  v-else-if="v.type === 'number'"
                  type="number"
                  :value="variableValues[v.name]"
                  @input="setVariable(v.name, parseFloat(($event.target as HTMLInputElement).value) || 0)"
                  class="rounded-xl h-10 bg-slate-950 border-slate-800 text-slate-200 font-mono text-xs"
                />

                <!-- Text Input -->
                <Input
                  v-else
                  type="text"
                  :value="variableValues[v.name]"
                  :placeholder="v.placeholder || `Enter ${v.label}...`"
                  @input="setVariable(v.name, ($event.target as HTMLInputElement).value)"
                  class="rounded-xl h-10 bg-slate-950 border-slate-800 text-slate-200 font-bold text-xs"
                />

                <p v-if="v.description" class="text-[9px] text-slate-500">{{ v.description }}</p>
              </div>
            </div>

            <!-- Live Evaluated JSON Preview Accordion / Code Box -->
            <div class="space-y-2 pt-2 border-t border-slate-800">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2 text-[10px] font-black uppercase text-slate-400">
                  <Code class="size-3 text-indigo-400" />
                  <span>Live Evaluated JSON Result</span>
                </div>
                <div class="flex items-center gap-2">
                  <Button variant="ghost" size="sm" @click="copyEvaluatedJson" class="h-6 text-[9px] font-mono text-slate-400 uppercase">
                    <Copy v-if="!copyJsonFeedback" class="size-3 mr-1" />
                    <Check v-else class="size-3 mr-1 text-emerald-400" />
                    <span>{{ copyJsonFeedback ? 'Copied' : 'Copy JSON' }}</span>
                  </Button>
                </div>
              </div>

              <pre class="p-4 rounded-xl bg-slate-950 border border-slate-800/80 text-[11px] font-mono text-indigo-300 max-h-48 overflow-y-auto custom-scrollbar whitespace-pre-wrap leading-relaxed">{{ evaluatedJsonString }}</pre>
            </div>
          </div>

          <!-- Save Current Asset As Custom Template Trigger -->
          <div class="p-4 rounded-2xl bg-indigo-950/20 border border-indigo-900/30 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <div class="p-2 rounded-xl bg-indigo-500/20 text-indigo-400">
                <Save class="size-4" />
              </div>
              <div>
                <h4 class="text-xs font-black text-slate-200 uppercase tracking-wider">Save Current Configuration as Template</h4>
                <p class="text-[10px] text-slate-500 mt-0.5">Persist this asset setup into your reusable template library.</p>
              </div>
            </div>
            <Button
              variant="outline"
              size="sm"
              @click="showSaveTemplateModal = true"
              class="rounded-xl border-indigo-500/30 bg-indigo-600/20 text-indigo-300 hover:bg-indigo-600 hover:text-white text-xs font-bold uppercase tracking-wider h-9"
            >
              Save as Template
            </Button>
          </div>

          <!-- Save Template Modal Mini Dialog -->
          <div v-if="showSaveTemplateModal" class="p-5 rounded-2xl bg-slate-900 border border-indigo-500/50 shadow-2xl space-y-4 animate-in fade-in">
            <div class="flex items-center justify-between border-b border-slate-800 pb-2">
              <h4 class="text-xs font-black text-white uppercase tracking-wider">Name New Template Preset</h4>
              <button @click="showSaveTemplateModal = false" class="text-slate-500 hover:text-white">
                <X class="size-4" />
              </button>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div class="space-y-1">
                <Label class="text-[10px] font-black text-slate-400 uppercase">Template Name</Label>
                <Input v-model="newTemplateName" placeholder="e.g. Standard CNC Machine Node" class="h-9 rounded-xl bg-slate-950 border-slate-800 text-xs text-white font-bold" />
              </div>
              <div class="space-y-1">
                <Label class="text-[10px] font-black text-slate-400 uppercase">Category</Label>
                <select v-model="newTemplateCategory" class="w-full h-9 rounded-xl px-3 bg-slate-950 border border-slate-800 text-xs text-white font-bold">
                  <option value="Controller">Controller</option>
                  <option value="Vision">Vision</option>
                  <option value="Motion">Motion</option>
                  <option value="Sensor">Sensor</option>
                  <option value="Software">Software</option>
                  <option value="Network">Network</option>
                  <option value="Dispensing">Dispensing</option>
                  <option value="General">General</option>
                </select>
              </div>
              <div class="space-y-1 col-span-2">
                <Label class="text-[10px] font-black text-slate-400 uppercase">Description</Label>
                <Input v-model="newTemplateDesc" placeholder="Brief explanation of the template..." class="h-9 rounded-xl bg-slate-950 border-slate-800 text-xs text-slate-300" />
              </div>
            </div>
            <div class="flex justify-end gap-2 pt-2">
              <Button variant="ghost" size="sm" @click="showSaveTemplateModal = false" class="text-xs text-slate-400 uppercase font-bold">Cancel</Button>
              <Button size="sm" @click="handleSaveAsCustomTemplate" class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase px-4">Save Preset</Button>
            </div>
          </div>

        </div>

      </div>

      <!-- Action Footer -->
      <div class="p-6 bg-slate-900/70 border-t border-slate-900 flex items-center justify-between gap-4 shrink-0">
        <Button
          variant="ghost"
          @click="emit('update:open', false)"
          class="rounded-xl h-11 text-xs font-black text-slate-400 uppercase tracking-wider hover:bg-slate-800 hover:text-slate-200 border border-slate-800 px-6"
        >
          Cancel
        </Button>

        <div class="flex items-center gap-3">
          <Button
            v-if="activeTab !== 'templates'"
            variant="outline"
            @click="activeTab = 'templates'"
            class="rounded-xl h-11 text-xs font-bold text-indigo-400 uppercase tracking-wider border-indigo-800/40 bg-indigo-950/20 hover:bg-indigo-900/40 hidden sm:flex items-center gap-2"
          >
            <Sparkles class="size-3.5" />
            <span>Load Template</span>
          </Button>

          <Button
            @click="handleSave"
            class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl h-11 text-xs font-black uppercase tracking-wider shadow-lg shadow-indigo-600/20 transition-all flex items-center justify-center gap-2 px-8"
          >
            <Zap class="w-4 h-4" />
            <span>{{ mode === 'create' ? 'Commit to Infrastructure' : 'Save Asset Record' }}</span>
          </Button>
        </div>
      </div>

    </DialogContent>
  </Dialog>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 5px;
  height: 5px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(15, 23, 42, 0.4);
  border-radius: 9999px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: linear-gradient(180deg, rgba(99, 102, 241, 0.45), rgba(79, 70, 229, 0.65));
  border-radius: 9999px;
  box-shadow: 0 0 6px rgba(99, 102, 241, 0.25);
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: linear-gradient(180deg, rgba(129, 140, 248, 0.8), rgba(99, 102, 241, 0.95));
  box-shadow: 0 0 10px rgba(99, 102, 241, 0.6);
}
</style>
