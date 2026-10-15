<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { toast } from 'vue-sonner'
import {
  PackagePlus,
  Boxes,
  Layers,
  Cpu,
  FileSpreadsheet,
  Upload,
  ArrowLeft,
  CheckCircle2,
  AlertTriangle,
  Info,
  Building,
  Barcode,
  DollarSign,
  Clock,
  Sparkles,
  Plus,
  Trash2,
  Hash,
  FileText,
  Check,
  RotateCcw,
  Code,
  Wrench,
  ShieldCheck,
  SlidersHorizontal,
  ChevronRight,
  Eye,
  Server,
  Network
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import { usePartsInventory } from '~/composables/usePartsInventory'
import type { InventoryPart, AssetTemplateDefinition } from '~/types/inventory'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = typeof useRouter === 'function' ? useRouter() : null
const navigate = (path: string) => {
  if (router?.push) {
    router.push(path)
  } else if (typeof navigateTo === 'function') {
    navigateTo(path)
  }
}

const {
  templates,
  indexedFields,
  fetchTemplates,
  fetchIndexedFields,
  logPart,
  bulkLogParts,
  loading: storeLoading
} = usePartsInventory()

// Intake Mode: Single Asset Intake vs Batch CSV/JSON Import
type IntakeMode = 'single' | 'batch'
const activeMode = ref<IntakeMode>('single')

// Currencies
const currencies = ['EUR', 'USD', 'HUF'] as const
type Currency = typeof currencies[number]
const selectedCurrency = ref<Currency>('EUR')
const exchangeRates: Record<Currency, number> = {
  EUR: 1.0,
  USD: 1.08,
  HUF: 400.0
}

// ─────────────────────────────────────────────────────────────────────────────
// 1. SINGLE ITEM INTAKE FORM
// ─────────────────────────────────────────────────────────────────────────────
const selectedTemplateId = ref('')

const singleForm = ref({
  sku: '',
  name: '',
  description: '',
  classification: 'Hardware' as 'Hardware' | 'Software' | 'Spare Part',
  trackingType: 'serialized' as 'serialized' | 'bulk',
  condition: 'new' as 'new' | 'used' | 'donor' | 'obsolete' | 'scrap',
  operationalState: 'working' as 'working' | 'in_service' | 'evaluation' | 'broken',
  serialNumber: '',
  location: 'Crib A - Shelf 02',
  quantity: 1,
  minSafetyStock: 2,
  maxSafetyStock: 10,
  manufacturerName: 'Generic OEM',
  supplierName: 'Phoenix Contact Fast Supply',
  leadTimeDays: 7,
  purchasePrice: 450,
  costCenter: 'CC-PROD-LINE-01',
  specs: [
    { key: 'Voltage', value: '24V DC' },
    { key: 'Torque', value: '15 Nm' },
    { key: 'Form Factor', value: 'DIN Rail 35mm' },
    { key: 'Ingress Protection', value: 'IP67' }
  ] as Array<{ key: string; value: string }>,
  schemaValues: {} as Record<string, any>
})

const isSingleSubmitting = ref(false)

// Instant Validation Feedback
const singleErrors = computed(() => {
  const errors: Record<string, string> = {}
  if (!singleForm.value.sku.trim()) {
    errors.sku = 'Part Number / SKU is required.'
  }
  if (!singleForm.value.name.trim()) {
    errors.name = 'Asset name is required.'
  }
  if (singleForm.value.quantity < 1) {
    errors.quantity = 'Quantity must be at least 1.'
  }
  if (singleForm.value.minSafetyStock < 0) {
    errors.minSafetyStock = 'Safety stock cannot be negative.'
  }
  if (singleForm.value.purchasePrice < 0) {
    errors.purchasePrice = 'Purchase price cannot be negative.'
  }
  if (singleForm.value.trackingType === 'serialized' && !singleForm.value.serialNumber.trim()) {
    errors.serialNumber = 'Serial number is required for serialized equipment.'
  }
  return errors
})

const isSingleFormValid = computed(() => Object.keys(singleErrors.value).length === 0)

// Quick SKU Generator
const generateSku = () => {
  const prefix = singleForm.value.classification === 'Hardware'
    ? 'HW'
    : singleForm.value.classification === 'Software'
    ? 'SW'
    : 'SP'
  const randomNum = Math.floor(1000 + Math.random() * 9000)
  singleForm.value.sku = `${prefix}-${randomNum}`
}

// Quick Serial Generator
const generateSerialNumber = () => {
  const yr = new Date().getFullYear()
  const rand = Math.floor(100000 + Math.random() * 900000)
  singleForm.value.serialNumber = `SN-${yr}-${rand}`
}

// Specs Management
const addSpec = () => {
  singleForm.value.specs.push({ key: '', value: '' })
}

const removeSpec = (index: number) => {
  singleForm.value.specs.splice(index, 1)
}

// Template Application
const selectedTemplate = computed(() => {
  if (!selectedTemplateId.value) return null
  return templates.value.find(t => t.id === selectedTemplateId.value) || null
})

const handleTemplateChange = (templateId: string) => {
  selectedTemplateId.value = templateId
  if (!templateId) return

  const tmpl = templates.value.find(t => t.id === templateId)
  if (!tmpl) return

  // Inherit specifications & classifications
  if (tmpl.name) singleForm.value.name = tmpl.name
  if (tmpl.description) singleForm.value.description = tmpl.description
  if (tmpl.topLevelCategory) {
    singleForm.value.classification = tmpl.topLevelCategory === 'Hardware' ? 'Hardware' : 'Software'
  }

  // Generate SKU from identifier pattern
  if (tmpl.identifierPattern) {
    const counter = tmpl.sequentialCounter || Math.floor(100 + Math.random() * 900)
    singleForm.value.sku = tmpl.identifierPattern.replace('{number}', String(counter))
  }

  // Inherit fixed specs
  if (tmpl.fixedFields) {
    if (tmpl.fixedFields.manufacturer) {
      singleForm.value.manufacturerName = String(tmpl.fixedFields.manufacturer)
    }
    if (tmpl.fixedFields.supplier) {
      singleForm.value.supplierName = String(tmpl.fixedFields.supplier)
    }
    if (tmpl.fixedFields.basePriceEur !== undefined) {
      singleForm.value.purchasePrice = tmpl.fixedFields.basePriceEur
    }

    if (tmpl.fixedFields.specs && typeof tmpl.fixedFields.specs === 'object') {
      const inheritedSpecs: Array<{ key: string; value: string }> = []
      for (const [k, v] of Object.entries(tmpl.fixedFields.specs)) {
        if (k && v !== undefined) {
          inheritedSpecs.push({ key: k, value: String(v) })
        }
      }
      if (inheritedSpecs.length > 0) {
        singleForm.value.specs = inheritedSpecs
      }
    }
  }

  // Reset & prepare schema fields
  singleForm.value.schemaValues = {}
  if (tmpl.instanceSpecificFieldsSchema && Array.isArray(tmpl.instanceSpecificFieldsSchema)) {
    for (const field of tmpl.instanceSpecificFieldsSchema) {
      singleForm.value.schemaValues[field.key] = field.defaultValue || ''
    }
  }

  toast.info(`Inherited specifications from template: ${tmpl.name}`)
}

// Price Conversion
const convertedPrice = computed(() => {
  const rate = exchangeRates[selectedCurrency.value] || 1.0
  const eurVal = singleForm.value.purchasePrice || 0
  return (eurVal * rate).toFixed(selectedCurrency.value === 'HUF' ? 0 : 2)
})

// Submit Single Part
const handleSubmitSingle = async () => {
  if (!isSingleFormValid.value) {
    toast.error('Please resolve validation errors before submitting.')
    return
  }

  isSingleSubmitting.value = true
  try {
    // Pack specs into object
    const specsMap: Record<string, any> = {}
    for (const spec of singleForm.value.specs) {
      if (spec.key.trim()) {
        specsMap[spec.key.trim()] = spec.value
      }
    }

    // Merge instance schema values
    Object.assign(specsMap, singleForm.value.schemaValues)

    const payload: Partial<InventoryPart> = {
      id: `prt-${Date.now()}-${Math.floor(100 + Math.random() * 900)}`,
      customIdentifier: singleForm.value.sku.trim(),
      templateId: selectedTemplateId.value || undefined,
      name: singleForm.value.name.trim(),
      displayName: singleForm.value.name.trim(),
      category: (singleForm.value.classification === 'Software' ? 'Software' : 'Hardware') as 'Hardware' | 'Software',
      subCategory: singleForm.value.classification === 'Spare Part' ? 'Spare Part' : undefined,
      trackingType: singleForm.value.trackingType,
      condition: singleForm.value.condition,
      operationalState: singleForm.value.operationalState,
      location: singleForm.value.location.trim(),
      priceEur: Number(singleForm.value.purchasePrice) || 0,
      estimatedResellPriceEur: Number(singleForm.value.purchasePrice) || 0,
      quantity: Number(singleForm.value.quantity) || 1,
      minQuantity: Number(singleForm.value.minSafetyStock) || 1,
      maxQuantity: Number(singleForm.value.maxSafetyStock) || 10,
      manufacturer: { name: singleForm.value.manufacturerName.trim() || 'OEM' },
      supplier: { name: singleForm.value.supplierName.trim() || 'Direct Vendor' },
      serialNumber: singleForm.value.trackingType === 'serialized' ? singleForm.value.serialNumber.trim() : undefined,
      tags: [singleForm.value.classification.toLowerCase().replace(' ', '-')],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }

    await logPart(payload)
    toast.success(`Asset "${payload.name}" (${payload.customIdentifier}) successfully ingested!`)
    navigate('/dashboard/inventory')
  } catch (err: any) {
    console.error('Failed to log part:', err)
    toast.error(err?.data?.message || err?.message || 'Failed to ingest asset. Please check fields and retry.')
  } finally {
    isSingleSubmitting.value = false
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. BATCH CSV / JSON IMPORT WORKBENCH
// ─────────────────────────────────────────────────────────────────────────────
type BatchFormat = 'json' | 'csv'
const batchFormat = ref<BatchFormat>('json')
const rawBatchContent = ref('')
const isBatchSubmitting = ref(false)

const sampleJson = `[
  {
    "sku": "IPC-SERVO-101",
    "name": "Beckhoff CX5140 Modular Controller IPC",
    "category": "Hardware",
    "trackingType": "serialized",
    "serialNumber": "SN-BKF-99014",
    "location": "Crib A - Bin 12",
    "quantity": 1,
    "minSafetyStock": 1,
    "supplierName": "Beckhoff Automation",
    "purchasePrice": 2850,
    "specs": { "Voltage": "24V DC", "Torque": "N/A", "Form Factor": "DIN Rail" }
  },
  {
    "sku": "FST-VLV-M5",
    "name": "Festo Compact Solenoid Valve M5",
    "category": "Hardware",
    "trackingType": "bulk",
    "location": "Crib B - Bin 44",
    "quantity": 25,
    "minSafetyStock": 10,
    "supplierName": "Festo Pneumatics",
    "purchasePrice": 85,
    "specs": { "Voltage": "24V DC", "Pressure": "6 Bar", "Form Factor": "Manifold Sub-base" }
  },
  {
    "sku": "SW-TWINCAT-03",
    "name": "TwinCAT 3 NC PTP Runtime License",
    "category": "Software",
    "trackingType": "serialized",
    "serialNumber": "LIC-TC3-88712",
    "location": "Virtual License Pool",
    "quantity": 1,
    "minSafetyStock": 1,
    "supplierName": "Beckhoff Automation",
    "purchasePrice": 620,
    "specs": { "License Type": "Runtime Floating", "Platform": "Windows 10 IoT" }
  }
]`

const sampleCsv = `sku,name,category,trackingType,serialNumber,location,quantity,minSafetyStock,supplierName,purchasePrice
IPC-SERVO-101,Beckhoff CX5140 Modular Controller IPC,Hardware,serialized,SN-BKF-99014,Crib A - Bin 12,1,1,Beckhoff Automation,2850
FST-VLV-M5,Festo Compact Solenoid Valve M5,Hardware,bulk,,Crib B - Bin 44,25,10,Festo Pneumatics,85
SW-TWINCAT-03,TwinCAT 3 NC PTP Runtime License,Software,serialized,LIC-TC3-88712,Virtual License Pool,1,1,Beckhoff Automation,620`

const loadSample = () => {
  rawBatchContent.value = batchFormat.value === 'json' ? sampleJson : sampleCsv
  toast.info(`Loaded sample ${batchFormat.value.toUpperCase()} dataset`)
}

// Parse CSV Helper
const parseCsv = (csvText: string): any[] => {
  const lines = csvText.trim().split('\n').filter(l => l.trim().length > 0)
  if (lines.length < 2) return []

  const headerLine = lines[0]
  const headers = headerLine.split(',').map(h => h.trim().replace(/^"|"$/g, ''))

  const rows: any[] = []
  for (let i = 1; i < lines.length; i++) {
    const values = lines[i].split(',').map(v => v.trim().replace(/^"|"$/g, ''))
    const obj: Record<string, any> = {}
    headers.forEach((h, idx) => {
      obj[h] = values[idx] || ''
    })
    rows.push(obj)
  }
  return rows
}

// Parsed Batch Items
const parsedBatchResult = computed(() => {
  const text = rawBatchContent.value.trim()
  if (!text) {
    return { items: [], error: null }
  }

  try {
    if (batchFormat.value === 'json') {
      const parsed = JSON.parse(text)
      if (!Array.isArray(parsed)) {
        return { items: [], error: 'Root JSON element must be an array of objects [ { ... }, { ... } ].' }
      }
      return { items: parsed, error: null }
    } else {
      const items = parseCsv(text)
      if (items.length === 0) {
        return { items: [], error: 'No valid data rows found in CSV text.' }
      }
      return { items, error: null }
    }
  } catch (err: any) {
    return { items: [], error: `Parsing error: ${err.message}` }
  }
})

// File Drag & Drop / Upload
const handleFileUpload = (event: Event) => {
  const input = event.target as HTMLInputElement
  if (!input.files || input.files.length === 0) return

  const file = input.files[0]
  const isCsv = file.name.endsWith('.csv')
  const isJson = file.name.endsWith('.json')

  if (!isCsv && !isJson) {
    toast.error('Unsupported file format. Please upload a .json or .csv file.')
    return
  }

  batchFormat.value = isCsv ? 'csv' : 'json'
  const reader = new FileReader()
  reader.onload = (e) => {
    rawBatchContent.value = String(e.target?.result || '')
    toast.success(`Loaded file: ${file.name}`)
  }
  reader.readAsText(file)
}

// Submit Batch Intake
const handleSubmitBatch = async () => {
  const { items, error } = parsedBatchResult.value
  if (error || items.length === 0) {
    toast.error(error || 'No items available to import.')
    return
  }

  isBatchSubmitting.value = true
  try {
    const normalizedParts: Array<Partial<InventoryPart>> = items.map((raw: any, index: number) => {
      const sku = raw.sku || raw.customIdentifier || raw.partNumber || `IMP-${Date.now()}-${index + 1}`
      const name = raw.name || raw.displayName || `Imported Item ${index + 1}`
      const cat = (String(raw.category || raw.classification).toLowerCase() === 'software' ? 'Software' : 'Hardware') as 'Hardware' | 'Software'
      const tracking = raw.trackingType === 'bulk' ? 'bulk' : 'serialized'
      const qty = Number(raw.quantity) || 1
      const price = Number(raw.purchasePrice || raw.priceEur || raw.price) || 0

      return {
        id: `prt-batch-${Date.now()}-${index}`,
        customIdentifier: String(sku),
        name: String(name),
        displayName: String(name),
        category: cat,
        trackingType: tracking,
        condition: (raw.condition || 'new') as any,
        operationalState: (raw.operationalState || 'working') as any,
        location: String(raw.location || 'Central Crib'),
        quantity: qty,
        minQuantity: Number(raw.minSafetyStock || raw.minQuantity) || 1,
        priceEur: price,
        estimatedResellPriceEur: price,
        serialNumber: tracking === 'serialized' ? String(raw.serialNumber || `SN-${sku}-${index}`) : undefined,
        manufacturer: { name: String(raw.manufacturerName || raw.manufacturer || 'OEM') },
        supplier: { name: String(raw.supplierName || raw.supplier || 'Vendor') },
        tags: ['batch-import'],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }
    })

    await bulkLogParts(normalizedParts)
    toast.success(`Successfully imported ${normalizedParts.length} assets into the registry!`)
    navigate('/dashboard/inventory')
  } catch (err: any) {
    console.error('Batch import failed:', err)
    toast.error(err?.data?.message || err?.message || 'Failed to complete batch import.')
  } finally {
    isBatchSubmitting.value = false
  }
}

onMounted(async () => {
  generateSku()
  generateSerialNumber()
  await Promise.all([
    fetchTemplates(),
    fetchIndexedFields()
  ])
})
</script>

<template>
  <div class="space-y-6 pb-20">
    <!-- Breadcrumbs & Page Header -->
    <div class="border-b border-border pb-5">
      <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-primary mb-2">
        <NuxtLink to="/dashboard" class="hover:underline flex items-center gap-1">
          <span>Dashboard</span>
        </NuxtLink>
        <ChevronRight class="w-3.5 h-3.5 text-muted-foreground/60" />
        <NuxtLink to="/dashboard/inventory" class="hover:underline flex items-center gap-1">
          <Boxes class="w-3.5 h-3.5" />
          <span>Inventory</span>
        </NuxtLink>
        <ChevronRight class="w-3.5 h-3.5 text-muted-foreground/60" />
        <span class="text-foreground font-extrabold flex items-center gap-1">
          <PackagePlus class="w-3.5 h-3.5 text-primary" />
          <span>Ingest New Asset</span>
        </span>
      </div>

      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-2.5">
            <PackagePlus class="w-7 h-7 text-primary" />
            <span>Industrial Asset & Parts Ingestion Workbench</span>
          </h1>
          <p class="text-xs text-muted-foreground mt-1">
            Register hardware components, serialized kinematics, spare part buffers, or batch import ERP/BOM inventories.
          </p>
        </div>

        <div class="flex items-center gap-2">
          <NuxtLink to="/dashboard/inventory">
            <Button variant="outline" size="sm" class="h-8 text-xs gap-1.5 cursor-pointer">
              <ArrowLeft class="w-3.5 h-3.5" />
              <span>Back to Registry</span>
            </Button>
          </NuxtLink>
        </div>
      </div>
    </div>

    <!-- Mode Selector Tabs: Single Item vs Batch Import -->
    <div class="flex items-center justify-between gap-3 p-1.5 bg-muted/30 border border-border rounded-xl">
      <div class="flex items-center gap-1.5">
        <button
          type="button"
          @click="activeMode = 'single'"
          class="px-4 py-2 text-xs font-bold rounded-lg transition-all flex items-center gap-2 cursor-pointer"
          :class="activeMode === 'single' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
        >
          <Cpu class="w-4 h-4" />
          <span>Single Item Intake</span>
        </button>

        <button
          type="button"
          @click="activeMode = 'batch'"
          class="px-4 py-2 text-xs font-bold rounded-lg transition-all flex items-center gap-2 cursor-pointer"
          :class="activeMode === 'batch' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
        >
          <FileSpreadsheet class="w-4 h-4" />
          <span>Batch CSV / JSON Import</span>
          <Badge v-if="parsedBatchResult.items.length > 0" variant="outline" class="text-[10px] px-1 py-0 font-mono">
            {{ parsedBatchResult.items.length }}
          </Badge>
        </button>
      </div>

      <div class="hidden sm:flex items-center gap-2 text-xs text-muted-foreground pr-2 font-mono">
        <ShieldCheck class="w-3.5 h-3.5 text-emerald-500" />
        <span>Production Certified Mode</span>
      </div>
    </div>

    <!-- ===================================================================== -->
    <!-- 1. SINGLE ITEM INTAKE FORM                                            -->
    <!-- ===================================================================== -->
    <div v-if="activeMode === 'single'" class="space-y-6">
      <!-- Blueprint Template Inheritor Banner -->
      <div class="p-4 rounded-xl bg-card border border-border space-y-3 shadow-xs">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <div class="flex items-center gap-2">
            <Sparkles class="w-4 h-4 text-amber-500" />
            <h3 class="text-xs font-bold uppercase tracking-wider text-foreground">
              Inherit Blueprint Specifications (Class Template)
            </h3>
          </div>
          <span class="text-[11px] text-muted-foreground">
            Optional: Automatically prefill specs, tolerances, and identifier masks
          </span>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div class="sm:col-span-2">
            <select
              v-model="selectedTemplateId"
              @change="handleTemplateChange(selectedTemplateId)"
              class="w-full h-9 bg-background border border-border rounded-lg px-3 text-xs text-foreground focus:outline-hidden focus:border-primary cursor-pointer font-medium"
            >
              <option value="">-- Manual Standalone Component (No Template) --</option>
              <option v-for="tmpl in templates" :key="tmpl.id" :value="tmpl.id">
                {{ tmpl.name }} ({{ tmpl.topLevelCategory }} - {{ tmpl.identifierPattern }})
              </option>
            </select>
          </div>

          <div v-if="selectedTemplate" class="flex items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="outline" class="font-mono text-[10px] text-primary border-primary/30">
              Pattern: {{ selectedTemplate.identifierPattern }}
            </Badge>
            <span class="truncate">OEM: {{ selectedTemplate.fixedFields?.manufacturer || 'Generic' }}</span>
          </div>
        </div>
      </div>

      <!-- Main Form Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Left Column: Core Identity & Classification (Col 7) -->
        <div class="lg:col-span-7 space-y-6">
          <!-- Section 1: Identification & Naming -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
              <Hash class="w-4 h-4 text-primary" />
              <span>Identity & Classification</span>
            </h3>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <!-- Part Number / SKU -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <Label class="text-xs font-bold text-foreground">Part Number / SKU *</Label>
                  <button
                    type="button"
                    @click="generateSku"
                    class="text-[10px] text-primary hover:underline font-mono cursor-pointer"
                  >
                    Generate SKU
                  </button>
                </div>
                <Input
                  v-model="singleForm.sku"
                  placeholder="e.g. HW-1042 or SKU-8821"
                  class="font-mono text-xs uppercase"
                  :class="singleErrors.sku ? 'border-rose-500' : ''"
                />
                <p v-if="singleErrors.sku" class="text-[11px] text-rose-500 font-medium">
                  {{ singleErrors.sku }}
                </p>
              </div>

              <!-- Classification -->
              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Classification *</Label>
                <div class="grid grid-cols-3 gap-1 p-0.5 bg-muted/40 border border-border rounded-lg text-xs">
                  <button
                    type="button"
                    @click="singleForm.classification = 'Hardware'"
                    class="py-1 rounded-md transition-colors cursor-pointer text-center font-medium"
                    :class="singleForm.classification === 'Hardware' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                  >
                    Hardware
                  </button>
                  <button
                    type="button"
                    @click="singleForm.classification = 'Software'"
                    class="py-1 rounded-md transition-colors cursor-pointer text-center font-medium"
                    :class="singleForm.classification === 'Software' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                  >
                    Software
                  </button>
                  <button
                    type="button"
                    @click="singleForm.classification = 'Spare Part'"
                    class="py-1 rounded-md transition-colors cursor-pointer text-center font-medium"
                    :class="singleForm.classification === 'Spare Part' ? 'bg-primary text-primary-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                  >
                    Spare Part
                  </button>
                </div>
              </div>
            </div>

            <!-- Name -->
            <div class="space-y-1.5">
              <Label class="text-xs font-bold text-foreground">Asset Name / Title *</Label>
              <Input
                v-model="singleForm.name"
                placeholder="e.g. Siemens SIMATIC S7 Controller / KUKA In-line Servo Drive Alpha"
                class="text-xs"
                :class="singleErrors.name ? 'border-rose-500' : ''"
              />
              <p v-if="singleErrors.name" class="text-[11px] text-rose-500 font-medium">
                {{ singleErrors.name }}
              </p>
            </div>

            <!-- Description -->
            <div class="space-y-1.5">
              <Label class="text-xs font-bold text-foreground">Technical Description</Label>
              <textarea
                v-model="singleForm.description"
                rows="2"
                placeholder="Operational purpose, tolerances, mounting constraints, or replacement instructions..."
                class="w-full bg-background border border-border rounded-lg p-2.5 text-xs text-foreground focus:outline-hidden focus:border-primary resize-none"
              />
            </div>
          </div>

          <!-- Section 2: Serialization & Tracking -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
              <Barcode class="w-4 h-4 text-cyan-500" />
              <span>Tracking Paradigm & Serialization</span>
            </h3>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Tracking Type</Label>
                <select
                  v-model="singleForm.trackingType"
                  class="w-full h-9 bg-background border border-border rounded-lg px-3 text-xs text-foreground focus:outline-hidden focus:border-primary cursor-pointer font-medium"
                >
                  <option value="serialized">Serialized (Unique 1-to-1 Unit Tracking)</option>
                  <option value="bulk">Bulk Consumable (Quantity Pool / Bin Count)</option>
                </select>
              </div>

              <!-- Serial Number -->
              <div class="space-y-1.5">
                <div class="flex items-center justify-between">
                  <Label class="text-xs font-bold text-foreground">
                    Serial Number {{ singleForm.trackingType === 'serialized' ? '*' : '(Optional)' }}
                  </Label>
                  <button
                    v-if="singleForm.trackingType === 'serialized'"
                    type="button"
                    @click="generateSerialNumber"
                    class="text-[10px] text-primary hover:underline font-mono cursor-pointer"
                  >
                    Auto-Generate
                  </button>
                </div>
                <Input
                  v-model="singleForm.serialNumber"
                  placeholder="e.g. SN-2026-94812"
                  class="font-mono text-xs uppercase"
                  :disabled="singleForm.trackingType === 'bulk'"
                  :class="singleErrors.serialNumber ? 'border-rose-500' : ''"
                />
                <p v-if="singleErrors.serialNumber" class="text-[11px] text-rose-500 font-medium">
                  {{ singleErrors.serialNumber }}
                </p>
              </div>
            </div>

            <!-- Condition & Operational State -->
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Lifecycle Condition</Label>
                <select
                  v-model="singleForm.condition"
                  class="w-full h-9 bg-background border border-border rounded-lg px-3 text-xs text-foreground focus:outline-hidden focus:border-primary cursor-pointer font-medium"
                >
                  <option value="new">New (Factory OEM Fresh)</option>
                  <option value="used">Used (Functional / Refurbished)</option>
                  <option value="donor">Donor (Kept for Harvesting Spares)</option>
                  <option value="obsolete">Obsolete (Discontinued Support)</option>
                  <option value="scrap">Scrap (Decommission / Disposal)</option>
                </select>
              </div>

              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Operational Status</Label>
                <select
                  v-model="singleForm.operationalState"
                  class="w-full h-9 bg-background border border-border rounded-lg px-3 text-xs text-foreground focus:outline-hidden focus:border-primary cursor-pointer font-medium"
                >
                  <option value="working">Working / Certified Active</option>
                  <option value="in_service">In-Service (Workshop Testing)</option>
                  <option value="evaluation">Pending Diagnostic Evaluation</option>
                  <option value="broken">Broken / Faulty Component</option>
                </select>
              </div>
            </div>
          </div>

          <!-- Section 3: Technical Specifications (Voltage, Torque, Form Factor, etc.) -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
                <SlidersHorizontal class="w-4 h-4 text-amber-500" />
                <span>Technical Specifications</span>
              </h3>
              <Button
                type="button"
                variant="outline"
                size="sm"
                @click="addSpec"
                class="h-7 text-xs gap-1 cursor-pointer"
              >
                <Plus class="w-3.5 h-3.5" />
                <span>Add Spec Property</span>
              </Button>
            </div>

            <div class="space-y-2.5">
              <div
                v-for="(spec, index) in singleForm.specs"
                :key="index"
                class="flex items-center gap-2.5"
              >
                <Input
                  v-model="spec.key"
                  placeholder="Key (e.g. Voltage, Torque, Bus Protocol)"
                  class="w-1/3 text-xs font-mono font-medium"
                />
                <Input
                  v-model="spec.value"
                  placeholder="Value (e.g. 24V DC, 15 Nm, PROFINET)"
                  class="flex-1 text-xs font-mono"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  @click="removeSpec(index)"
                  class="h-8 w-8 p-0 text-muted-foreground hover:text-rose-500 cursor-pointer"
                  title="Remove spec"
                >
                  <Trash2 class="w-3.5 h-3.5" />
                </Button>
              </div>

              <div v-if="singleForm.specs.length === 0" class="p-4 text-center text-xs text-muted-foreground border border-dashed border-border rounded-lg">
                No technical specifications configured. Click "Add Spec Property" to define engineering parameters.
              </div>
            </div>

            <!-- Dynamic Instance Schema Fields from Selected Template -->
            <div v-if="selectedTemplate?.instanceSpecificFieldsSchema && selectedTemplate.instanceSpecificFieldsSchema.length > 0" class="pt-3 border-t border-border space-y-3">
              <span class="text-xs font-bold text-foreground uppercase tracking-wider block">
                Template Instance Fields ({{ selectedTemplate.name }})
              </span>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div
                  v-for="field in selectedTemplate.instanceSpecificFieldsSchema"
                  :key="field.key"
                  class="space-y-1"
                >
                  <Label class="text-xs text-muted-foreground font-mono">
                    {{ field.label || field.key }} {{ field.required ? '*' : '' }}
                  </Label>
                  <Input
                    v-model="singleForm.schemaValues[field.key]"
                    :placeholder="field.description || `Enter ${field.label || field.key}`"
                    class="text-xs font-mono"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Right Column: Warehouse Logistics, Vendor & Valuation (Col 5) -->
        <div class="lg:col-span-5 space-y-6">
          <!-- Section 4: Physical Warehouse Location & Quantities -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
              <Boxes class="w-4 h-4 text-primary" />
              <span>Storage Logistics & Stock Levels</span>
            </h3>

            <!-- Storage Crib / Bin Location -->
            <div class="space-y-1.5">
              <Label class="text-xs font-bold text-foreground">Storage Location (Crib / Bin) *</Label>
              <Input
                v-model="singleForm.location"
                placeholder="e.g. Crib A - Shelf 02 / Bin 14"
                class="text-xs font-mono"
              />
              <div class="flex flex-wrap gap-1.5 pt-1">
                <button
                  v-for="loc in ['Crib A - Bin 12', 'Crib B - Bin 04', 'Floor Buffer - Cell 01', 'High-Bay Rack 03']"
                  :key="loc"
                  type="button"
                  @click="singleForm.location = loc"
                  class="text-[10px] px-2 py-0.5 rounded-md bg-muted/60 hover:bg-muted text-muted-foreground hover:text-foreground font-mono transition-colors cursor-pointer"
                >
                  {{ loc }}
                </button>
              </div>
            </div>

            <!-- Quantity & Safety Stock Buffers -->
            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Intake Quantity *</Label>
                <Input
                  v-model.number="singleForm.quantity"
                  type="number"
                  :min="1"
                  class="font-mono text-xs font-bold"
                  :disabled="singleForm.trackingType === 'serialized'"
                  :class="singleErrors.quantity ? 'border-rose-500' : ''"
                />
                <span class="text-[10px] text-muted-foreground block">
                  {{ singleForm.trackingType === 'serialized' ? 'Fixed to 1 for serialized unit' : 'Current count to register' }}
                </span>
              </div>

              <div class="space-y-1.5">
                <Label class="text-xs font-bold text-foreground">Min Safety Stock *</Label>
                <Input
                  v-model.number="singleForm.minSafetyStock"
                  type="number"
                  :min="0"
                  class="font-mono text-xs font-bold"
                  :class="singleErrors.minSafetyStock ? 'border-rose-500' : ''"
                />
                <span class="text-[10px] text-muted-foreground block">
                  Triggers low stock alarm
                </span>
              </div>
            </div>
          </div>

          <!-- Section 5: Procurement, Supplier & Valuation -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
              <Building class="w-4 h-4 text-emerald-500" />
              <span>Procurement & Commercial Valuation</span>
            </h3>

            <!-- Supplier / Vendor -->
            <div class="space-y-1.5">
              <Label class="text-xs font-bold text-foreground">Supplier / Vendor</Label>
              <Input
                v-model="singleForm.supplierName"
                placeholder="e.g. Phoenix Contact, Siemens AG, Festo"
                class="text-xs"
              />
            </div>

            <!-- Lead Time Days -->
            <div class="space-y-1.5">
              <Label class="text-xs font-bold text-foreground flex items-center justify-between">
                <span>Procurement Lead Time</span>
                <span class="font-mono text-[10px] text-muted-foreground">{{ singleForm.leadTimeDays }} days</span>
              </Label>
              <div class="flex items-center gap-2">
                <Input
                  v-model.number="singleForm.leadTimeDays"
                  type="number"
                  :min="1"
                  class="font-mono text-xs w-28"
                />
                <span class="text-xs text-muted-foreground">business days for replenishment</span>
              </div>
            </div>

            <!-- Purchase Price & Live Currency Conversion -->
            <div class="space-y-2 pt-2 border-t border-border">
              <div class="flex items-center justify-between">
                <Label class="text-xs font-bold text-foreground">Unit Purchase Price</Label>
                <div class="flex items-center p-0.5 bg-muted/50 rounded-md border border-border text-[10px]">
                  <button
                    v-for="curr in currencies"
                    :key="curr"
                    type="button"
                    @click="selectedCurrency = curr"
                    class="px-2 py-0.5 rounded font-mono font-bold cursor-pointer transition-colors"
                    :class="selectedCurrency === curr ? 'bg-background text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground'"
                  >
                    {{ curr }}
                  </button>
                </div>
              </div>

              <div class="grid grid-cols-2 gap-3 items-center">
                <div class="relative">
                  <span class="absolute left-3 top-2.5 text-xs font-bold text-muted-foreground">€</span>
                  <Input
                    v-model.number="singleForm.purchasePrice"
                    type="number"
                    :min="0"
                    step="0.01"
                    placeholder="0.00"
                    class="pl-7 font-mono text-xs font-bold"
                  />
                </div>

                <div class="p-2.5 rounded-lg bg-muted/30 border border-border text-right">
                  <span class="text-[10px] text-muted-foreground uppercase font-mono block">Converted Value</span>
                  <span class="text-sm font-mono font-black text-foreground">
                    {{ convertedPrice }} {{ selectedCurrency }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Section 6: Ingestion Summary & Final Submission -->
          <div class="p-5 rounded-2xl bg-card border-2 border-primary/20 space-y-4 shadow-lg">
            <h4 class="text-xs font-bold uppercase tracking-wider text-primary flex items-center gap-2">
              <ShieldCheck class="w-4 h-4 text-primary" />
              <span>Intake Verification Summary</span>
            </h4>

            <div class="space-y-1.5 text-xs divide-y divide-border/60">
              <div class="py-1 flex justify-between">
                <span class="text-muted-foreground">Part SKU:</span>
                <span class="font-mono font-bold text-foreground">{{ singleForm.sku || 'N/A' }}</span>
              </div>
              <div class="py-1 flex justify-between">
                <span class="text-muted-foreground">Classification:</span>
                <span class="font-medium text-foreground">{{ singleForm.classification }} ({{ singleForm.trackingType }})</span>
              </div>
              <div class="py-1 flex justify-between">
                <span class="text-muted-foreground">Location:</span>
                <span class="font-mono text-foreground">{{ singleForm.location }}</span>
              </div>
              <div class="py-1 flex justify-between">
                <span class="text-muted-foreground">Total Registration:</span>
                <span class="font-mono font-bold text-emerald-500">{{ singleForm.quantity }} unit(s)</span>
              </div>
            </div>

            <Button
              type="button"
              @click="handleSubmitSingle"
              :disabled="isSingleSubmitting || !isSingleFormValid"
              class="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-bold text-xs gap-2 py-2.5 shadow-md cursor-pointer"
            >
              <PackagePlus v-if="!isSingleSubmitting" class="w-4 h-4" />
              <RotateCcw v-else class="w-4 h-4 animate-spin" />
              <span>{{ isSingleSubmitting ? 'Ingesting Asset...' : 'Ingest Asset to Registry' }}</span>
            </Button>
          </div>
        </div>
      </div>
    </div>

    <!-- ===================================================================== -->
    <!-- 2. BATCH CSV / JSON IMPORT WORKBENCH                                  -->
    <!-- ===================================================================== -->
    <div v-else-if="activeMode === 'batch'" class="space-y-6">
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        <!-- Left: Upload & Raw Editor Pane (Col 7) -->
        <div class="lg:col-span-7 space-y-5">
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
                  <Upload class="w-4 h-4 text-primary" />
                  <span>Batch Ingestion Payload</span>
                </h3>
                <p class="text-xs text-muted-foreground mt-0.5">
                  Paste structured CSV/JSON data or drop your BOM / ERP export file
                </p>
              </div>

              <!-- Format Switcher & Load Sample -->
              <div class="flex items-center gap-2">
                <div class="flex items-center p-0.5 bg-muted/40 border border-border rounded-lg text-xs">
                  <button
                    type="button"
                    @click="batchFormat = 'json'"
                    class="px-2.5 py-1 rounded-md font-mono cursor-pointer transition-colors"
                    :class="batchFormat === 'json' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
                  >
                    JSON
                  </button>
                  <button
                    type="button"
                    @click="batchFormat = 'csv'"
                    class="px-2.5 py-1 rounded-md font-mono cursor-pointer transition-colors"
                    :class="batchFormat === 'csv' ? 'bg-background text-foreground shadow-xs font-bold' : 'text-muted-foreground hover:text-foreground'"
                  >
                    CSV
                  </button>
                </div>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  @click="loadSample"
                  class="h-7 text-xs gap-1 cursor-pointer font-medium"
                >
                  <Sparkles class="w-3.5 h-3.5 text-amber-500" />
                  <span>Load Sample</span>
                </Button>
              </div>
            </div>

            <!-- Drag & Drop Zone -->
            <label
              class="border-2 border-dashed border-border rounded-xl p-5 flex flex-col items-center justify-center gap-2 hover:border-primary/60 hover:bg-muted/10 transition-colors cursor-pointer text-center"
            >
              <Upload class="w-6 h-6 text-muted-foreground" />
              <div class="text-xs text-foreground font-semibold">
                Click to browse or drag and drop BOM file
              </div>
              <p class="text-[11px] text-muted-foreground font-mono">
                Accepts .json (array of parts) or .csv (comma separated with headers)
              </p>
              <input
                type="file"
                accept=".csv,.json"
                @change="handleFileUpload"
                class="hidden"
              />
            </label>

            <!-- Raw Textarea Editor -->
            <div class="space-y-1.5">
              <div class="flex items-center justify-between text-xs">
                <Label class="font-bold text-foreground">Raw Data Content</Label>
                <span class="text-muted-foreground font-mono text-[10px]">
                  Format: {{ batchFormat.toUpperCase() }}
                </span>
              </div>
              <textarea
                v-model="rawBatchContent"
                rows="14"
                placeholder="Paste JSON array or CSV records here..."
                class="w-full bg-background border border-border rounded-xl p-3 font-mono text-xs text-foreground focus:outline-hidden focus:border-primary resize-y"
              />
            </div>
          </div>
        </div>

        <!-- Right: Real-time Parser Validation & Preview Pane (Col 5) -->
        <div class="lg:col-span-5 space-y-5">
          <!-- Parsing Status Card -->
          <div class="p-5 rounded-2xl bg-card border border-border space-y-4 shadow-xs">
            <h3 class="text-sm font-bold text-foreground flex items-center justify-between">
              <span class="flex items-center gap-2">
                <CheckCircle2 v-if="!parsedBatchResult.error && parsedBatchResult.items.length > 0" class="w-4 h-4 text-emerald-500" />
                <AlertTriangle v-else-if="parsedBatchResult.error" class="w-4 h-4 text-rose-500" />
                <FileText v-else class="w-4 h-4 text-primary" />
                <span>Parser Diagnostics</span>
              </span>

              <Badge
                variant="outline"
                class="text-xs font-mono"
                :class="parsedBatchResult.error ? 'border-rose-500/40 text-rose-500' : 'border-emerald-500/40 text-emerald-500'"
              >
                {{ parsedBatchResult.error ? 'Syntax Error' : `${parsedBatchResult.items.length} Ready` }}
              </Badge>
            </h3>

            <!-- Error Banner -->
            <div v-if="parsedBatchResult.error" class="p-3 rounded-xl bg-rose-950/20 border border-rose-500/30 text-rose-400 text-xs">
              <p class="font-bold flex items-center gap-1.5">
                <AlertTriangle class="w-3.5 h-3.5" />
                <span>Parse Failure:</span>
              </p>
              <p class="font-mono text-[11px] mt-1 break-all">{{ parsedBatchResult.error }}</p>
            </div>

            <!-- Ready Summary -->
            <div v-else-if="parsedBatchResult.items.length > 0" class="p-3 rounded-xl bg-emerald-950/20 border border-emerald-500/30 text-emerald-400 text-xs space-y-1.5">
              <div class="flex items-center justify-between font-bold">
                <span>Payload Validated</span>
                <span class="font-mono">{{ parsedBatchResult.items.length }} Records Detected</span>
              </div>
              <p class="text-[11px] text-emerald-300/80">
                Ready to commit directly to master parts inventory and provision ledger.
              </p>
            </div>

            <div v-else class="p-6 text-center text-xs text-muted-foreground border border-dashed border-border rounded-xl">
              No data entered yet. Paste JSON/CSV content or click "Load Sample" to test parsing.
            </div>

            <!-- Commit Button -->
            <Button
              type="button"
              @click="handleSubmitBatch"
              :disabled="isBatchSubmitting || parsedBatchResult.items.length === 0 || !!parsedBatchResult.error"
              class="w-full bg-cyan-600 hover:bg-cyan-500 text-white font-bold text-xs gap-2 py-2.5 shadow-md cursor-pointer"
            >
              <Upload v-if="!isBatchSubmitting" class="w-4 h-4" />
              <RotateCcw v-else class="w-4 h-4 animate-spin" />
              <span>{{ isBatchSubmitting ? 'Importing Batch...' : `Import ${parsedBatchResult.items.length} Assets to Registry` }}</span>
            </Button>
          </div>

          <!-- Parsed Records Table Preview -->
          <div v-if="parsedBatchResult.items.length > 0" class="p-4 rounded-2xl bg-card border border-border space-y-3 shadow-xs">
            <h4 class="text-xs font-bold text-foreground uppercase tracking-wider flex items-center justify-between">
              <span>Preview Records (First 5)</span>
              <span class="font-mono text-[10px] text-muted-foreground">Showing 1-{{ Math.min(5, parsedBatchResult.items.length) }} of {{ parsedBatchResult.items.length }}</span>
            </h4>

            <div class="space-y-2 overflow-x-auto max-h-96">
              <div
                v-for="(item, idx) in parsedBatchResult.items.slice(0, 5)"
                :key="idx"
                class="p-2.5 rounded-lg bg-muted/20 border border-border text-xs space-y-1"
              >
                <div class="flex items-center justify-between">
                  <Badge variant="outline" class="font-mono text-[10px] text-primary">
                    {{ item.sku || item.customIdentifier || `REC-${idx + 1}` }}
                  </Badge>
                  <span class="font-mono text-[10px] font-bold text-foreground">
                    Qty: {{ item.quantity || 1 }}
                  </span>
                </div>
                <div class="font-bold text-foreground truncate">
                  {{ item.name || 'Unnamed Asset' }}
                </div>
                <div class="flex items-center justify-between text-[10px] text-muted-foreground font-mono pt-1 border-t border-border/40">
                  <span>{{ item.location || 'Central Crib' }}</span>
                  <span>{{ item.supplierName || item.manufacturer || 'OEM' }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
