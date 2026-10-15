<script setup lang="ts">
import { ref } from 'vue'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { XIcon, PlusIcon } from 'lucide-vue-next'

const props = defineProps<{
  modelValue: any
  isChild?: boolean
}>()

const emit = defineEmits(['update:modelValue', 'remove'])

const availableFields = {
  hardware: [
    { label: 'General', fields: ['general_query', 'name', 'serialNumber', 'modelNumber'] },
    { label: 'Purchase', fields: ['costInHuf', 'purchaseDate', 'supplier'] },
    { label: 'Taxonomy', fields: ['categories'] },
  ],
  software: [
    { label: 'General', fields: ['general_query', 'name', 'version', 'serialNumber'] },
    { label: 'Purchase', fields: ['costInHuf', 'purchaseDate', 'supplier'] },
  ]
}

const activeTab = ref<'hardware' | 'software'>('hardware'); // This should ideally be passed as a prop

function addCondition() {
  const current = props.modelValue
  if (!current.conditions) current.conditions = []
  current.conditions.push({ field: 'name', operator: 'contains', value: '' })
  emit('update:modelValue', current)
}

function removeCondition(index: number) {
  const current = props.modelValue
  current.conditions.splice(index, 1)
  emit('update:modelValue', current)
}
</script>

<template>
  <Card class="bg-card border border-border p-4 rounded-2xl shadow-sm text-sm text-foreground">
    <div class="flex items-center gap-2 mb-4">
      <Select v-model="modelValue.logic">
        <SelectTrigger class="w-28 h-8 bg-background border-border rounded-lg text-[10px] font-bold uppercase tracking-widest text-foreground">
          <SelectValue />
        </SelectTrigger>
        <SelectContent class="bg-card border-border">
          <SelectItem value="and">Match ALL</SelectItem>
          <SelectItem value="or">Match ANY</SelectItem>
        </SelectContent>
      </Select>
      <div class="h-px flex-grow bg-border"></div>
    </div>

    <div class="space-y-3">
      <div v-for="(item, index) in modelValue.conditions" :key="index" class="flex gap-2 items-center">
        <!-- Condition -->
        <template>
          <Select v-model="item.field">
            <SelectTrigger class="w-48 h-10 bg-background border-border rounded-lg text-xs font-bold uppercase tracking-wider text-foreground">
              <SelectValue placeholder="Select field..." />
            </SelectTrigger>
            <SelectContent class="bg-card border-border">
              <template v-for="group in availableFields[activeTab]" :key="group.label">
                <label class="text-xs text-muted-foreground px-2 py-1.5 font-bold">{{ group.label }}</label>
                <SelectItem v-for="field in group.fields" :key="field" :value="field">{{ field }}</SelectItem>
              </template>
            </SelectContent>
          </Select>
          
          <Select v-model="item.operator">
            <SelectTrigger class="w-40 h-10 bg-background border-border rounded-lg text-xs font-bold uppercase tracking-wider text-foreground">
              <SelectValue placeholder="Operator..." />
            </SelectTrigger>
            <SelectContent class="bg-card border-border">
              <SelectItem value="contains">Contains</SelectItem>
              <SelectItem value="equals">Equals</SelectItem>
              <SelectItem value="startsWith">Starts With</SelectItem>
              <SelectItem value="endsWith">Ends With</SelectItem>
              <SelectItem value="gt">Greater Than</SelectItem>
              <SelectItem value="lt">Less Than</SelectItem>
            </SelectContent>
          </Select>
          
          <Input v-model="item.value" class="h-10 bg-background border-border text-foreground rounded-lg" placeholder="Value..." />
          
          <Button @click="removeCondition(index)" variant="ghost" size="icon" class="h-8 w-8 text-muted-foreground hover:text-rose-500 flex-shrink-0">
            <XIcon class="h-4 w-4" />
          </Button>
        </template>
      </div>
    </div>
    
    <div class="flex items-center gap-3 mt-4 pt-4 border-t border-border">
      <Button @click="addCondition" variant="outline" size="sm" class="text-xs font-bold uppercase tracking-wider border-border bg-card text-foreground hover:bg-muted">
        <PlusIcon class="h-4 w-4 mr-2" /> Add Filter
      </Button>
    </div>
  </Card>
</template>
