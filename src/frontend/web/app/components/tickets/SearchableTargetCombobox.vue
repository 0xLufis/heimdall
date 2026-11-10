<script setup lang="ts">
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'

defineProps<any>()
defineEmits<{
  (e: 'update:modelValue', val: string): void
  (e: 'select', item: TargetItem | { id: string; label: string; isCustom: true; raw?: any }): void
  (e: 'clear'): void
}>()
</script>

<template>
  <SearchableTargetCombobox
    v-bind="$props"
    @update:model-value="(v: string) => $emit('update:modelValue', v)"
    @select="(item: any) => $emit('select', item)"
    @clear="() => $emit('clear')"
  />
</template>
