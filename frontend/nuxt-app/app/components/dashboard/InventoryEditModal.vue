<script setup lang="ts">
import AssetTabbedEditor from './AssetTabbedEditor.vue'

const props = defineProps<{
  open: boolean
  item: any | null
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'close'): void
  (e: 'save', updatedItem: any): void
}>()
</script>

<template>
  <AssetTabbedEditor
    :open="open"
    :item="item"
    mode="edit"
    @update:open="emit('update:open', $event)"
    @close="emit('close')"
    @save="emit('save', $event)"
  />
</template>
