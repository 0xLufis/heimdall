<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useLuaExtensions } from '~/composables/useLuaExtensions'
import type { LuaUiTargetZone, LuaUiExtension } from '~/types/lua'
import { SparklesIcon, AlertCircleIcon, CheckCircle2Icon, Loader2Icon } from 'lucide-vue-next'

const props = withDefaults(
  defineProps<{
    zone: LuaUiTargetZone
    context?: Record<string, any>
  }>(),
  {
    context: () => ({})
  }
)

const emit = defineEmits<{
  (e: 'actionInvoked', result: any): void
}>()

const { extensions, loading, fetchExtensions, invokeAction } = useLuaExtensions()
const activeActionId = ref<string | null>(null)
const invocationFeedback = ref<{ id: string; success: boolean; message: string } | null>(null)

onMounted(async () => {
  await fetchExtensions(props.zone)
})

const zoneExtensions = computed(() => {
  return extensions.value.filter(e => {
    return (
      e.targetZone === props.zone ||
      String(e.targetZone).toLowerCase() === String(props.zone).toLowerCase()
    )
  })
})

const actionButtons = computed(() =>
  zoneExtensions.value.filter(
    e => e.extensionType === 'ActionButton' || (e.extensionType as any) === 0
  )
)

const badges = computed(() =>
  zoneExtensions.value.filter(
    e => e.extensionType === 'Badge' || (e.extensionType as any) === 1
  )
)

const banners = computed(() =>
  zoneExtensions.value.filter(
    e => e.extensionType === 'BannerAlert' || (e.extensionType as any) === 3
  )
)

async function handleActionClick(ext: LuaUiExtension) {
  activeActionId.value = ext.id
  invocationFeedback.value = null

  try {
    const res = await invokeAction(ext.id, props.context)
    if (res.success) {
      invocationFeedback.value = {
        id: ext.id,
        success: true,
        message: typeof res.returnValue === 'string'
          ? res.returnValue
          : (res.returnValue?.status || 'Action dispatched successfully via Lua')
      }
    } else {
      invocationFeedback.value = {
        id: ext.id,
        success: false,
        message: res.error || 'Execution failed'
      }
    }
    emit('actionInvoked', res)
  } finally {
    activeActionId.value = null
  }
}
</script>

<template>
  <div v-if="zoneExtensions.length > 0" class="dynamic-lua-extensions space-y-3">
    <!-- Dynamic Alert Banners -->
    <div v-if="banners.length > 0" class="space-y-2">
      <div
        v-for="b in banners"
        :key="b.id"
        class="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-3 text-primary text-sm"
      >
        <SparklesIcon class="h-4 w-4 shrink-0 mt-0.5" />
        <div>
          <h5 class="font-semibold leading-none tracking-tight mb-1">{{ b.title }}</h5>
          <div v-if="b.description" class="text-xs opacity-90">{{ b.description }}</div>
        </div>
      </div>
    </div>

    <!-- Dynamic Status Badges -->
    <div v-if="badges.length > 0" class="flex flex-wrap items-center gap-2">
      <Badge
        v-for="badge in badges"
        :key="badge.id"
        variant="outline"
        class="border-indigo-500/30 bg-indigo-500/10 text-indigo-400 gap-1.5 py-0.5 px-2 text-xs font-medium"
      >
        <SparklesIcon class="h-3 w-3" />
        {{ badge.title }}
      </Badge>
    </div>

    <!-- Dynamic Action Buttons -->
    <div v-if="actionButtons.length > 0" class="flex flex-wrap items-center gap-2">
      <Button
        v-for="btn in actionButtons"
        :key="btn.id"
        size="sm"
        variant="outline"
        class="border-dashed border-primary/40 hover:border-primary gap-1.5 text-xs h-8"
        :disabled="activeActionId === btn.id"
        @click="handleActionClick(btn)"
      >
        <Loader2Icon v-if="activeActionId === btn.id" class="h-3.5 w-3.5 animate-spin" />
        <SparklesIcon v-else class="h-3.5 w-3.5 text-primary" />
        <span>{{ btn.title }}</span>
      </Button>
    </div>

    <!-- Feedback Notice -->
    <div
      v-if="invocationFeedback"
      class="text-xs p-2 rounded-md flex items-center gap-2 transition-all duration-300"
      :class="invocationFeedback.success ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' : 'bg-red-500/10 text-red-400 border border-red-500/20'"
    >
      <CheckCircle2Icon v-if="invocationFeedback.success" class="h-3.5 w-3.5 shrink-0" />
      <AlertCircleIcon v-else class="h-3.5 w-3.5 shrink-0" />
      <span>{{ invocationFeedback.message }}</span>
    </div>
  </div>
</template>
