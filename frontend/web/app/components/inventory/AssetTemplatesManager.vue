<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  FolderTree,
  Cpu,
  Code,
  Tag,
  ChevronDown,
  ChevronRight,
  GitBranch,
  Building,
  User,
  DollarSign,
  Edit2,
  Check,
  X,
  ExternalLink,
  Percent,
  Plus,
  Trash2
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { AssetTemplateDefinition, InventoryPart } from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'
import CreateEditTemplateModal from './CreateEditTemplateModal.vue'

const props = defineProps<{
  templates: AssetTemplateDefinition[]
}>()

const emit = defineEmits<{
  (e: 'refresh'): void
}>()

const { formatCurrency, activeCurrency, updateResellPrice, deleteTemplate, canManageInventory } = usePartsInventory()

const activeCategory = ref<'all' | 'Hardware' | 'Software'>('all')
const expandedTemplates = ref<Set<string>>(new Set(['tmpl-ipc-controller', 'tmpl-vision-sensor']))
const editingInstance = ref<InventoryPart | null>(null)
const editWearPercent = ref<number>(0)
const editResellOverride = ref<number | undefined>(undefined)

const showTemplateModal = ref(false)
const templateModalMode = ref<'create' | 'edit'>('create')
const selectedTemplateForEdit = ref<AssetTemplateDefinition | null>(null)

const openCreateTemplate = () => {
  templateModalMode.value = 'create'
  selectedTemplateForEdit.value = null
  showTemplateModal.value = true
}

const openEditTemplate = (tmpl: AssetTemplateDefinition) => {
  templateModalMode.value = 'edit'
  selectedTemplateForEdit.value = tmpl
  showTemplateModal.value = true
}

const handleDeleteTemplate = async (tmpl: AssetTemplateDefinition) => {
  if (confirm(`Are you sure you want to delete template "${tmpl.name}"?`)) {
    try {
      await deleteTemplate(tmpl.id)
      emit('refresh')
    } catch (err: any) {
      alert(err.data?.statusMessage || err.message || 'Failed to delete template.')
    }
  }
}

const onTemplateSaved = () => {
  emit('refresh')
}

const toggleExpand = (templateId: string) => {
  if (expandedTemplates.value.has(templateId)) {
    expandedTemplates.value.delete(templateId)
  } else {
    expandedTemplates.value.add(templateId)
  }
}

const filteredTemplates = computed(() => {
  if (activeCategory.value === 'all') return props.templates
  return props.templates.filter(t => t.topLevelCategory === activeCategory.value)
})

const getParentTemplateName = (parentId?: string) => {
  if (!parentId) return null
  const parent = props.templates.find(t => t.id === parentId)
  return parent ? parent.name : parentId
}

const startEditResell = (instance: InventoryPart) => {
  editingInstance.value = instance
  editWearPercent.value = instance.wearDepreciationPercentage || 0
  editResellOverride.value = instance.resellPriceOverrideEur
}

const cancelEditResell = () => {
  editingInstance.value = null
}

const saveEditResell = async () => {
  if (!editingInstance.value) return
  await updateResellPrice(editingInstance.value.id, {
    wearDepreciationPercentage: editWearPercent.value,
    resellPriceOverrideEur: editResellOverride.value
  })
  editingInstance.value = null
  emit('refresh')
}
</script>

<template>
  <div class="space-y-4">
    <!-- Header & Category Switcher -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-xl bg-card border border-border shadow-xs">
      <div>
        <h3 class="text-sm font-bold text-foreground flex items-center gap-2">
          <FolderTree class="size-4 text-primary" />
          <span>Asset Templates & Serialized Instances Tree</span>
        </h3>
        <p class="text-xs text-muted-foreground mt-0.5">
          Templates with inheritance, fixed OEM specifications, identifier sequencing, and instance valuations
        </p>
      </div>

      <!-- Top-Level Categorization Filter & Actions -->
      <div class="flex flex-wrap items-center gap-2 self-start sm:self-auto">
        <div class="flex items-center gap-1.5 p-1 bg-muted/60 rounded-lg border border-border">
          <Button
            variant="ghost"
            size="sm"
            @click="activeCategory = 'all'"
            :class="activeCategory === 'all' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            class="h-7 px-3 text-xs cursor-pointer"
          >
            All ({{ templates.length }})
          </Button>
          <Button
            variant="ghost"
            size="sm"
            @click="activeCategory = 'Hardware'"
            :class="activeCategory === 'Hardware' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            class="h-7 px-3 text-xs flex items-center gap-1.5 cursor-pointer"
          >
            <Cpu class="size-3" />
            Hardware
          </Button>
          <Button
            variant="ghost"
            size="sm"
            @click="activeCategory = 'Software'"
            :class="activeCategory === 'Software' ? 'bg-primary text-primary-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'"
            class="h-7 px-3 text-xs flex items-center gap-1.5 cursor-pointer"
          >
            <Code class="size-3" />
            Software
          </Button>
        </div>

        <Button
          size="sm"
          @click="openCreateTemplate"
          class="h-8 px-3 text-xs gap-1.5 bg-primary text-primary-foreground hover:bg-primary/90 font-semibold shadow-xs cursor-pointer"
        >
          <Plus class="size-3.5" />
          <span>New Template</span>
        </Button>
      </div>
    </div>

    <!-- Templates List & Instances Tree -->
    <div class="space-y-3">
      <div
        v-for="tmpl in filteredTemplates"
        :key="tmpl.id"
        class="rounded-xl border border-border bg-card overflow-hidden transition-all shadow-xs"
      >
        <!-- Template Header Row -->
        <div
          @click="toggleExpand(tmpl.id)"
          class="p-4 flex flex-col md:flex-row md:items-center justify-between gap-3 cursor-pointer hover:bg-muted/30 transition-colors select-none"
        >
          <div class="flex items-start gap-3">
            <button type="button" class="mt-0.5 text-muted-foreground p-0.5 hover:text-foreground">
              <ChevronDown v-if="expandedTemplates.has(tmpl.id)" class="size-4" />
              <ChevronRight v-else class="size-4" />
            </button>

            <div class="space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <span class="text-sm font-bold text-foreground">{{ tmpl.name }}</span>

                <!-- Top-Level Category Badge -->
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
                  :class="tmpl.topLevelCategory === 'Hardware' ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20' : 'bg-blue-500/15 text-blue-600 dark:text-blue-400 border border-blue-500/20'"
                >
                  {{ tmpl.topLevelCategory }}
                </span>

                <!-- Inherited Template Badge -->
                <span
                  v-if="tmpl.extendsTemplateId"
                  class="flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-500/20"
                >
                  <GitBranch class="size-3" />
                  <span>Inherits: {{ getParentTemplateName(tmpl.extendsTemplateId) }}</span>
                </span>

                <!-- Identifier Pattern -->
                <span class="font-mono text-[10px] px-2 py-0.5 rounded bg-muted text-muted-foreground border border-border">
                  ID: {{ tmpl.identifierPattern }}
                </span>
              </div>

              <p class="text-xs text-muted-foreground leading-relaxed">
                {{ tmpl.description }}
              </p>
            </div>
          </div>

          <!-- Fixed Fields & Instances Count -->
          <div class="flex flex-wrap items-center gap-4 text-xs md:text-right shrink-0">
            <div>
              <div class="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">OEM / Supplier</div>
              <div class="font-medium text-foreground">{{ tmpl.fixedFields.manufacturer }}</div>
            </div>

            <div>
              <div class="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">Base Price</div>
              <div class="font-mono font-bold text-emerald-600 dark:text-emerald-400">
                €{{ formatCurrency(tmpl.fixedFields.basePriceEur, 'EUR') }}
              </div>
            </div>

            <div>
              <div class="text-[10px] text-muted-foreground uppercase tracking-wider font-semibold">Serialized Units</div>
              <div class="font-mono font-bold text-foreground">
                {{ tmpl.instances?.length || 0 }} instantiated
              </div>
            </div>

            <!-- Template Blueprint Actions -->
            <div class="flex items-center gap-1.5 ml-1">
              <Button
                variant="outline"
                size="sm"
                @click.stop="openEditTemplate(tmpl)"
                class="h-7 px-2.5 text-xs text-muted-foreground hover:text-foreground border-border bg-card shadow-2xs cursor-pointer"
                title="Edit Blueprint Specs & Schema"
              >
                <Edit2 class="size-3 mr-1 text-primary" />
                <span>Edit</span>
              </Button>
              <Button
                variant="ghost"
                size="sm"
                @click.stop="handleDeleteTemplate(tmpl)"
                class="h-7 w-7 p-0 text-muted-foreground hover:text-destructive hover:bg-destructive/10 cursor-pointer"
                title="Delete Template"
              >
                <Trash2 class="size-3.5" />
              </Button>
            </div>
          </div>
        </div>

        <!-- Expanded Instances Tree View -->
        <div v-if="expandedTemplates.has(tmpl.id)" class="border-t border-border bg-muted/15 p-4 space-y-3">
          <!-- Template Fixed vs Instance Schema Banner -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs p-3 rounded-lg bg-card border border-border">
            <div>
              <span class="font-semibold text-primary">Fixed Template Specs:</span>
              <div class="mt-1 flex flex-wrap gap-2 text-[11px] font-mono text-muted-foreground">
                <span v-for="(v, k) in tmpl.fixedFields.specs" :key="k" class="px-1.5 py-0.5 rounded bg-muted border border-border">
                  {{ k }}: {{ v }}
                </span>
              </div>
            </div>
            <div>
              <span class="font-semibold text-primary">Instance-Specific Schema Fields:</span>
              <div class="mt-1 flex flex-wrap gap-2 text-[11px] font-mono text-muted-foreground">
                <span v-for="field in tmpl.instanceSpecificFieldsSchema" :key="field.key" class="px-1.5 py-0.5 rounded bg-muted border border-border">
                  {{ field.label }} ({{ field.type }})
                </span>
              </div>
            </div>
          </div>

          <!-- Instances Tree Table -->
          <div class="space-y-2">
            <div class="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <FolderTree class="size-3.5 text-primary" />
              <span>Instantiated Units (Quantity fixed to 1 per serialized asset)</span>
            </div>

            <div v-if="tmpl.instances && tmpl.instances.length > 0" class="border border-border rounded-lg overflow-x-auto bg-card">
              <table class="w-full text-xs text-left">
                <thead class="bg-muted/40 text-muted-foreground border-b border-border font-medium">
                  <tr>
                    <th class="p-2.5">Identifier & Serial</th>
                    <th class="p-2.5">Condition</th>
                    <th class="p-2.5">Operational State</th>
                    <th class="p-2.5">Owner</th>
                    <th class="p-2.5">Storage / Machine Location</th>
                    <th class="p-2.5">Wear Math & Resell Price</th>
                    <th class="p-2.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-border">
                  <tr v-for="inst in tmpl.instances" :key="inst.id" class="hover:bg-muted/20">
                    <td class="p-2.5">
                      <div class="font-mono font-bold text-primary">{{ inst.customIdentifier }}</div>
                      <div class="font-mono text-[11px] text-muted-foreground">{{ inst.serialNumber || 'No SN' }}</div>
                    </td>
                    <td class="p-2.5">
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wider"
                        :class="{
                          'bg-emerald-500/15 text-emerald-600': inst.condition === 'new',
                          'bg-blue-500/15 text-blue-600': inst.condition === 'used',
                          'bg-amber-500/15 text-amber-600': inst.condition === 'donor',
                          'bg-neutral-500/15 text-neutral-600': inst.condition === 'obsolete',
                          'bg-rose-500/15 text-rose-600': inst.condition === 'scrap'
                        }"
                      >
                        {{ inst.condition }}
                      </span>
                    </td>
                    <td class="p-2.5">
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-medium"
                        :class="{
                          'bg-emerald-500/15 text-emerald-600': inst.operationalState === 'working',
                          'bg-purple-500/15 text-purple-600': inst.operationalState === 'in_service',
                          'bg-amber-500/15 text-amber-600': inst.operationalState === 'evaluation',
                          'bg-destructive/15 text-destructive': inst.operationalState === 'broken'
                        }"
                      >
                        {{ inst.operationalState || 'N/A' }}
                      </span>
                    </td>
                    <td class="p-2.5">
                      <div class="flex items-center gap-1 text-[11px] text-foreground">
                        <Building v-if="inst.owner.type === 'organization'" class="size-3 text-muted-foreground" />
                        <User v-else class="size-3 text-primary" />
                        <span class="truncate max-w-[120px]">{{ inst.owner.name }}</span>
                      </div>
                    </td>
                    <td class="p-2.5 text-muted-foreground text-[11px]">
                      {{ inst.location }}
                    </td>
                    <td class="p-2.5">
                      <div class="font-mono font-semibold text-foreground">
                        €{{ inst.estimatedResellPriceEur }}
                        <span v-if="inst.wearDepreciationPercentage" class="text-[10px] text-muted-foreground font-normal">
                          (-{{ inst.wearDepreciationPercentage }}% wear)
                        </span>
                      </div>
                      <div v-if="inst.resellPriceOverrideEur !== undefined" class="text-[10px] font-mono text-amber-600">
                        Manual Override
                      </div>
                    </td>
                    <td class="p-2.5 text-right">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        @click="startEditResell(inst)"
                        class="h-7 text-[11px] gap-1 cursor-pointer text-muted-foreground hover:text-foreground"
                      >
                        <Edit2 class="size-3" />
                        <span>Valuation</span>
                      </Button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div v-else class="p-6 text-center text-xs text-muted-foreground border border-dashed border-border rounded-lg bg-card">
              No serialized instances currently instantiated for this template. Use "Log Part" to instantiate one.
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Edit Valuation Modal Overlay -->
    <div v-if="editingInstance" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
      <div class="relative w-full max-w-md bg-card border border-border rounded-xl shadow-2xl p-6 space-y-4">
        <div class="flex items-center justify-between pb-3 border-b border-border">
          <div>
            <h4 class="text-sm font-bold text-foreground">Adjust Resell Price & Wear Scalar</h4>
            <p class="text-xs text-muted-foreground mt-0.5">
              Unit: {{ editingInstance.customIdentifier }} (Base: €{{ editingInstance.priceEur }})
            </p>
          </div>
          <button type="button" @click="cancelEditResell" class="p-1 rounded text-muted-foreground hover:text-foreground cursor-pointer">
            <X class="size-4" />
          </button>
        </div>

        <div class="space-y-3 text-xs">
          <div class="space-y-1.5">
            <label class="font-semibold text-foreground flex items-center gap-1.5">
              <Percent class="size-3.5 text-primary" />
              <span>Wear Depreciation Percentage (0 - 100%)</span>
            </label>
            <Input v-model.number="editWearPercent" type="number" min="0" max="100" class="h-9 text-xs font-mono" />
            <p class="text-[11px] text-muted-foreground">
              Calculates resell price via scalar math: basePrice * (1 - wear%) = €{{ Math.round(editingInstance.priceEur * (1 - (editWearPercent / 100)) * 100) / 100 }}
            </p>
          </div>

          <div class="space-y-1.5">
            <label class="font-semibold text-foreground flex items-center gap-1.5">
              <DollarSign class="size-3.5 text-amber-500" />
              <span>Or Direct Resell Price Override (EUR)</span>
            </label>
            <Input v-model.number="editResellOverride" type="number" min="0" placeholder="Optional fixed override" class="h-9 text-xs font-mono" />
          </div>
        </div>

        <div class="flex items-center justify-end gap-2 pt-3 border-t border-border">
          <Button variant="outline" size="sm" @click="cancelEditResell" class="h-8 text-xs cursor-pointer">
            Cancel
          </Button>
          <Button size="sm" @click="saveEditResell" class="h-8 text-xs bg-primary text-primary-foreground hover:bg-primary/90 cursor-pointer">
            Save Valuation
          </Button>
        </div>
      </div>
    </div>

    <!-- Create / Edit Template Modal -->
    <CreateEditTemplateModal
      :open="showTemplateModal"
      :mode="templateModalMode"
      :template="selectedTemplateForEdit"
      :available-templates="templates"
      @update:open="showTemplateModal = $event"
      @saved="onTemplateSaved"
    />
  </div>
</template>
