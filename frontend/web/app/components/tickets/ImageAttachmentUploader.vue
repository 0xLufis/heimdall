<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Upload, X, ZoomIn, ImageOff, Paperclip } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import type { TicketAttachment } from '~/types/maintenance'

const props = withDefaults(
  defineProps<{
    modelValue: TicketAttachment[]
    label?: string
    maxFiles?: number
  }>(),
  {
    label: 'Attachments',
    maxFiles: 10
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: TicketAttachment[]): void
}>()

const isDragging = ref(false)
const lightboxOpen = ref(false)
const lightboxSrc = ref('')
const lightboxName = ref('')
const fileInputRef = ref<HTMLInputElement | null>(null)

const ACCEPTED_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/svg+xml']

function generateId(): string {
  return `att_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`
}

function isAtLimit(): boolean {
  return props.modelValue.length >= props.maxFiles
}

async function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

async function processFiles(files: FileList | File[]) {
  const arr = Array.from(files)
  const allowed = arr.filter(f => ACCEPTED_TYPES.includes(f.type))
  const remaining = props.maxFiles - props.modelValue.length
  const toProcess = allowed.slice(0, remaining)

  const newAttachments: TicketAttachment[] = await Promise.all(
    toProcess.map(async file => {
      const url = await fileToBase64(file)
      return {
        id: generateId(),
        ticketId: '',
        fileName: file.name,
        contentType: file.type,
        fileSize: file.size,
        uploadedAt: new Date().toISOString(),
        url
      } satisfies TicketAttachment
    })
  )

  emit('update:modelValue', [...props.modelValue, ...newAttachments])
}

function onDragEnter(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function onDragOver(e: DragEvent) {
  e.preventDefault()
}

function onDragLeave(e: DragEvent) {
  // only reset when actually leaving the drop zone element
  const target = e.currentTarget as HTMLElement
  if (!target.contains(e.relatedTarget as Node)) {
    isDragging.value = false
  }
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false
  if (e.dataTransfer?.files) {
    processFiles(e.dataTransfer.files)
  }
}

function onFileInputChange(e: Event) {
  const input = e.target as HTMLInputElement
  if (input.files) processFiles(input.files)
  input.value = ''
}

function openPicker() {
  if (isAtLimit()) return
  fileInputRef.value?.click()
}

function removeAttachment(id: string) {
  emit('update:modelValue', props.modelValue.filter(a => a.id !== id))
}

function openLightbox(att: TicketAttachment) {
  lightboxSrc.value = att.url ?? ''
  lightboxName.value = att.fileName
  lightboxOpen.value = true
}

// Paste from clipboard
async function onWindowPaste(e: ClipboardEvent) {
  if (!e.clipboardData) return
  const files = Array.from(e.clipboardData.files).filter(f => ACCEPTED_TYPES.includes(f.type))
  const items = Array.from(e.clipboardData.items)
    .filter(it => ACCEPTED_TYPES.includes(it.type))
    .map(it => it.getAsFile())
    .filter(Boolean) as File[]

  const combined = files.length ? files : items
  if (combined.length) {
    e.preventDefault()
    processFiles(combined)
  }
}

onMounted(() => window.addEventListener('paste', onWindowPaste))
onUnmounted(() => window.removeEventListener('paste', onWindowPaste))

const thumbnailCount = computed(() => props.modelValue.length)
</script>

<template>
  <div class="space-y-3">
    <div v-if="label" class="flex items-center gap-2">
      <Paperclip class="w-4 h-4 text-slate-400" />
      <span class="text-xs font-bold uppercase tracking-wider text-slate-400">{{ label }}</span>
      <span class="text-[10px] font-mono text-slate-600">({{ thumbnailCount }}/{{ maxFiles }})</span>
    </div>

    <!-- Drop Zone -->
    <div
      class="relative border-2 border-dashed rounded-2xl p-6 text-center transition-colors duration-200 cursor-pointer"
      :class="[
        isDragging
          ? 'border-indigo-500 bg-indigo-950/30 text-indigo-400'
          : isAtLimit()
            ? 'border-slate-800 bg-slate-950/60 cursor-not-allowed opacity-50'
            : 'border-slate-700 bg-slate-950/60 hover:border-indigo-500/60 hover:bg-indigo-950/10 text-slate-500 hover:text-indigo-400'
      ]"
      @dragenter="onDragEnter"
      @dragover="onDragOver"
      @dragleave="onDragLeave"
      @drop="onDrop"
      @click="openPicker"
    >
      <input
        ref="fileInputRef"
        type="file"
        multiple
        accept="image/png,image/jpeg,image/webp,image/svg+xml"
        class="hidden"
        @change="onFileInputChange"
      />
      <Upload class="w-6 h-6 mx-auto mb-2 opacity-60" />
      <p class="text-xs font-semibold">
        {{ isAtLimit() ? 'Maximum attachments reached' : 'Drop images here, click to browse, or paste from clipboard' }}
      </p>
      <p class="text-[10px] text-slate-600 mt-1">PNG, JPEG, WebP, SVG</p>
    </div>

    <!-- Thumbnail Grid -->
    <div v-if="modelValue.length > 0" class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2">
      <div
        v-for="att in modelValue"
        :key="att.id"
        class="relative group aspect-square bg-slate-900 rounded-xl overflow-hidden border border-slate-800 hover:border-indigo-500/50 transition-colors"
      >
        <!-- Thumbnail -->
        <img
          v-if="att.url"
          :src="att.url"
          :alt="att.fileName"
          class="w-full h-full object-cover"
        />
        <div v-else class="w-full h-full flex items-center justify-center">
          <ImageOff class="w-6 h-6 text-slate-600" />
        </div>

        <!-- Overlay actions -->
        <div class="absolute inset-0 bg-slate-950/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
          <button
            class="p-1.5 bg-slate-800 hover:bg-indigo-600 rounded-lg transition-colors"
            title="View full size"
            @click.stop="openLightbox(att)"
          >
            <ZoomIn class="w-3.5 h-3.5 text-white" />
          </button>
          <button
            class="p-1.5 bg-slate-800 hover:bg-red-600 rounded-lg transition-colors"
            title="Remove"
            @click.stop="removeAttachment(att.id)"
          >
            <X class="w-3.5 h-3.5 text-white" />
          </button>
        </div>

        <!-- File name tooltip at bottom -->
        <div class="absolute bottom-0 inset-x-0 px-1.5 py-1 bg-slate-950/80 opacity-0 group-hover:opacity-100 transition-opacity">
          <p class="text-[9px] font-mono text-slate-300 truncate">{{ att.fileName }}</p>
        </div>
      </div>
    </div>

    <!-- Lightbox Dialog -->
    <Dialog v-model:open="lightboxOpen">
      <DialogContent class="max-w-4xl bg-slate-950 border-slate-800 p-2">
        <DialogHeader class="px-4 pt-4">
          <DialogTitle class="text-sm font-mono text-slate-300 truncate">{{ lightboxName }}</DialogTitle>
        </DialogHeader>
        <div class="flex items-center justify-center p-4 max-h-[80vh] overflow-auto">
          <img
            :src="lightboxSrc"
            :alt="lightboxName"
            class="max-w-full max-h-full object-contain rounded-xl"
          />
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
