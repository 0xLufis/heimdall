<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  FileText, Search, Plus, Filter, Tag, Cpu, Activity,
  Check, ArrowRight, ShieldAlert, Sparkles, Wrench, X, RefreshCw,
  Download, Edit2, ArrowLeft, Layers, SlidersHorizontal, CheckCircle2
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '~/components/ui/card'
import {
  ERROR_TEMPLATES, CATEGORIES, MACHINE_TYPES, TECHNOLOGIES,
  exportTwinCatDutXml
} from '~/utils/errorTemplateEngine'
import type { ErrorTemplate } from '~/types/maintenance'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const router = useRouter()

const activeCategory = ref<string>('All')
const selectedMachineType = ref<string>('All')
const selectedTechnology = ref<string>('All')
const searchQuery = ref('')
const isLoading = ref(false)
const templates = ref<ErrorTemplate[]>([...ERROR_TEMPLATES])

// Create / Edit modal state
const showForm = ref(false)
const isEditing = ref(false)
const editingTemplateId = ref<string | null>(null)
const isSubmitting = ref(false)
const formError = ref<string | null>(null)
const formSuccess = ref(false)

const form = ref({
  errorCode: '',
  category: 'Error' as 'Prevention' | 'Error' | 'Improvement' | 'ETC',
  errorGroup: '',
  shortDescription: '',
  detailedDescription: '',
  targetKanbanState: 'Open', // Strictly Open
  defaultTags: '',
  machineTypes: [] as string[],
  technologies: [] as string[],
  machineGroups: [] as string[],
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
      let list = res.templates
      if (selectedTechnology.value !== 'All') {
        list = list.filter(t => t.assignedTechnologies?.includes(selectedTechnology.value))
      }
      templates.value = list
    }
  } catch {
    let local = [...ERROR_TEMPLATES]
    if (activeCategory.value !== 'All') {
      local = local.filter(t => t.category.toLowerCase() === activeCategory.value.toLowerCase())
    }
    if (selectedMachineType.value !== 'All') {
      local = local.filter(t => t.affectedMachineTypes?.includes(selectedMachineType.value))
    }
    if (selectedTechnology.value !== 'All') {
      local = local.filter(t => t.assignedTechnologies?.includes(selectedTechnology.value))
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

function openCreateForm() {
  isEditing.value = false
  editingTemplateId.value = null
  formError.value = null
  formSuccess.value = false
  form.value = {
    errorCode: `E-CUST-${Math.floor(10 + Math.random() * 90)}`,
    category: 'Error',
    errorGroup: 'Mechanical & Automation',
    shortDescription: '',
    detailedDescription: '',
    targetKanbanState: 'Open',
    defaultTags: '#Automation, #Maintenance',
    machineTypes: ['Pressing'],
    technologies: ['Assembly'],
    machineGroups: ['grp-line01'],
    fbBlockName: 'FB_StationControl',
    fbState: 'FAULT',
    fbErrorCode: '16#9901'
  }
  showForm.value = true
}

function openEditForm(tmpl: ErrorTemplate) {
  isEditing.value = true
  editingTemplateId.value = tmpl.id
  formError.value = null
  formSuccess.value = false
  form.value = {
    errorCode: tmpl.errorCode,
    category: tmpl.category as any,
    errorGroup: tmpl.errorGroup,
    shortDescription: tmpl.shortDescription,
    detailedDescription: tmpl.detailedDescription,
    targetKanbanState: 'Open', // strict open
    defaultTags: tmpl.defaultTags.join(', '),
    machineTypes: tmpl.affectedMachineTypes ? [...tmpl.affectedMachineTypes] : [],
    technologies: tmpl.assignedTechnologies ? [...tmpl.assignedTechnologies] : [],
    machineGroups: tmpl.assignedMachineGroups ? [...tmpl.assignedMachineGroups] : [],
    fbBlockName: tmpl.sampleFbState?.blockName || '',
    fbState: tmpl.sampleFbState?.state || '',
    fbErrorCode: tmpl.sampleFbState?.errorCode || ''
  }
  showForm.value = true
}

async function handleSubmitForm() {
  if (!form.value.errorCode.trim() || !form.value.shortDescription.trim()) {
    formError.value = 'Error code and short description are required.'
    return
  }

  isSubmitting.value = true
  formError.value = null

  const tagsArray = form.value.defaultTags
    .split(',')
    .map(t => t.trim())
    .filter(Boolean)
    .map(t => (t.startsWith('#') ? t : `#${t}`))

  const payload: any = {
    id: editingTemplateId.value || form.value.errorCode.trim(),
    errorCode: form.value.errorCode.trim(),
    category: form.value.category,
    errorGroup: form.value.errorGroup.trim() || 'General',
    shortDescription: form.value.shortDescription.trim(),
    detailedDescription: form.value.detailedDescription.trim(),
    targetKanbanState: 'Open', // strict
    defaultTags: tagsArray,
    affectedMachineTypes: form.value.machineTypes,
    assignedTechnologies: form.value.technologies,
    assignedMachineGroups: form.value.machineGroups,
    sampleFbState: form.value.fbBlockName ? {
      blockName: form.value.fbBlockName.trim(),
      state: form.value.fbState.trim() || 'FAULT',
      errorCode: form.value.fbErrorCode.trim() || undefined
    } : undefined
  }

  try {
    if (isEditing.value) {
      await $fetch('/api/tickets/templates', {
        method: 'PUT',
        body: payload
      })
    } else {
      await $fetch('/api/tickets/templates', {
        method: 'POST',
        body: payload
      })
    }
    formSuccess.value = true
    await fetchTemplates()
    setTimeout(() => {
      showForm.value = false
    }, 600)
  } catch (err: any) {
    formError.value = err?.message || 'Failed to save template.'
  } finally {
    isSubmitting.value = false
  }
}

function triggerDownloadDut() {
  try {
    const xml = exportTwinCatDutXml(templates.value)
    const blob = new Blob([xml], { type: 'application/xml;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'E_HeimdallErrorCode.TcDUT'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  } catch (e) {
    console.error('Failed to export TwinCAT DUT', e)
  }
}

function instantiateTicket(tmpl: ErrorTemplate) {
  router.push({
    path: '/dashboard/tickets',
    query: {
      create: 'true',
      templateId: tmpl.id || tmpl.errorCode
    }
  })
}

function getCategoryColor(cat: string) {
  switch (cat) {
    case 'Prevention': return 'text-emerald-700 dark:text-emerald-300 bg-emerald-500/10 border-emerald-500/20'
    case 'Improvement': return 'text-sky-700 dark:text-sky-300 bg-sky-500/10 border-sky-500/20'
    case 'ETC': return 'text-amber-700 dark:text-amber-300 bg-amber-500/10 border-amber-500/20'
    default: return 'text-rose-700 dark:text-rose-300 bg-rose-500/10 border-rose-500/20'
  }
}

watch([activeCategory, selectedMachineType, selectedTechnology], () => {
  fetchTemplates()
})

let debounceTimer: any = null
watch(searchQuery, () => {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    fetchTemplates()
  }, 200)
})

onMounted(() => {
  fetchTemplates()
})
</script>

<template>
  <div class="space-y-6 pb-16">
    <!-- Header with Breadcrumb and Actions -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-border">
      <div class="flex items-center gap-3">
        <NuxtLink to="/dashboard/tickets">
          <Button variant="ghost" size="icon" class="rounded-xl h-9 w-9 text-muted-foreground hover:text-foreground">
            <ArrowLeft class="size-4" />
          </Button>
        </NuxtLink>
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-2xl font-bold tracking-tight text-foreground">
              4-Tier Ticket Templates Catalog
            </h1>
            <Badge variant="outline" class="border-indigo-500/30 text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 text-xs">
              {{ templates.length }} Profiles
            </Badge>
          </div>
          <p class="text-sm text-muted-foreground mt-0.5">
            Standardized failure modes, automated PLC telemetry signatures, and Beckhoff TwinCAT .TcDUT export
          </p>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          @click="triggerDownloadDut"
          class="border-border bg-card hover:bg-muted text-foreground rounded-lg px-3 h-8 text-xs font-medium flex items-center gap-1.5 transition-colors"
          title="Export TwinCAT 3 .TcDUT IEC 61131-3 error enum file"
        >
          <Download class="size-3.5 text-indigo-600 dark:text-indigo-400" />
          <span>Export TwinCAT .TcDUT</span>
        </Button>

        <Button
          size="sm"
          @click="openCreateForm"
          class="bg-primary hover:bg-primary/90 text-primary-foreground rounded-lg px-3.5 h-8 text-xs font-medium flex items-center gap-1.5 shadow-sm transition-colors"
        >
          <Plus class="size-3.5" />
          <span>New Template</span>
        </Button>
      </div>
    </div>

    <!-- Filter & Scope Bar -->
    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-card border border-border p-4 rounded-2xl shadow-xs">
      <!-- Category Tabs -->
      <div class="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0">
        <Button
          variant="ghost"
          size="sm"
          @click="activeCategory = 'All'"
          :class="activeCategory === 'All' ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          class="rounded-lg h-8 text-xs"
        >
          All
        </Button>
        <Button
          v-for="cat in CATEGORIES"
          :key="cat"
          variant="ghost"
          size="sm"
          @click="activeCategory = cat"
          :class="activeCategory === cat ? 'bg-primary text-primary-foreground font-semibold shadow-xs' : 'text-muted-foreground hover:text-foreground'"
          class="rounded-lg h-8 text-xs"
        >
          {{ cat }}
        </Button>
      </div>

      <!-- Search & Filters -->
      <div class="flex flex-wrap items-center gap-2.5">
        <div class="relative w-full sm:w-64">
          <Search class="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
          <Input
            v-model="searchQuery"
            placeholder="Search code, title, tags..."
            class="pl-8 h-8 text-xs bg-background rounded-lg border-border"
          />
        </div>

        <!-- Machine Type Filter -->
        <select
          v-model="selectedMachineType"
          class="h-8 rounded-lg border border-border bg-background px-2.5 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
        >
          <option value="All">All Machine Types</option>
          <option v-for="m in MACHINE_TYPES" :key="m" :value="m">{{ m }}</option>
        </select>

        <!-- Technology Filter -->
        <select
          v-model="selectedTechnology"
          class="h-8 rounded-lg border border-border bg-background px-2.5 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
        >
          <option value="All">All Technologies</option>
          <option v-for="t in TECHNOLOGIES" :key="t" :value="t">{{ t }}</option>
        </select>
      </div>
    </div>

    <!-- Template Grid -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <Card
        v-for="tmpl in templates"
        :key="tmpl.id"
        class="bg-card border-border hover:border-border/80 transition-all rounded-2xl flex flex-col justify-between group shadow-xs hover:shadow-md"
      >
        <CardHeader class="pb-3">
          <div class="flex items-start justify-between gap-2">
            <div class="flex items-center gap-2">
              <span class="font-mono text-xs font-bold text-foreground bg-muted px-2 py-0.5 rounded-md border border-border">
                {{ tmpl.errorCode }}
              </span>
              <Badge variant="outline" :class="getCategoryColor(tmpl.category)" class="text-[10px] font-semibold uppercase">
                {{ tmpl.category }}
              </Badge>
            </div>
            <div class="flex items-center gap-1 opacity-80 group-hover:opacity-100 transition-opacity">
              <Button
                variant="ghost"
                size="icon"
                @click="openEditForm(tmpl)"
                class="size-7 rounded-md text-muted-foreground hover:text-foreground"
                title="Edit Template"
              >
                <Edit2 class="size-3.5" />
              </Button>
            </div>
          </div>

          <CardTitle class="text-sm font-semibold text-foreground pt-1.5 leading-snug">
            {{ tmpl.shortDescription }}
          </CardTitle>
          <CardDescription class="text-xs text-muted-foreground line-clamp-2">
            {{ tmpl.detailedDescription }}
          </CardDescription>
        </CardHeader>

        <CardContent class="pt-0 space-y-3">
          <!-- PLC Function Block & Error Code -->
          <div v-if="tmpl.sampleFbState" class="p-2 rounded-lg bg-muted/60 border border-border/80 space-y-1 font-mono text-[11px]">
            <div class="flex items-center justify-between text-muted-foreground">
              <span>PLC Block:</span>
              <span class="font-semibold text-foreground">{{ tmpl.sampleFbState.blockName }}</span>
            </div>
            <div v-if="tmpl.sampleFbState.errorCode" class="flex items-center justify-between text-muted-foreground">
              <span>Error Word:</span>
              <span class="text-indigo-600 dark:text-indigo-400 font-bold">{{ tmpl.sampleFbState.errorCode }}</span>
            </div>
          </div>

          <!-- Technologies & Machine Types -->
          <div class="flex flex-wrap gap-1 text-[10px]">
            <span
              v-for="tech in (tmpl.assignedTechnologies || [])"
              :key="tech"
              class="px-1.5 py-0.5 rounded bg-cyan-500/10 text-cyan-700 dark:text-cyan-300 border border-cyan-500/20 font-medium"
            >
              {{ tech }}
            </span>
            <span
              v-for="mach in (tmpl.affectedMachineTypes || []).slice(0, 2)"
              :key="mach"
              class="px-1.5 py-0.5 rounded bg-muted text-muted-foreground font-mono"
            >
              {{ mach }}
            </span>
          </div>

          <!-- Bottom Footer Action -->
          <div class="pt-2 border-t border-border flex items-center justify-between">
            <span class="text-[10px] text-muted-foreground">Initial: <strong class="text-foreground">Open</strong></span>
            <Button
              size="sm"
              variant="outline"
              @click="instantiateTicket(tmpl)"
              class="h-7 px-2.5 text-xs text-indigo-600 dark:text-indigo-400 border-indigo-500/30 hover:bg-indigo-500/10 rounded-lg flex items-center gap-1"
            >
              <span>Use Template</span>
              <ArrowRight class="size-3" />
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Create / Edit Template Workbench Drawer (Zero Modal Overlays) -->
    <div
      v-if="showForm"
      class="fixed inset-y-0 right-0 z-50 w-full max-w-xl bg-card border-l border-border shadow-2xl overflow-y-auto animate-in slide-in-from-right duration-200 flex flex-col justify-between"
    >
      <div>
        <div class="flex items-center justify-between p-4 border-b border-border bg-muted/40">
          <div class="flex items-center gap-2">
            <FileText class="size-4 text-primary" />
            <h3 class="text-sm font-bold text-foreground">
              {{ isEditing ? 'Edit Ticket Template' : 'Create 4-Tier Ticket Template' }}
            </h3>
          </div>
          <Button variant="ghost" size="icon" class="size-7 rounded-lg text-muted-foreground" @click="showForm = false">
            <X class="size-4" />
          </Button>
        </div>

        <form @submit.prevent="handleSubmitForm" class="p-5 space-y-4 max-h-[80vh] overflow-y-auto">
          <div v-if="formError" class="p-3 rounded-lg bg-rose-500/10 border border-rose-500/20 text-rose-600 dark:text-rose-400 text-xs">
            {{ formError }}
          </div>
          <div v-if="formSuccess" class="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 text-xs flex items-center gap-2">
            <CheckCircle2 class="size-4" />
            <span>Template saved successfully!</span>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="text-[11px] font-bold text-muted-foreground uppercase">Error Code *</label>
              <Input v-model="form.errorCode" class="mt-1 h-8 text-xs font-mono" placeholder="E-MOT-01" required />
            </div>
            <div>
              <label class="text-[11px] font-bold text-muted-foreground uppercase">Category *</label>
              <select v-model="form.category" class="mt-1 w-full h-8 rounded-md border border-border bg-background px-2 text-xs">
                <option v-for="cat in CATEGORIES" :key="cat" :value="cat">{{ cat }}</option>
              </select>
            </div>
          </div>

          <div>
            <label class="text-[11px] font-bold text-muted-foreground uppercase">Short Description *</label>
            <Input v-model="form.shortDescription" class="mt-1 h-8 text-xs" placeholder="Axis Position Divergence" required />
          </div>

          <div>
            <label class="text-[11px] font-bold text-muted-foreground uppercase">Detailed Description</label>
            <textarea
              v-model="form.detailedDescription"
              rows="3"
              class="mt-1 w-full rounded-md border border-border bg-background p-2 text-xs text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
              placeholder="Root causes, recovery instructions, tool checks..."
            ></textarea>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="text-[11px] font-bold text-muted-foreground uppercase">Error Group</label>
              <Input v-model="form.errorGroup" class="mt-1 h-8 text-xs" placeholder="Motion & Drive" />
            </div>
            <div>
              <label class="text-[11px] font-bold text-muted-foreground uppercase">Initial State (Enforced)</label>
              <Input :value="'Open'" disabled class="mt-1 h-8 text-xs bg-muted font-bold" />
            </div>
          </div>

          <!-- PLC Function Block Linking -->
          <div class="p-3 bg-muted/40 rounded-xl border border-border space-y-3">
            <h4 class="text-xs font-bold text-foreground flex items-center gap-1.5">
              <Cpu class="size-3.5 text-indigo-600 dark:text-indigo-400" />
              <span>TwinCAT PLC Function Block Binding</span>
            </h4>
            <div class="grid grid-cols-3 gap-2">
              <div>
                <label class="text-[10px] text-muted-foreground">FB Name</label>
                <Input v-model="form.fbBlockName" class="mt-0.5 h-7 text-xs font-mono" placeholder="FB_AxisControl" />
              </div>
              <div>
                <label class="text-[10px] text-muted-foreground">State</label>
                <Input v-model="form.fbState" class="mt-0.5 h-7 text-xs font-mono" placeholder="ERROR_STOP" />
              </div>
              <div>
                <label class="text-[10px] text-muted-foreground">Hex Code</label>
                <Input v-model="form.fbErrorCode" class="mt-0.5 h-7 text-xs font-mono" placeholder="16#4330" />
              </div>
            </div>
          </div>

          <!-- Technologies Checklist -->
          <div>
            <label class="text-[11px] font-bold text-muted-foreground uppercase">Assigned Technologies</label>
            <div class="mt-1.5 flex flex-wrap gap-1.5">
              <label
                v-for="tech in TECHNOLOGIES"
                :key="tech"
                class="flex items-center gap-1.5 px-2 py-1 rounded-md border border-border text-xs cursor-pointer select-none"
                :class="form.technologies.includes(tech) ? 'bg-primary/10 border-primary/40 text-primary font-medium' : 'bg-background text-muted-foreground'"
              >
                <input
                  type="checkbox"
                  :value="tech"
                  v-model="form.technologies"
                  class="size-3 rounded text-primary focus:ring-0"
                />
                <span>{{ tech }}</span>
              </label>
            </div>
          </div>

          <!-- Tags -->
          <div>
            <label class="text-[11px] font-bold text-muted-foreground uppercase">Default Tags (Comma separated)</label>
            <Input v-model="form.defaultTags" class="mt-1 h-8 text-xs font-mono" placeholder="#Motion, #Axis, #Servo" />
          </div>

          <div class="pt-3 border-t border-border flex items-center justify-end gap-2">
            <Button type="button" variant="outline" size="sm" @click="showForm = false" class="rounded-lg h-8 text-xs">
              Cancel
            </Button>
            <Button type="submit" size="sm" :disabled="isSubmitting" class="rounded-lg h-8 text-xs bg-primary text-primary-foreground font-medium">
              {{ isSubmitting ? 'Saving...' : isEditing ? 'Save Changes' : 'Create Template' }}
            </Button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
