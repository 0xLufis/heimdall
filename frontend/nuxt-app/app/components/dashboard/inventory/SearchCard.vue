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
  <Card class="bg-slate-900/50 border border-slate-800 p-4 rounded-2xl shadow-inner text-sm">
    <div class="flex items-center gap-2 mb-4">
      <Select v-model="modelValue.logic">
        <SelectTrigger class="w-28 h-8 bg-slate-800 border-slate-700 rounded-lg text-[10px] font-bold uppercase tracking-widest">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="and">Match ALL</SelectItem>
          <SelectItem value="or">Match ANY</SelectItem>
        </SelectContent>
      </Select>
      <div class="h-px flex-grow bg-slate-800/50"></div>
    </div>

    <div class="space-y-3">
      <div v-for="(item, index) in modelValue.conditions" :key="index" class="flex gap-2 items-center">
        <!-- Condition -->
        <template>
          <Select v-model="item.field">
            <SelectTrigger class="w-48 h-10 bg-slate-800 border-slate-700 rounded-lg text-xs font-bold uppercase tracking-wider">
              <SelectValue placeholder="Select field..." />
            </SelectTrigger>
            <SelectContent>
              <template v-for="group in availableFields[activeTab]" :key="group.label">
                <label class="text-xs text-slate-500 px-2 py-1.5 font-bold">{{ group.label }}</label>
                <SelectItem v-for="field in group.fields" :key="field" :value="field">{{ field }}</SelectItem>
              </template>
            </SelectContent>
          </Select>
          
          <Select v-model="item.operator">
            <SelectTrigger class="w-40 h-10 bg-slate-800 border-slate-700 rounded-lg text-xs font-bold uppercase tracking-wider">
              <SelectValue placeholder="Operator..." />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="contains">Contains</SelectItem>
              <SelectItem value="equals">Equals</SelectItem>
              <SelectItem value="startsWith">Starts With</SelectItem>
              <SelectItem value="endsWith">Ends With</SelectItem>
              <SelectItem value="gt">Greater Than</SelectItem>
              <SelectItem value="lt">Less Than</SelectItem>
            </SelectContent>
          </Select>
          
          <Input v-model="item.value" class="h-10 bg-slate-800 border-slate-700 rounded-lg" placeholder="Value..." />
          
          <Button @click="removeCondition(index)" variant="ghost" size="icon" class="h-8 w-8 text-slate-500 hover:text-rose-500 flex-shrink-0">
            <XIcon class="h-4 w-4" />
          </Button>
        </template>
      </div>
    </div>
    
    <div class="flex items-center gap-3 mt-4 pt-4 border-t border-slate-800/50">
      <Button @click="addCondition" variant="outline" size="sm" class="text-xs font-bold uppercase tracking-wider border-slate-700 hover:bg-slate-800">
        <PlusIcon class="h-4 w-4 mr-2" /> Add Filter
      </Button>
    </div>
  </Card>
</template>
