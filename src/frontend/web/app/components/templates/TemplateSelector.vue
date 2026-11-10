<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Search,
  Layers,
  Cpu,
  Users,
  Activity,
  ShieldAlert,
  Boxes,
  Check,
  ChevronRight,
  Eye,
  Sparkles
} from 'lucide-vue-next'
import type { GenericTemplate, GenericTemplateType } from '~/types/templates'

const props = withDefaults(defineProps<{
  templates?: GenericTemplate[]
  selectedId?: string | null
  allowedType?: string
  placeholder?: string
  showPreview?: boolean
}>(), {
  templates: () => [],
  selectedId: null,
  allowedType: undefined,
  placeholder: 'Search templates by code, name, or tags...',
  showPreview: true
})

const emit = defineEmits<{
  (e: 'select', template: GenericTemplate): void
  (e: 'update:selectedId', id: string): void
}>()

const searchQuery = ref('')
const selectedCategory = ref<string>('all')
const previewTemplate = ref<GenericTemplate | null>(null)

// Icon mapper
const getIcon = (iconName?: string) => {
  switch (iconName?.toLowerCase()) {
    case 'cpu': return Cpu
    case 'users': return Users
    case 'activity': return Activity
    case 'shieldalert': return ShieldAlert
    case 'boxes': return Boxes
    default: return Layers
  }
}

const availableCategories = computed<string[]>(() => {
  const set = new Set<string>()
  for (const t of props.templates) {
    if (props.allowedType && t.templateType.toLowerCase() !== props.allowedType.toLowerCase()) continue
    if (t.category) set.add(t.category)
  }
  return Array.from(set).sort()
})

const filteredTemplates = computed<GenericTemplate[]>(() => {
  return props.templates.filter(t => {
    if (props.allowedType && t.templateType.toLowerCase() !== props.allowedType.toLowerCase()) {
      return false
    }
    if (selectedCategory.value !== 'all' && t.category.toLowerCase() !== selectedCategory.value.toLowerCase()) {
      return false
    }
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.trim().toLowerCase()
      const match =
        t.code.toLowerCase().includes(q) ||
        t.name.toLowerCase().includes(q) ||
        (t.description && t.description.toLowerCase().includes(q)) ||
        (t.tags && t.tags.some(tag => tag.toLowerCase().includes(q)))
      if (!match) return false
    }
    return true
  })
})

const selectItem = (t: GenericTemplate) => {
  emit('select', t)
  emit('update:selectedId', t.id)
}
</script>

<template>
  <div class="space-y-3">
    <!-- Filters & Search -->
    <div class="flex flex-col sm:flex-row items-stretch sm:items-center gap-2">
      <!-- Search Input -->
      <div class="relative flex-1">
        <Search class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground" />
        <Input
          v-model="searchQuery"
          :placeholder="placeholder"
          class="h-8 pl-8 text-xs"
        />
      </div>

      <!-- Category Filter -->
      <select
        v-if="availableCategories.length > 0"
        v-model="selectedCategory"
        class="h-8 px-2.5 text-xs rounded-md border border-input bg-background"
      >
        <option value="all">All Categories</option>
        <option v-for="cat in availableCategories" :key="cat" :value="cat">
          {{ cat }}
        </option>
      </select>
    </div>

    <!-- Template Cards Grid -->
    <div
      v-if="filteredTemplates.length === 0"
      class="p-6 text-center border rounded-lg border-dashed text-muted-foreground bg-muted/10 text-xs"
    >
      No templates matched your search criteria.
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-2.5 max-h-[360px] overflow-y-auto pr-1">
      <div
        v-for="t in filteredTemplates"
        :key="t.id"
        @click="selectItem(t)"
        class="group p-3 rounded-lg border bg-card hover:border-primary/50 transition-all cursor-pointer relative"
        :class="{ 'ring-2 ring-primary border-transparent bg-primary/5': selectedId === t.id }"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="flex items-start gap-2.5">
            <div
              class="w-8 h-8 rounded-md flex items-center justify-center shrink-0 border"
              :class="selectedId === t.id ? 'bg-primary text-primary-foreground border-primary' : 'bg-muted/50 text-muted-foreground group-hover:bg-primary/10 group-hover:text-primary'"
            >
              <component :is="getIcon(t.icon)" class="w-4 h-4" />
            </div>

            <div class="min-w-0">
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="text-xs font-semibold text-foreground truncate">{{ t.name }}</span>
                <span class="text-[10px] font-mono text-muted-foreground bg-muted/60 px-1 py-0.2 rounded">{{ t.code }}</span>
              </div>
              <p v-if="t.description" class="text-[11px] text-muted-foreground line-clamp-1 mt-0.5">
                {{ t.description }}
              </p>
            </div>
          </div>

          <div v-if="selectedId === t.id" class="text-primary">
            <Check class="w-4 h-4 stroke-[3]" />
          </div>
        </div>

        <!-- Footer Badges -->
        <div class="flex items-center justify-between gap-2 mt-2 pt-2 border-t border-border/50 text-[10px]">
          <div class="flex items-center gap-1.5 overflow-hidden">
            <Badge variant="outline" class="text-[9px] px-1 py-0 uppercase">
              {{ t.templateType }}
            </Badge>
            <Badge variant="secondary" class="text-[9px] px-1 py-0 truncate">
              {{ t.category }}
            </Badge>
            <span class="text-muted-foreground font-mono">
              {{ (t.schema || []).length }} specs
            </span>
          </div>

          <button
            v-if="showPreview"
            type="button"
            @click.stop="previewTemplate = t"
            class="hover:text-primary transition-colors text-muted-foreground flex items-center gap-0.5 text-[10px]"
          >
            <Eye class="w-3 h-3" />
            <span>Preview</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Quick Preview Modal / Drawer -->
    <div
      v-if="previewTemplate"
      class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4"
      @click.self="previewTemplate = null"
    >
      <div class="bg-card text-card-foreground border rounded-xl shadow-xl w-full max-w-lg p-5 space-y-4 max-h-[85vh] overflow-y-auto">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-2">
            <div class="w-8 h-8 rounded-md bg-primary/10 text-primary flex items-center justify-center">
              <component :is="getIcon(previewTemplate.icon)" class="w-4 h-4" />
            </div>
            <div>
              <h3 class="text-sm font-bold text-foreground">{{ previewTemplate.name }}</h3>
              <p class="text-[11px] font-mono text-muted-foreground">{{ previewTemplate.code }} &bull; v{{ previewTemplate.version }}</p>
            </div>
          </div>
          <Button variant="ghost" size="sm" @click="previewTemplate = null" class="h-7 w-7 p-0">
            &times;
          </Button>
        </div>

        <p v-if="previewTemplate.description" class="text-xs text-muted-foreground">
          {{ previewTemplate.description }}
        </p>

        <!-- Lineage / Inherits -->
        <div v-if="previewTemplate.extendsTemplateCode" class="p-2.5 rounded bg-blue-500/10 border border-blue-500/20 text-xs text-blue-600 dark:text-blue-400">
          <span class="font-semibold">Inherits From:</span> {{ previewTemplate.extendsTemplateName }} ({{ previewTemplate.extendsTemplateCode }})
        </div>

        <!-- Specifications Preview -->
        <div class="space-y-2">
          <h4 class="text-xs font-semibold text-foreground uppercase tracking-wide">
            Template Specifications ({{ (previewTemplate.schema || []).length }})
          </h4>
          <div class="border rounded-lg divide-y text-xs">
            <div
              v-for="f in (previewTemplate.schema || [])"
              :key="f.key"
              class="p-2 flex items-center justify-between"
            >
              <div>
                <span class="font-medium text-foreground">{{ f.label }}</span>
                <span v-if="f.required" class="text-rose-500 ml-0.5">*</span>
                <span class="text-[10px] font-mono text-muted-foreground ml-1.5">({{ f.key }})</span>
              </div>
              <div class="flex items-center gap-1.5">
                <Badge variant="outline" class="text-[9px] font-mono uppercase">{{ f.fieldType }}</Badge>
                <span v-if="f.unit" class="text-[10px] font-mono text-muted-foreground">{{ f.unit }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="flex justify-end gap-2 pt-2 border-t">
          <Button variant="outline" size="sm" @click="previewTemplate = null">
            Close
          </Button>
          <Button
            size="sm"
            @click="selectItem(previewTemplate); previewTemplate = null"
          >
            Apply Template
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
