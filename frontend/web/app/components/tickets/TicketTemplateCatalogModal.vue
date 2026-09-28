<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  FileText, Search, Plus, Filter, Tag, Cpu, Activity,
  Check, ArrowRight, ShieldAlert, Sparkles, Wrench, X, RefreshCw
} from 'lucide-vue-next'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import {
  ERROR_TEMPLATES, CATEGORIES, MACHINE_TYPES,
  type ErrorTemplate
} from '~/utils/errorTemplateEngine'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'select', template: ErrorTemplate): void
}>()

const isOpen = computed({
  get: () => props.open,
  set: (v) => { if (!v) emit('close') }
})

const activeCategory = ref<string>('All')
const selectedMachineType = ref<string>('All')
const searchQuery = ref('')
const isLoading = ref(false)
const templates = ref<ErrorTemplate[]>([...ERROR_TEMPLATES])

// Custom template creation state
const showCreateForm = ref(false)
const isSubmittingCustom = ref(false)
const customForm = ref({
  errorCode: '',
  category: 'Error' as 'Prevention' | 'Error' | 'Improvement' | 'ETC',
  errorGroup: '',
  shortDescription: '',
  detailedDescription: '',
  targetKanbanState: 'Open',
  defaultTags: '',
  machineTypes: [] as string[],
  fbBlockName: '',
  fbState: '',
  fbErrorCode: ''
})

async function fetchTemplates() {
  isLoading.value = true
  try {
    const params = new URLSearchParams()
    if (activeCategory.value !== 'All') params.set('category', activeCategory.value)
    if (selectedMachineType.value !== 'All') params.set('machineType', selectedMachineType.value)
    if (searchQuery.value.trim()) params.set('search', searchQuery.value.trim())

    const res = await $fetch<{ templates: ErrorTemplate[] }>(`/api/tickets/templates?${params.toString()}`)
    if (res && res.templates) {
      templates.value = res.templates
    }
  } catch {
    // Fallback to local filtering
    let local = [...ERROR_TEMPLATES]
    if (activeCategory.value !== 'All') {
      local = local.filter(t => t.category.toLowerCase() === activeCategory.value.toLowerCase())
    }
    if (selectedMachineType.value !== 'All') {
      local = local.filter(t => t.affectedMachineTypes?.includes(selectedMachineType.value))
    }
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.toLowerCase().trim()
      local = local.filter(t =>
        t.errorCode.toLowerCase().includes(q) ||
        t.shortDescription.toLowerCase().includes(q) ||
        t.detailedDescription.toLowerCase().includes(q) ||
        t.defaultTags.some(tag => tag.toLowerCase().includes(q))
      )
    }
    templates.value = local
  } finally {
    isLoading.value = false
  }
}

watch([activeCategory, selectedMachineType], () => {
  fetchTemplates()
})

let debounceTimer: any = null
watch(searchQuery, () => {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    fetchTemplates()
  }, 250)
})

onMounted(() => {
  fetchTemplates()
})

function onSelectTemplate(tmpl: ErrorTemplate) {
  emit('select', tmpl)
  emit('close')
}

async function handleCreateCustomTemplate() {
  if (!customForm.value.errorCode.trim() || !customForm.value.shortDescription.trim()) return

  isSubmittingCustom.value = true
  try {
    const tags = customForm.value.defaultTags
      .split(',')
      .map(t => t.trim())
      .filter(Boolean)
      .map(t => (t.startsWith('#') ? t : `#${t}`))

    const newTmpl: Partial<ErrorTemplate> = {
      id: customForm.value.errorCode.trim(),
      errorCode: customForm.value.errorCode.trim(),
      category: customForm.value.category,
      errorGroup: customForm.value.errorGroup.trim() || 'Custom',
      shortDescription: customForm.value.shortDescription.trim(),
      detailedDescription: customForm.value.detailedDescription.trim(),
      targetKanbanState: customForm.value.targetKanbanState as any,
      defaultTags: tags,
      affectedMachineTypes: customForm.value.machineTypes.length > 0 ? customForm.value.machineTypes : undefined,
      sampleFbState: customForm.value.fbBlockName ? {
        blockName: customForm.value.fbBlockName.trim(),
        state: customForm.value.fbState.trim() || 'ERROR',
        errorCode: customForm.value.fbErrorCode.trim() || undefined
      } : undefined
    }

    const res = await $fetch<{ success: boolean; template: ErrorTemplate }>('/api/tickets/templates', {
      method: 'POST',
      body: newTmpl
    })

    if (res?.template) {
      templates.value.unshift(res.template)
      showCreateForm.value = false
      resetCustomForm()
    }
  } catch (e) {
    console.error('Failed to create template', e)
  } finally {
    isSubmittingCustom.value = false
  }
}

function resetCustomForm() {
  customForm.value = {
    errorCode: '',
    category: 'Error',
    errorGroup: '',
    shortDescription: '',
    detailedDescription: '',
    targetKanbanState: 'Open',
    defaultTags: '',
    machineTypes: [],
    fbBlockName: '',
    fbState: '',
    fbErrorCode: ''
  }
}

function toggleMachineTypeSelection(mt: string) {
  const idx = customForm.value.machineTypes.indexOf(mt)
  if (idx >= 0) {
    customForm.value.machineTypes.splice(idx, 1)
  } else {
    customForm.value.machineTypes.push(mt)
  }
}

function getCategoryColor(cat: string): string {
  switch (cat.toLowerCase()) {
    case 'error': return 'text-rose-500 bg-rose-500/10 border-rose-500/30'
    case 'prevention': return 'text-amber-500 bg-amber-500/10 border-amber-500/30'
    case 'improvement': return 'text-emerald-500 bg-emerald-500/10 border-emerald-500/30'
    case 'etc': return 'text-blue-500 bg-blue-500/10 border-blue-500/30'
    default: return 'text-muted-foreground bg-muted border-border'
  }
}
</script>

<template>
  <Dialog v-model:open="isOpen">
    <DialogContent class="max-w-4xl max-h-[88vh] flex flex-col p-0 gap-0 overflow-hidden bg-background text-foreground border-border shadow-2xl">
      <!-- Modal Header -->
      <DialogHeader class="p-6 pb-4 border-b border-border bg-card/60">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-500">
              <FileText class="size-5" />
            </div>
            <div>
              <DialogTitle class="text-xl font-bold tracking-tight">
                Incident & Maintenance Template Catalog
              </DialogTitle>
              <p class="text-xs text-muted-foreground mt-0.5">
                Standardized 4-tier failure profiles, diagnostic telemetry keys, and function block signatures
              </p>
            </div>
          </div>
          <Button
            size="sm"
            variant="outline"
            @click="showCreateForm = !showCreateForm"
            class="h-8 text-xs gap-1.5"
          >
            <component :is="showCreateForm ? X : Plus" class="size-3.5" />
            <span>{{ showCreateForm ? 'View Catalog' : 'New Template' }}</span>
          </Button>
        </div>

        <!-- Filter Toolbar (Hidden when creating template) -->
        <div v-if="!showCreateForm" class="mt-4 flex flex-col sm:flex-row gap-3 items-center justify-between">
          <!-- Category Pills -->
          <div class="flex items-center gap-1.5 p-1 bg-muted/60 rounded-lg border border-border w-full sm:w-auto overflow-x-auto">
            <button
              v-for="cat in ['All', ...CATEGORIES]"
              :key="cat"
              @click="activeCategory = cat"
              :class="activeCategory === cat ? 'bg-background shadow-xs text-foreground font-semibold' : 'text-muted-foreground hover:text-foreground'"
              class="px-2.5 py-1 text-xs rounded-md transition-all whitespace-nowrap"
            >
              {{ cat }}
            </button>
          </div>

          <!-- Search & Machine Type Selector -->
          <div class="flex items-center gap-2 w-full sm:w-auto">
            <div class="relative flex-1 sm:w-60">
              <Search class="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" />
              <Input
                v-model="searchQuery"
                placeholder="Search code, title, tags..."
                class="pl-8 h-8 text-xs"
              />
            </div>
            <select
              v-model="selectedMachineType"
              class="h-8 text-xs rounded-md border border-input bg-background px-2 text-foreground focus:outline-hidden focus:ring-1 focus:ring-ring"
            >
              <option value="All">All Machines</option>
              <option v-for="mt in MACHINE_TYPES" :key="mt" :value="mt">{{ mt }}</option>
            </select>
          </div>
        </div>
      </DialogHeader>

      <!-- Modal Body -->
      <div class="flex-1 overflow-y-auto p-6">
        <!-- New Custom Template Form -->
        <div v-if="showCreateForm" class="space-y-4 max-w-2xl mx-auto py-2">
          <div class="p-4 rounded-xl border border-indigo-500/20 bg-indigo-500/5">
            <h3 class="text-sm font-semibold text-indigo-500 flex items-center gap-2">
              <Sparkles class="size-4" />
              Define Custom Incident Template
            </h3>
            <p class="text-xs text-muted-foreground mt-1">
              Add a specialized failure pattern for custom plant machinery or automation cells.
            </p>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-xs font-medium">Error Code <span class="text-destructive">*</span></label>
              <Input v-model="customForm.errorCode" placeholder="e.g. E-LAS-01" class="h-9 text-xs font-mono" />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs font-medium">Category</label>
              <select
                v-model="customForm.category"
                class="w-full h-9 text-xs rounded-md border border-input bg-background px-3 text-foreground"
              >
                <option v-for="cat in CATEGORIES" :key="cat" :value="cat">{{ cat }}</option>
              </select>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="space-y-1.5">
              <label class="text-xs font-medium">Group Name</label>
              <Input v-model="customForm.errorGroup" placeholder="e.g. Laser Marking" class="h-9 text-xs" />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs font-medium">Target Kanban State</label>
              <select
                v-model="customForm.targetKanbanState"
                class="w-full h-9 text-xs rounded-md border border-input bg-background px-3 text-foreground"
              >
                <option value="Open">Open</option>
                <option value="In_Progress">In Progress</option>
                <option value="Pending_Parts">Pending Parts</option>
                <option value="Escalated">Escalated</option>
                <option value="Escalated_External">Escalated External</option>
              </select>
            </div>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium">Short Title / Subject <span class="text-destructive">*</span></label>
            <Input v-model="customForm.shortDescription" placeholder="e.g. Laser Diode Intensity Degradation" class="h-9 text-xs" />
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium">Detailed Failure Description & Troubleshooting Steps</label>
            <textarea
              v-model="customForm.detailedDescription"
              rows="3"
              placeholder="Describe symptoms, potential root causes, and verification steps..."
              class="w-full text-xs rounded-md border border-input bg-background p-2.5 text-foreground focus:outline-hidden focus:ring-1 focus:ring-ring"
            ></textarea>
          </div>

          <div class="space-y-1.5">
            <label class="text-xs font-medium">Default Tags (comma-separated)</label>
            <Input v-model="customForm.defaultTags" placeholder="#Laser, #Optics, #Power" class="h-9 text-xs" />
          </div>

          <!-- Affected Machine Types Selection -->
          <div class="space-y-1.5">
            <label class="text-xs font-medium">Affected Machine Types</label>
            <div class="flex flex-wrap gap-1.5 pt-1">
              <button
                v-for="mt in MACHINE_TYPES"
                :key="mt"
                type="button"
                @click="toggleMachineTypeSelection(mt)"
                :class="customForm.machineTypes.includes(mt) ? 'bg-indigo-500/20 text-indigo-400 border-indigo-500/40' : 'bg-muted/40 text-muted-foreground border-border hover:bg-muted'"
                class="text-xs px-2 py-0.5 rounded border transition-colors"
              >
                {{ mt }}
              </button>
            </div>
          </div>

          <!-- Function Block Signature -->
          <div class="p-3 rounded-lg border border-border bg-card space-y-3">
            <span class="text-xs font-medium text-foreground flex items-center gap-1.5">
              <Cpu class="size-3.5 text-muted-foreground" />
              PLC Function Block Signature (Optional)
            </span>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <Input v-model="customForm.fbBlockName" placeholder="FB_LaserControl" class="h-8 text-xs font-mono" />
              <Input v-model="customForm.fbState" placeholder="State: FAULT" class="h-8 text-xs font-mono" />
              <Input v-model="customForm.fbErrorCode" placeholder="Error: 16#A001" class="h-8 text-xs font-mono" />
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-2">
            <Button variant="ghost" size="sm" @click="showCreateForm = false">Cancel</Button>
            <Button
              size="sm"
              @click="handleCreateCustomTemplate"
              :disabled="isSubmittingCustom || !customForm.errorCode || !customForm.shortDescription"
              class="gap-1.5"
            >
              <Check class="size-3.5" />
              <span>Save & Add to Catalog</span>
            </Button>
          </div>
        </div>

        <!-- Template Grid -->
        <div v-else class="space-y-4">
          <div v-if="isLoading" class="py-12 flex flex-col items-center justify-center text-muted-foreground gap-2">
            <RefreshCw class="size-6 animate-spin text-indigo-500" />
            <span class="text-xs">Loading template catalog...</span>
          </div>

          <div v-else-if="templates.length === 0" class="py-16 text-center text-muted-foreground">
            <FileText class="size-10 mx-auto opacity-30 mb-2" />
            <p class="text-sm font-medium">No templates found</p>
            <p class="text-xs text-muted-foreground">Try clearing filters or search query</p>
          </div>

          <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div
              v-for="tmpl in templates"
              :key="tmpl.id"
              class="group relative rounded-xl border border-border bg-card p-4 hover:border-indigo-500/40 hover:shadow-md transition-all flex flex-col justify-between"
            >
              <div class="space-y-2.5">
                <!-- Top Badge Row -->
                <div class="flex items-center justify-between gap-2">
                  <div class="flex items-center gap-2">
                    <Badge variant="outline" class="font-mono text-xs px-2 py-0.5 bg-muted font-bold">
                      {{ tmpl.errorCode }}
                    </Badge>
                    <Badge variant="outline" :class="getCategoryColor(tmpl.category)" class="text-xs px-2 py-0.5">
                      {{ tmpl.category }}
                    </Badge>
                  </div>
                  <span class="text-xs text-muted-foreground truncate">{{ tmpl.errorGroup }}</span>
                </div>

                <!-- Title & Description -->
                <div>
                  <h4 class="text-sm font-semibold tracking-tight text-foreground group-hover:text-indigo-500 transition-colors">
                    {{ tmpl.shortDescription }}
                  </h4>
                  <p class="text-xs text-muted-foreground mt-1 line-clamp-2 leading-relaxed">
                    {{ tmpl.detailedDescription }}
                  </p>
                </div>

                <!-- PLC Function Block Preview -->
                <div
                  v-if="tmpl.sampleFbState"
                  class="p-2 rounded bg-muted/60 border border-border/60 text-xs font-mono text-muted-foreground flex items-center justify-between"
                >
                  <div class="flex items-center gap-1.5">
                    <Cpu class="size-3 text-indigo-400" />
                    <span>{{ tmpl.sampleFbState.blockName }}</span>
                  </div>
                  <div class="flex items-center gap-2">
                    <span class="text-amber-500">{{ tmpl.sampleFbState.state }}</span>
                    <span v-if="tmpl.sampleFbState.errorCode" class="text-rose-400">{{ tmpl.sampleFbState.errorCode }}</span>
                  </div>
                </div>

                <!-- Machine Types & Tags -->
                <div class="flex flex-wrap gap-1 pt-1">
                  <span
                    v-for="mt in (tmpl.affectedMachineTypes || []).slice(0, 3)"
                    :key="mt"
                    class="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground"
                  >
                    {{ mt }}
                  </span>
                  <span
                    v-for="tag in (tmpl.defaultTags || []).slice(0, 3)"
                    :key="tag"
                    class="text-[10px] px-1.5 py-0.5 rounded bg-indigo-500/10 text-indigo-400"
                  >
                    {{ tag }}
                  </span>
                </div>
              </div>

              <!-- Action Footer -->
              <div class="pt-4 mt-2 border-t border-border flex items-center justify-between">
                <div class="text-[11px] text-muted-foreground">
                  Target: <strong class="text-foreground capitalize">{{ tmpl.targetKanbanState.replace('_', ' ') }}</strong>
                </div>
                <Button
                  size="sm"
                  variant="default"
                  @click="onSelectTemplate(tmpl)"
                  class="h-7 text-xs px-2.5 gap-1.5 bg-indigo-600 hover:bg-indigo-700 text-white"
                >
                  <span>Use Template</span>
                  <ArrowRight class="size-3" />
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="p-3 border-t border-border bg-card/60 flex items-center justify-between text-xs text-muted-foreground">
        <span>{{ templates.length }} templates available</span>
        <Button variant="ghost" size="sm" @click="emit('close')">Close</Button>
      </div>
    </DialogContent>
  </Dialog>
</template>
