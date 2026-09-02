<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  initialQuery?: string
  availableKeys: string[]
}>()

const emit = defineEmits<{
  search: [query: string]
  close: []
}>()

const name = ref('')
const technology = ref('')
const costCenter = ref('')
const manufacturer = ref('')
const supplier = ref('')
const customTags = ref<{ key: string, value: string }[]>([])

// Initialize from initialQuery if possible
onMounted(() => {
  if (props.initialQuery) {
    const tagMatches = props.initialQuery.matchAll(/(\w+):"?([^"\s]+)"?/g)
    for (const match of tagMatches) {
      const key = match[1].toLowerCase()
      const val = match[2]
      if (key === 'name') name.value = val
      else if (key === 'technology') technology.value = val
      else if (key === 'costcenter') costCenter.value = val
      else if (key === 'manufacturer') manufacturer.value = val
      else if (key === 'supplier') supplier.value = val
      else customTags.value.push({ key: match[1], value: val })
    }
  }
})

function addCustomTag() {
  customTags.value.push({ key: '', value: '' })
}

function removeCustomTag(index: number) {
  customTags.value.splice(index, 1)
}

function handleSearch() {
  let queryParts = []
  if (name.value) queryParts.push(`name:"${name.value}"`)
  if (technology.value) queryParts.push(`technology:"${technology.value}"`)
  if (costCenter.value) queryParts.push(`costcenter:"${costCenter.value}"`)
  if (manufacturer.value) queryParts.push(`manufacturer:"${manufacturer.value}"`)
  if (supplier.value) queryParts.push(`supplier:"${supplier.value}"`)
  
  for (const tag of customTags.value) {
    if (tag.key && tag.value) {
      queryParts.push(`${tag.key}:"${tag.value}"`)
    }
  }
  
  emit('search', queryParts.join(' '))
}
</script>

<template>
  <div class="space-y-4 py-4">
    <div class="grid grid-cols-2 gap-4">
      <div class="space-y-2">
        <Label for="name">Name</Label>
        <Input id="name" v-model="name" placeholder="Component name..." />
      </div>
      <div class="space-y-2">
        <Label for="technology">Technology</Label>
        <Input id="technology" v-model="technology" placeholder="e.g. Vision, Sensor..." />
      </div>
      <div class="space-y-2">
        <Label for="costcenter">Cost Center</Label>
        <Input id="costcenter" v-model="costCenter" placeholder="e.g. Engineering..." />
      </div>
      <div class="space-y-2">
        <Label for="manufacturer">Manufacturer</Label>
        <Input id="manufacturer" v-model="manufacturer" placeholder="Manufacturer name..." />
      </div>
    </div>

    <Separator />

    <div class="space-y-2">
      <div class="flex items-center justify-between">
        <Label>Custom Tags</Label>
        <Button variant="outline" size="sm" @click="addCustomTag">
          <Icon name="i-lucide-plus" class="mr-2 size-4" />
          Add Tag
        </Button>
      </div>
      
      <div v-for="(tag, index) in customTags" :key="index" class="flex gap-2">
        <Select v-model="tag.key">
          <SelectTrigger class="w-[180px]">
            <SelectValue placeholder="Key" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="key in availableKeys" :key="key" :value="key">
              {{ key }}
            </SelectItem>
          </SelectContent>
        </Select>
        <Input v-model="tag.value" placeholder="Value" class="flex-1" />
        <Button variant="ghost" size="icon" @click="removeCustomTag(index)">
          <Icon name="i-lucide-x" class="size-4" />
        </Button>
      </div>
      <div v-if="customTags.length === 0" class="text-sm text-muted-foreground italic">
        No custom tags added.
      </div>
    </div>

    <div class="flex justify-end pt-4">
      <Button @click="handleSearch">
        <Icon name="i-lucide-search" class="mr-2 size-4" />
        Search
      </Button>
    </div>
  </div>
</template>
