<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { GaugeIcon, XIcon } from 'lucide-vue-next'
import {
  KPI_TEMPLATES,
  defaultKpiParams,
  validateKpiParams,
  buildKpiScript,
  type KpiTemplate,
  type KpiParams
} from '~/utils/lua/kpiTemplates'

/**
 * "New KPI": pick a template, fill in its few parameters, get an editable Lua script.
 * Emits the script; the page puts it into the editor (nothing is saved here).
 */
const props = defineProps<{
  stations: Array<{ id: string; label: string }>
  defaultStationId?: string | null
}>()

const emit = defineEmits<{
  (e: 'create', payload: { name: string; description: string; code: string }): void
  (e: 'close'): void
}>()

const templateId = ref<KpiTemplate['id']>('oee')
const template = computed(() => KPI_TEMPLATES.find(t => t.id === templateId.value)!)
const params = ref<KpiParams>({})
const touched = ref(false)

const resetParams = () => {
  params.value = { ...defaultKpiParams(template.value), stationId: props.defaultStationId || props.stations[0]?.id || '' }
  touched.value = false
}
watch(templateId, resetParams, { immediate: true })

const errors = computed(() => validateKpiParams(template.value, params.value))
const visibleErrors = computed(() => (touched.value ? errors.value : {}))

function selectTemplate(id: KpiTemplate['id']) {
  templateId.value = id
}

function submit() {
  touched.value = true
  if (Object.keys(errors.value).length > 0) return
  const station = props.stations.find(s => s.id === params.value.stationId)
  emit('create', {
    name: `${template.value.name} – ${station?.label || params.value.stationId}`,
    description: template.value.description,
    code: buildKpiScript(template.value, params.value)
  })
}
</script>

<template>
  <div class="rounded-xl border border-primary/30 bg-card shadow-sm" data-testid="lua-kpi-wizard">
    <div class="flex items-center justify-between border-b px-4 py-2.5">
      <div class="flex items-center gap-2 text-sm font-semibold">
        <GaugeIcon class="h-4 w-4 text-primary" />
        New KPI from a template
      </div>
      <button type="button" class="rounded p-1 text-muted-foreground hover:bg-muted" aria-label="Close" @click="emit('close')">
        <XIcon class="h-4 w-4" />
      </button>
    </div>

    <div class="grid gap-4 p-4 md:grid-cols-5">
      <div class="space-y-1.5 md:col-span-2" role="radiogroup" aria-label="KPI template">
        <button
          v-for="t in KPI_TEMPLATES"
          :key="t.id"
          type="button"
          role="radio"
          :aria-checked="templateId === t.id"
          class="w-full rounded-lg border p-2.5 text-left transition-colors"
          :class="templateId === t.id ? 'border-primary bg-primary/5' : 'border-border/60 hover:bg-muted/40'"
          @click="selectTemplate(t.id)"
        >
          <div class="flex items-center justify-between text-sm font-medium">
            <span>{{ t.name }}</span>
            <span class="font-mono text-[10px] text-muted-foreground">{{ t.unit }}</span>
          </div>
          <p class="mt-0.5 text-xs text-muted-foreground">{{ t.description }}</p>
        </button>
      </div>

      <form class="space-y-3 md:col-span-3" novalidate @submit.prevent="submit">
        <div v-for="p in template.params" :key="p.key" class="space-y-1">
          <label :for="`kpi-param-${p.key}`" class="text-xs font-medium text-foreground">
            {{ p.label }}<span v-if="p.optional" class="font-normal text-muted-foreground"> (optional)</span>
          </label>
          <select
            v-if="p.type === 'station'"
            :id="`kpi-param-${p.key}`"
            v-model="params[p.key]"
            class="h-8 w-full rounded-md border border-input bg-background px-2 text-xs"
          >
            <option value="" disabled>Choose a station…</option>
            <option v-for="s in stations" :key="s.id" :value="s.id">{{ s.label }}</option>
          </select>
          <Input
            v-else
            :id="`kpi-param-${p.key}`"
            v-model="params[p.key]"
            :type="p.type === 'number' ? 'number' : 'text'"
            :step="p.type === 'number' ? 'any' : undefined"
            class="h-8 text-xs"
            :class="p.type === 'metric' ? 'font-mono' : ''"
          />
          <p v-if="visibleErrors[p.key]" class="text-xs text-destructive" role="alert">{{ visibleErrors[p.key] }}</p>
          <p v-else-if="p.help" class="text-[11px] text-muted-foreground">{{ p.help }}</p>
        </div>
        <p v-if="stations.length === 0" class="text-xs text-amber-500">No stations are loaded yet; the script needs one.</p>
        <div class="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" size="sm" class="h-8 text-xs" @click="emit('close')">Cancel</Button>
          <Button type="submit" size="sm" class="h-8 text-xs" data-testid="lua-kpi-create">Create script</Button>
        </div>
      </form>
    </div>
  </div>
</template>
