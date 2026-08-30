<script setup lang="ts">
import AssetTabbedEditor from './AssetTabbedEditor.vue'

const props = defineProps<{
  type: 'hardware' | 'software'
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'close'): void
  (e: 'save', newItem: any): void
}>()
</script>

<template>
  <AssetTabbedEditor
    :open="open"
    :initial-type="type"
    mode="create"
    @update:open="emit('update:open', $event)"
    @close="emit('close')"
    @save="emit('save', $event)"
  />
</template>
