<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { toast } from 'vue-sonner'
import {
  Layers,
  Cpu,
  Users,
  Activity,
  ShieldAlert,
  Boxes,
  Plus,
  Save,
  Trash2,
  Copy,
  GitBranch,
  Play,
  Download,
  Upload,
  Search,
  CheckCircle2,
  AlertTriangle,
  Code2,
  Sparkles,
  Sliders,
  Eye,
  RefreshCw,
  FolderTree,
  FileJson,
  X,
  Factory,
  Flame,
  Wrench
} from 'lucide-vue-next'
import TemplateSchemaBuilder from './TemplateSchemaBuilder.vue'
import TemplateFormRenderer from './TemplateFormRenderer.vue'
import { useGenericTemplates } from '~/composables/useGenericTemplates'
import type {
  GenericTemplate,
  GenericTemplateType,
  TemplateFieldDefinition,
  CreateGenericTemplateInput,
  DeriveTemplateInput
} from '~/types/templates'

const {
  templates,
  templateTypes,
  activeTemplateId,
  activeTemplate,
  effectiveResolution,
  isLoading,
  isSaving,
  error,
  selectedTypeFilter,
  selectedCategoryFilter,
  searchQuery,
  availableCategories,
  filteredTemplates,
  fetchTemplates,
  fetchTemplateTypes,
  selectTemplate,
  createTemplate,
  updateTemplate,
  deleteTemplate,
  deriveTemplate,
  instantiateTemplate,
  validateInstanceData,
  exportTemplateAsJson,
  parseImportedJson
} = useGenericTemplates()

// Editor view tabs
const designerTab = ref<'schema' | 'defaults' | 'meta' | 'rawJson'>('schema')
const sandboxTab = ref<'preview' | 'instantiate'>('preview')

// Working copy of currently active template
const localTemplate = ref<GenericTemplate | null>(null)
const rawJsonString = ref<string>('')
const rawJsonError = ref<string | null>(null)

// Sandbox test inputs & results
const sandboxValues = ref<Record<string, any>>({})
const sandboxResult = ref<any | null>(null)
const sandboxErrors = ref<string[]>([])
const isSimulating = ref(false)

// Modals
const showCreateModal = ref(false)
const showDeriveModal = ref(false)
const showImportModal = ref(false)
const importJsonText = ref('')
const importError = ref<string | null>(null)

// Create modal form
const newTemplateForm = ref<CreateGenericTemplateInput>({
  templateType: 'component',
  code: '',
  name: '',
  description: '',
  category: 'General',
  icon: 'Cpu',
  version: '1.0.0',
  tags: [],
  schema: [],
  defaultValues: {},
  isActive: true
})

// Derive modal form
const deriveForm = ref<DeriveTemplateInput>({
  newCode: '',
  newName: '',
  description: '',
  category: ''
})

// Tags input in metadata tab
const newTagInput = ref('')

const addTagToLocal = () => {
  const val = newTagInput.value.trim()
  if (!val || !localTemplate.value) return
  if (!localTemplate.value.tags) localTemplate.value.tags = []
  if (!localTemplate.value.tags.includes(val)) {
    localTemplate.value.tags.push(val)
  }
  newTagInput.value = ''
}

const removeTagFromLocal = (tag: string) => {
  if (!localTemplate.value || !localTemplate.value.tags) return
  localTemplate.value.tags = localTemplate.value.tags.filter(t => t !== tag)
}

// Sync local template when activeTemplate changes
watch(() => activeTemplate.value, (newActive) => {
  if (newActive) {
    localTemplate.value = JSON.parse(JSON.stringify(newActive))
    rawJsonString.value = JSON.stringify(newActive, null, 2)
    rawJsonError.value = null
    // Reset sandbox inputs to defaults
    sandboxValues.value = { ...(newActive.defaultValues || {}) }
    sandboxResult.value = null
    sandboxErrors.value = []
  } else {
    localTemplate.value = null
    rawJsonString.value = ''
  }
}, { immediate: true })

// Sync raw JSON when user edits JSON tab
const onRawJsonChange = () => {
  try {
    const parsed = JSON.parse(rawJsonString.value)
    rawJsonError.value = null
    if (localTemplate.value) {
      if (parsed.schema && Array.isArray(parsed.schema)) localTemplate.value.schema = parsed.schema
      if (parsed.defaultValues && typeof parsed.defaultValues === 'object') localTemplate.value.defaultValues = parsed.defaultValues
      if (parsed.name) localTemplate.value.name = parsed.name
      if (parsed.description) localTemplate.value.description = parsed.description
    }
  } catch (err: any) {
    rawJsonError.value = err.message
  }
}

// Save active template
const saveCurrentTemplate = async () => {
  if (!localTemplate.value) return
  try {
    await updateTemplate(localTemplate.value.id, {
      code: localTemplate.value.code,
      name: localTemplate.value.name,
      description: localTemplate.value.description,
      category: localTemplate.value.category,
      icon: localTemplate.value.icon,
      version: localTemplate.value.version,
      extendsTemplateId: localTemplate.value.extendsTemplateId,
      tags: localTemplate.value.tags,
      schema: localTemplate.value.schema,
      defaultValues: localTemplate.value.defaultValues,
      uiConfig: localTemplate.value.uiConfig,
      isActive: localTemplate.value.isActive
    })
    toast.success('Template saved successfully')
  } catch (e: any) {
    toast.error(e?.message || 'Failed to save template')
  }
}

// Handle schema update from SchemaBuilder
const handleSchemaUpdate = (newSchema: TemplateFieldDefinition[]) => {
  if (!localTemplate.value) return
  localTemplate.value.schema = newSchema
  rawJsonString.value = JSON.stringify(localTemplate.value, null, 2)
}

// Run test simulation / instantiation
const runTestInstantiation = async () => {
  if (!localTemplate.value) return
  isSimulating.value = true
  sandboxErrors.value = []
  try {
    const res = await instantiateTemplate(localTemplate.value.id, sandboxValues.value)
    sandboxResult.value = res.instanceData
    if (!res.success && res.errors?.length) {
      sandboxErrors.value = res.errors
      toast.warning('Instantiation test completed with validation warnings')
    } else {
      toast.success('Instantiation test executed successfully')
    }
  } catch (err: any) {
    sandboxErrors.value = [err.message]
    toast.error('Simulation failed')
  } finally {
    isSimulating.value = false
  }
}

// Create new template action
const openCreateDialog = (type: string = 'component') => {
  newTemplateForm.value = {
    templateType: type,
    code: `TPL-${type.toUpperCase().slice(0, 3)}-${Date.now().toString().slice(-4)}`,
    name: `New ${type.charAt(0).toUpperCase() + type.slice(1)} Template`,
    description: '',
    category: 'General',
    icon: type === 'component' ? 'Cpu' : type === 'delegation' ? 'Users' : type === 'telemetry' ? 'Activity' : 'Layers',
    version: '1.0.0',
    tags: [type],
    schema: [],
    defaultValues: {},
    isActive: true
  }
  showCreateModal.value = true
}

const submitCreateTemplate = async () => {
  if (!newTemplateForm.value.code || !newTemplateForm.value.name) {
    toast.error('Code and Name are required.')
    return
  }
  try {
    const created = await createTemplate(newTemplateForm.value)
    showCreateModal.value = false
    toast.success(`Template ${created.code} created`)
  } catch (err: any) {
    toast.error(err.message || 'Creation failed')
  }
}

// Derive child template action
const openDeriveDialog = () => {
  if (!localTemplate.value) return
  deriveForm.value = {
    newCode: `${localTemplate.value.code}-EXT`,
    newName: `${localTemplate.value.name} (Specialized)`,
    description: `Specialized child blueprint derived from ${localTemplate.value.name}`,
    category: localTemplate.value.category
  }
  showDeriveModal.value = true
}

const submitDeriveTemplate = async () => {
  if (!localTemplate.value) return
  try {
    const derived = await deriveTemplate(localTemplate.value.id, deriveForm.value)
    showDeriveModal.value = false
    toast.success(`Derived child template ${derived.code} created`)
  } catch (err: any) {
    toast.error(err.message || 'Derive failed')
  }
}

// Delete action
const handleDeleteCurrent = async () => {
  if (!localTemplate.value) return
  if (!confirm(`Are you sure you want to delete template "${localTemplate.value.name}" (${localTemplate.value.code})?`)) {
    return
  }
  const ok = await deleteTemplate(localTemplate.value.id)
  if (ok) {
    toast.success('Template deleted')
  }
}

// Export / Import
const handleExport = () => {
  if (!localTemplate.value) return
  const jsonStr = exportTemplateAsJson(localTemplate.value)
  const blob = new Blob([jsonStr], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${localTemplate.value.code.toLowerCase()}-template.json`
  a.click()
  URL.revokeObjectURL(url)
  toast.success('Template JSON exported')
}

const submitImportJson = async () => {
  importError.value = null
  try {
    const parsed = parseImportedJson(importJsonText.value)
    await createTemplate(parsed)
    showImportModal.value = false
    importJsonText.value = ''
    toast.success('Template imported successfully')
  } catch (err: any) {
    importError.value = err.message || 'Failed to parse JSON'
  }
}

// Icon helper
const getIcon = (iconName?: string) => {
  switch (iconName?.toLowerCase()) {
    case 'factory': return Factory
    case 'cpu': return Cpu
    case 'users': return Users
    case 'activity': return Activity
    case 'shieldalert': return ShieldAlert
    case 'boxes': return Boxes
    case 'flame': return Flame
    case 'wrench': return Wrench
    case 'eye': return Eye
    default: return Layers
  }
}

onMounted(async () => {
  await fetchTemplateTypes()
  await fetchTemplates()
})
</script>

<template>
  <div class="h-full flex flex-col bg-background text-foreground">
    <!-- Top Action Ribbon -->
    <header class="border-b bg-card/60 backdrop-blur-xs px-4 py-2.5 flex items-center justify-between gap-3 shrink-0">
      <div class="flex items-center gap-3">
        <div class="w-9 h-9 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary">
          <Layers class="w-5 h-5" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <h1 class="text-sm font-bold tracking-tight text-foreground">Universal Recipe & Template Studio</h1>
            <Badge variant="outline" class="text-[10px] px-1.5 py-0 bg-primary/5 text-primary border-primary/20">
              Generic Engine v2.0
            </Badge>
          </div>
          <p class="text-[11px] text-muted-foreground">
            Standardized blueprint design, prototype inheritance, and dynamic schema composition
          </p>
        </div>
      </div>

      <!-- Action Buttons -->
      <div class="flex items-center gap-2">
        <Button
          type="button"
          size="sm"
          variant="outline"
          @click="showImportModal = true"
          class="h-8 text-xs gap-1 cursor-pointer"
        >
          <Upload class="w-3.5 h-3.5" />
          <span class="hidden sm:inline">Import</span>
        </Button>

        <Button
          v-if="localTemplate"
          type="button"
          size="sm"
          variant="outline"
          @click="handleExport"
          class="h-8 text-xs gap-1 cursor-pointer"
        >
          <Download class="w-3.5 h-3.5" />
          <span class="hidden sm:inline">Export</span>
        </Button>

        <Button
          v-if="localTemplate"
          type="button"
          size="sm"
          variant="outline"
          @click="openDeriveDialog"
          class="h-8 text-xs gap-1 cursor-pointer text-blue-600 dark:text-blue-400 border-blue-500/30 hover:bg-blue-500/10"
          title="Create child template inheriting from this one"
        >
          <GitBranch class="w-3.5 h-3.5" />
          <span class="hidden sm:inline">Derive Child</span>
        </Button>

        <Button
          type="button"
          size="sm"
          variant="default"
          @click="openCreateDialog()"
          class="h-8 text-xs gap-1 cursor-pointer"
        >
          <Plus class="w-3.5 h-3.5" />
          <span>New Blueprint</span>
        </Button>

        <Button
          v-if="localTemplate"
          type="button"
          size="sm"
          variant="default"
          :disabled="isSaving"
          @click="saveCurrentTemplate"
          class="h-8 text-xs gap-1 cursor-pointer bg-emerald-600 hover:bg-emerald-700 text-white"
        >
          <Save class="w-3.5 h-3.5" />
          <span>{{ isSaving ? 'Saving...' : 'Save Specs' }}</span>
        </Button>
      </div>
    </header>

    <!-- 3-Panel Studio Layout -->
    <div class="flex-1 grid grid-cols-12 min-h-0 overflow-hidden divide-x">
      <!-- PANEL 1: Left Library / Navigator (Col 3) -->
      <aside class="col-span-12 lg:col-span-3 flex flex-col bg-muted/10 min-h-0 overflow-hidden">
        <!-- Filter Tabs -->
        <div class="p-3 border-b space-y-2 bg-card/40">
          <!-- Search -->
          <div class="relative">
            <Search class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground" />
            <Input
              v-model="searchQuery"
              placeholder="Search recipes & templates..."
              class="h-7.5 pl-8 text-xs bg-background"
            />
          </div>

          <!-- Type Filter Pills -->
          <div class="flex items-center gap-1 overflow-x-auto pb-1 text-[11px] scrollbar-none">
            <button
              type="button"
              @click="selectedTypeFilter = 'all'"
              class="px-2 py-0.5 rounded-full border transition-colors shrink-0 cursor-pointer"
              :class="selectedTypeFilter === 'all' ? 'bg-primary text-primary-foreground border-primary font-medium' : 'bg-background text-muted-foreground hover:bg-muted'"
            >
              All ({{ templates.length }})
            </button>
            <button
              v-for="tt in templateTypes"
              :key="tt.type"
              type="button"
              @click="selectedTypeFilter = tt.type"
              class="px-2 py-0.5 rounded-full border transition-colors shrink-0 cursor-pointer flex items-center gap-1"
              :class="selectedTypeFilter === tt.type ? 'bg-primary text-primary-foreground border-primary font-medium' : 'bg-background text-muted-foreground hover:bg-muted'"
            >
              <component :is="getIcon(tt.icon)" class="w-3 h-3" />
              <span>{{ tt.label.split(' ')[0] }}</span>
            </button>
          </div>
        </div>

        <!-- Template Cards List -->
        <div class="flex-1 overflow-y-auto p-2 space-y-1.5">
          <div
            v-if="isLoading"
            class="p-6 text-center text-xs text-muted-foreground"
          >
            <RefreshCw class="w-4 h-4 animate-spin mx-auto mb-2 opacity-50" />
            Loading template catalog...
          </div>

          <div
            v-else-if="filteredTemplates.length === 0"
            class="p-6 text-center text-xs text-muted-foreground border rounded-lg border-dashed m-2"
          >
            No templates match current filters.
          </div>

          <div
            v-for="t in filteredTemplates"
            :key="t.id"
            @click="selectTemplate(t)"
            class="group p-2.5 rounded-lg border bg-card hover:border-primary/50 transition-all cursor-pointer relative"
            :class="{ 'ring-1 ring-primary border-primary bg-primary/5': activeTemplateId === t.id }"
          >
            <div class="flex items-start justify-between gap-2">
              <div class="flex items-start gap-2 min-w-0">
                <div
                  class="w-7 h-7 rounded-md flex items-center justify-center shrink-0 border mt-0.5"
                  :class="activeTemplateId === t.id ? 'bg-primary text-primary-foreground border-primary' : 'bg-muted/40 text-muted-foreground group-hover:text-primary'"
                >
                  <component :is="getIcon(t.icon)" class="w-3.5 h-3.5" />
                </div>
                <div class="min-w-0">
                  <div class="flex items-center gap-1.5 truncate">
                    <span class="text-xs font-semibold text-foreground truncate">{{ t.name }}</span>
                  </div>
                  <div class="flex items-center gap-1.5 mt-0.5">
                    <span class="text-[10px] font-mono text-muted-foreground bg-muted/60 px-1 py-0.2 rounded">{{ t.code }}</span>
                    <span class="text-[10px] text-muted-foreground">&bull; {{ t.category }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Footer Meta -->
            <div class="flex items-center justify-between gap-1 mt-2 pt-1.5 border-t border-border/40 text-[10px] text-muted-foreground">
              <div class="flex items-center gap-1 truncate">
                <Badge variant="outline" class="text-[9px] px-1 py-0 font-mono uppercase">
                  {{ t.templateType }}
                </Badge>
                <span v-if="t.extendsTemplateCode" class="text-blue-500 font-mono text-[9px] truncate">
                  &larr; {{ t.extendsTemplateCode }}
                </span>
              </div>
              <span class="font-mono text-[10px]">
                {{ (t.schema || []).length }} specs
              </span>
            </div>
          </div>
        </div>
      </aside>

      <!-- PANEL 2: Center Visual Designer (Col 5) -->
      <main class="col-span-12 lg:col-span-5 flex flex-col min-h-0 overflow-hidden bg-background">
        <div v-if="!localTemplate" class="flex-1 flex items-center justify-center p-8 text-center text-muted-foreground">
          <div>
            <Layers class="w-10 h-10 mx-auto mb-2 opacity-30" />
            <p class="text-sm font-medium">Select a template from the left or create a new one</p>
          </div>
        </div>

        <div v-else class="flex-1 flex flex-col min-h-0 overflow-hidden">
          <!-- Template Header Ribbon -->
          <div class="p-3.5 border-b bg-muted/15 space-y-2 shrink-0">
            <div class="flex items-start justify-between gap-2">
              <div class="flex items-start gap-2.5 min-w-0">
                <div class="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary shrink-0">
                  <component :is="getIcon(localTemplate.icon)" class="w-4 h-4" />
                </div>
                <div class="min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <h2 class="text-sm font-bold text-foreground truncate">{{ localTemplate.name }}</h2>
                    <Badge variant="outline" class="text-[10px] font-mono px-1.5 py-0">
                      {{ localTemplate.code }}
                    </Badge>
                    <Badge variant="secondary" class="text-[10px] px-1.5 py-0 uppercase">
                      {{ localTemplate.templateType }}
                    </Badge>
                  </div>
                  <p v-if="localTemplate.description" class="text-xs text-muted-foreground line-clamp-1 mt-0.5">
                    {{ localTemplate.description }}
                  </p>
                </div>
              </div>

              <!-- Delete button -->
              <Button
                type="button"
                variant="ghost"
                size="sm"
                @click="handleDeleteCurrent"
                class="h-7 text-xs text-rose-500 hover:text-rose-600 hover:bg-rose-500/10 px-2 cursor-pointer"
                title="Delete template"
              >
                <Trash2 class="w-3.5 h-3.5" />
              </Button>
            </div>

            <!-- Inheritance Lineage Trail -->
            <div
              v-if="effectiveResolution && effectiveResolution.lineage.length > 1"
              class="flex items-center gap-1 text-[11px] bg-blue-500/10 border border-blue-500/20 px-2.5 py-1 rounded-md text-blue-700 dark:text-blue-300 overflow-x-auto"
            >
              <GitBranch class="w-3.5 h-3.5 shrink-0 text-blue-500" />
              <span class="font-semibold shrink-0">Lineage:</span>
              <div class="flex items-center gap-1 font-mono text-[10px]">
                <template v-for="(item, idx) in effectiveResolution.lineage" :key="item.id">
                  <span :class="item.id === localTemplate.id ? 'font-bold underline' : 'opacity-80'">
                    {{ item.code }}
                  </span>
                  <span v-if="idx < effectiveResolution.lineage.length - 1" class="opacity-50">&rarr;</span>
                </template>
              </div>
            </div>

            <!-- Designer Navigation Tabs -->
            <div class="flex items-center gap-1 border-b pt-1 text-xs">
              <button
                type="button"
                @click="designerTab = 'schema'"
                class="px-3 py-1.5 border-b-2 font-medium transition-colors cursor-pointer flex items-center gap-1.5"
                :class="designerTab === 'schema' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
              >
                <Sliders class="w-3.5 h-3.5" />
                <span>Schema Designer ({{ (localTemplate.schema || []).length }})</span>
              </button>

              <button
                type="button"
                @click="designerTab = 'defaults'"
                class="px-3 py-1.5 border-b-2 font-medium transition-colors cursor-pointer flex items-center gap-1.5"
                :class="designerTab === 'defaults' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
              >
                <Sparkles class="w-3.5 h-3.5" />
                <span>Default Values</span>
              </button>

              <button
                type="button"
                @click="designerTab = 'meta'"
                class="px-3 py-1.5 border-b-2 font-medium transition-colors cursor-pointer flex items-center gap-1.5"
                :class="designerTab === 'meta' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
              >
                <FolderTree class="w-3.5 h-3.5" />
                <span>Metadata & Lineage</span>
              </button>

              <button
                type="button"
                @click="designerTab = 'rawJson'"
                class="px-3 py-1.5 border-b-2 font-medium transition-colors cursor-pointer flex items-center gap-1.5"
                :class="designerTab === 'rawJson' ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground'"
              >
                <FileJson class="w-3.5 h-3.5" />
                <span>JSON Schema</span>
              </button>
            </div>
          </div>

          <!-- Designer Tab Content -->
          <div class="flex-1 overflow-y-auto p-4">
            <!-- TAB 1: SCHEMA BUILDER -->
            <div v-if="designerTab === 'schema'">
              <TemplateSchemaBuilder
                :schema="localTemplate.schema || []"
                @update:schema="handleSchemaUpdate"
              />
            </div>

            <!-- TAB 2: DEFAULT VALUES -->
            <div v-else-if="designerTab === 'defaults'" class="space-y-3">
              <div class="flex items-center justify-between pb-2 border-b">
                <div>
                  <h4 class="text-xs font-semibold text-foreground uppercase tracking-wider">
                    Default Values Configuration
                  </h4>
                  <p class="text-[11px] text-muted-foreground">
                    Define default parameters for newly created instances of this recipe.
                  </p>
                </div>
              </div>

              <!-- Form Renderer for Defaults -->
              <TemplateFormRenderer
                :schema="effectiveResolution?.effectiveSchema || localTemplate.schema || []"
                v-model="localTemplate.defaultValues"
                :show-summary="false"
              />
            </div>

            <!-- TAB 3: METADATA & LINEAGE -->
            <div v-else-if="designerTab === 'meta'" class="space-y-4">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div class="space-y-1">
                  <Label class="text-xs font-medium">Blueprint Code</Label>
                  <Input v-model="localTemplate.code" class="h-8 text-xs font-mono" />
                </div>
                <div class="space-y-1">
                  <Label class="text-xs font-medium">Display Name</Label>
                  <Input v-model="localTemplate.name" class="h-8 text-xs" />
                </div>
                <div class="space-y-1">
                  <Label class="text-xs font-medium">Category</Label>
                  <Input v-model="localTemplate.category" class="h-8 text-xs" />
                </div>
                <div class="space-y-1">
                  <Label class="text-xs font-medium">Version</Label>
                  <Input v-model="localTemplate.version" class="h-8 text-xs font-mono" />
                </div>
                <div class="space-y-1 sm:col-span-2">
                  <Label class="text-xs font-medium">Parent Template (Single Base Lineage)</Label>
                  <select
                    v-model="localTemplate.extendsTemplateId"
                    class="w-full h-8 px-2.5 text-xs rounded-md border border-input bg-background"
                  >
                    <option :value="null">None (Standalone Root Blueprint)</option>
                    <option
                      v-for="p in templates.filter(t => t.id !== localTemplate?.id && t.templateType === localTemplate?.templateType)"
                      :key="p.id"
                      :value="p.id"
                    >
                      {{ p.name }} ({{ p.code }})
                    </option>
                  </select>
                  <p class="text-[10px] text-muted-foreground mt-0.5">
                    Child blueprints inherit all schema specifications and default values from their parent lineage.
                  </p>
                </div>
                <div class="space-y-1 sm:col-span-2">
                  <Label class="text-xs font-medium">Detailed Description</Label>
                  <textarea
                    v-model="localTemplate.description"
                    rows="3"
                    class="w-full rounded-md border border-input bg-background p-2 text-xs"
                  />
                </div>
              </div>

              <!-- Tags -->
              <div class="space-y-1.5 pt-2 border-t">
                <Label class="text-xs font-medium">Classification Tags</Label>
                <div class="flex flex-wrap gap-1 items-center min-h-8 p-1.5 rounded border border-input bg-background">
                  <span
                    v-for="tag in (localTemplate.tags || [])"
                    :key="tag"
                    class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-xs bg-secondary text-secondary-foreground font-mono"
                  >
                    <span>#{{ tag }}</span>
                    <button type="button" @click="removeTagFromLocal(tag)" class="hover:text-rose-500">
                      <X class="w-2.5 h-2.5" />
                    </button>
                  </span>
                  <input
                    v-model="newTagInput"
                    @keydown.enter.prevent="addTagToLocal"
                    @keydown.,.prevent="addTagToLocal"
                    placeholder="Add tag and hit Enter..."
                    class="flex-1 min-w-[120px] bg-transparent text-xs outline-none px-1"
                  />
                </div>
              </div>
            </div>

            <!-- TAB 4: RAW JSON SCHEMA -->
            <div v-else-if="designerTab === 'rawJson'" class="space-y-2 h-full flex flex-col">
              <div class="flex items-center justify-between">
                <span class="text-xs font-semibold">Raw JSON Template Definition</span>
                <span v-if="rawJsonError" class="text-rose-500 text-xs font-mono">{{ rawJsonError }}</span>
              </div>
              <textarea
                v-model="rawJsonString"
                @input="onRawJsonChange"
                rows="18"
                class="w-full flex-1 rounded-md border border-input bg-zinc-950 text-zinc-100 dark:bg-black font-mono p-3 text-xs leading-relaxed outline-none focus:ring-1 focus:ring-primary"
              />
            </div>
          </div>
        </div>
      </main>

      <!-- PANEL 3: Right Live Preview & Test Sandbox (Col 4) -->
      <aside class="col-span-12 lg:col-span-4 flex flex-col bg-muted/10 min-h-0 overflow-hidden">
        <!-- Sandbox Header Ribbon -->
        <div class="p-3 border-b bg-card/40 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-2">
            <Eye class="w-4 h-4 text-primary" />
            <span class="text-xs font-bold uppercase tracking-wider text-foreground">Interactive Sandbox</span>
          </div>

          <!-- Sub-tabs -->
          <div class="flex items-center gap-1 bg-muted/50 p-0.5 rounded-md text-[11px]">
            <button
              type="button"
              @click="sandboxTab = 'preview'"
              class="px-2 py-0.5 rounded transition-colors cursor-pointer"
              :class="sandboxTab === 'preview' ? 'bg-background shadow-xs text-foreground font-medium' : 'text-muted-foreground'"
            >
              Live UI Form
            </button>
            <button
              type="button"
              @click="sandboxTab = 'instantiate'"
              class="px-2 py-0.5 rounded transition-colors cursor-pointer"
              :class="sandboxTab === 'instantiate' ? 'bg-background shadow-xs text-foreground font-medium' : 'text-muted-foreground'"
            >
              Test Instantiate
            </button>
          </div>
        </div>

        <div v-if="!localTemplate" class="flex-1 flex items-center justify-center p-6 text-center text-xs text-muted-foreground">
          No template selected for preview.
        </div>

        <!-- Sandbox Body -->
        <div v-else class="flex-1 overflow-y-auto p-3.5 space-y-4">
          <!-- SUB-TAB 1: LIVE UI FORM PREVIEW -->
          <div v-if="sandboxTab === 'preview'" class="space-y-3">
            <div class="p-2.5 rounded bg-muted/30 border text-[11px] text-muted-foreground flex items-center justify-between">
              <span>This renders how end users see and fill this template across the plant:</span>
              <Badge variant="outline" class="text-[9px] font-mono">
                {{ (effectiveResolution?.effectiveSchema || localTemplate.schema || []).length }} specs
              </Badge>
            </div>

            <!-- The Live Template Form Renderer -->
            <TemplateFormRenderer
              :schema="effectiveResolution?.effectiveSchema || localTemplate.schema || []"
              v-model="sandboxValues"
            />
          </div>

          <!-- SUB-TAB 2: TEST INSTANTIATION -->
          <div v-else-if="sandboxTab === 'instantiate'" class="space-y-3">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold">Instance Evaluation Test</span>
              <Button
                type="button"
                size="sm"
                variant="default"
                :disabled="isSimulating"
                @click="runTestInstantiation"
                class="h-7 text-xs gap-1.5 cursor-pointer bg-blue-600 hover:bg-blue-700 text-white"
              >
                <Play class="w-3 h-3 fill-current" />
                <span>{{ isSimulating ? 'Evaluating...' : 'Run Instantiation' }}</span>
              </Button>
            </div>

            <!-- Errors alert -->
            <div
              v-if="sandboxErrors.length > 0"
              class="p-2.5 rounded border border-rose-500/30 bg-rose-500/10 text-rose-600 dark:text-rose-400 text-xs space-y-1"
            >
              <div class="flex items-center gap-1.5 font-bold">
                <AlertTriangle class="w-3.5 h-3.5" />
                <span>Validation Errors ({{ sandboxErrors.length }})</span>
              </div>
              <ul class="list-disc list-inside text-[11px] space-y-0.5">
                <li v-for="(err, i) in sandboxErrors" :key="i">{{ err }}</li>
              </ul>
            </div>

            <!-- Result output -->
            <div v-if="sandboxResult" class="space-y-1.5">
              <div class="flex items-center justify-between text-xs">
                <span class="font-semibold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                  <CheckCircle2 class="w-3.5 h-3.5" />
                  Instantiated Payload
                </span>
                <span class="text-[10px] text-muted-foreground font-mono">Dynamic tokens evaluated</span>
              </div>
              <pre class="p-3 rounded-lg bg-zinc-950 text-emerald-400 dark:bg-black font-mono text-[11px] overflow-x-auto border border-zinc-800 leading-relaxed">{{ JSON.stringify(sandboxResult, null, 2) }}</pre>
            </div>
          </div>
        </div>
      </aside>
    </div>

    <!-- MODAL 1: Create New Template -->
    <div
      v-if="showCreateModal"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4"
    >
      <div class="bg-card text-card-foreground border rounded-xl shadow-xl w-full max-w-md p-5 space-y-4">
        <div class="flex items-center justify-between pb-2 border-b">
          <h3 class="text-sm font-bold">Create New Recipe / Template</h3>
          <button @click="showCreateModal = false" class="text-muted-foreground hover:text-foreground">
            <X class="w-4 h-4" />
          </button>
        </div>

        <div class="space-y-3">
          <div class="space-y-1">
            <Label class="text-xs font-medium">Domain / Type</Label>
            <select
              v-model="newTemplateForm.templateType"
              class="w-full h-8 px-2.5 text-xs rounded border border-input bg-background"
            >
              <option value="machine">Machine & Production Station</option>
              <option value="component">Machine Component Recipe</option>
              <option value="delegation">Technician Delegation Recipe</option>
              <option value="telemetry">Telemetry & IoT Probe</option>
              <option value="ticket">Error & Maintenance Ticket</option>
              <option value="asset">Inventory Asset & Spare Part</option>
              <option value="custom">Custom Universal Blueprint</option>
            </select>
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Unique Code</Label>
            <Input v-model="newTemplateForm.code" placeholder="e.g. CMP-SERVO-02" class="h-8 text-xs font-mono" />
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Template Name</Label>
            <Input v-model="newTemplateForm.name" placeholder="e.g. Multi-Axis Dynamic Servo" class="h-8 text-xs" />
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Category</Label>
            <Input v-model="newTemplateForm.category" placeholder="e.g. Motion" class="h-8 text-xs" />
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Description</Label>
            <textarea v-model="newTemplateForm.description" rows="2" class="w-full rounded border border-input bg-background p-2 text-xs" />
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t">
          <Button variant="outline" size="sm" @click="showCreateModal = false">Cancel</Button>
          <Button size="sm" @click="submitCreateTemplate">Create Blueprint</Button>
        </div>
      </div>
    </div>

    <!-- MODAL 2: Derive Child Template -->
    <div
      v-if="showDeriveModal"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4"
    >
      <div class="bg-card text-card-foreground border rounded-xl shadow-xl w-full max-w-md p-5 space-y-4">
        <div class="flex items-center justify-between pb-2 border-b">
          <div class="flex items-center gap-2">
            <GitBranch class="w-4 h-4 text-blue-500" />
            <h3 class="text-sm font-bold">Derive Child Blueprint</h3>
          </div>
          <button @click="showDeriveModal = false" class="text-muted-foreground hover:text-foreground">
            <X class="w-4 h-4" />
          </button>
        </div>

        <p class="text-xs text-muted-foreground">
          Deriving a child blueprint establishes single-lineage prototype inheritance from
          <strong class="text-foreground">{{ localTemplate?.name }}</strong>. All parent specifications are inherited and can be overridden or extended.
        </p>

        <div class="space-y-3">
          <div class="space-y-1">
            <Label class="text-xs font-medium">Child Blueprint Code</Label>
            <Input v-model="deriveForm.newCode" placeholder="e.g. CMP-SERVO-01-SMART" class="h-8 text-xs font-mono" />
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Child Name</Label>
            <Input v-model="deriveForm.newName" placeholder="e.g. Smart Multi-Axis Servo" class="h-8 text-xs" />
          </div>

          <div class="space-y-1">
            <Label class="text-xs font-medium">Description</Label>
            <textarea v-model="deriveForm.description" rows="2" class="w-full rounded border border-input bg-background p-2 text-xs" />
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t">
          <Button variant="outline" size="sm" @click="showDeriveModal = false">Cancel</Button>
          <Button size="sm" class="bg-blue-600 hover:bg-blue-700 text-white" @click="submitDeriveTemplate">Derive Blueprint</Button>
        </div>
      </div>
    </div>

    <!-- MODAL 3: Import JSON -->
    <div
      v-if="showImportModal"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4"
    >
      <div class="bg-card text-card-foreground border rounded-xl shadow-xl w-full max-w-lg p-5 space-y-4">
        <div class="flex items-center justify-between pb-2 border-b">
          <h3 class="text-sm font-bold">Import Template from JSON</h3>
          <button @click="showImportModal = false" class="text-muted-foreground hover:text-foreground">
            <X class="w-4 h-4" />
          </button>
        </div>

        <div class="space-y-2">
          <p class="text-xs text-muted-foreground">
            Paste a valid Heimdall Universal Template JSON payload below:
          </p>
          <textarea
            v-model="importJsonText"
            rows="10"
            placeholder='{&#10;  "code": "CMP-CUSTOM-01",&#10;  "name": "Custom Actuator",&#10;  "schema": []&#10;}'
            class="w-full rounded border border-input bg-zinc-950 text-zinc-100 dark:bg-black font-mono p-2 text-xs"
          />
          <p v-if="importError" class="text-xs text-rose-500 font-mono">{{ importError }}</p>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t">
          <Button variant="outline" size="sm" @click="showImportModal = false">Cancel</Button>
          <Button size="sm" @click="submitImportJson">Import Template</Button>
        </div>
      </div>
    </div>
  </div>
</template>
