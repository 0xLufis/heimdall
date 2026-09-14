<script setup lang="ts">
import { computed } from 'vue'
import {
  Dialog, DialogContent
} from '~/components/ui/dialog'
import MachineGroupManager from './MachineGroupManager.vue'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'groupSelected', groupId: string): void
}>()

const isOpen = computed({
  get: () => props.open,
  set: (v) => { if (!v) emit('close') }
})
</script>

<template>
  <Dialog v-model:open="isOpen">
    <DialogContent class="max-w-4xl bg-slate-950 border-slate-800 text-slate-100 p-6 shadow-2xl">
      <MachineGroupManager
        :is-modal="true"
        @close="emit('close')"
        @group-selected="emit('groupSelected', $event)"
      />
    </DialogContent>
  </Dialog>
</template>
