<script setup lang="ts">
import { useHeimdallNotifications, type ToastItem } from '~/composables/useHeimdallNotifications'
import {
  Info,
  CheckCircle2,
  AlertTriangle,
  AlertOctagon,
  X,
  RotateCcw,
  ExternalLink
} from 'lucide-vue-next'

const { activeToasts, dismissToast } = useHeimdallNotifications()

async function handleUndo(toast: ToastItem) {
  if (toast.undoAction) {
    try {
      await toast.undoAction()
    } catch (err) {
      console.error('Error executing toast undo:', err)
    }
  }
  dismissToast(toast.id)
}

function getIcon(type?: string) {
  switch (type) {
    case 'success':
      return CheckCircle2
    case 'warning':
      return AlertTriangle
    case 'error':
    case 'critical':
      return AlertOctagon
    default:
      return Info
  }
}

function getIconColorClass(type?: string) {
  switch (type) {
    case 'success':
      return 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20'
    case 'warning':
      return 'text-amber-500 bg-amber-500/10 border-amber-500/20'
    case 'error':
    case 'critical':
      return 'text-rose-500 bg-rose-500/10 border-rose-500/20'
    default:
      return 'text-sky-500 bg-sky-500/10 border-sky-500/20'
  }
}

function getBorderClass(type?: string) {
  switch (type) {
    case 'success':
      return 'border-emerald-500/30 dark:border-emerald-500/40'
    case 'warning':
      return 'border-amber-500/30 dark:border-amber-500/40'
    case 'error':
    case 'critical':
      return 'border-rose-500/40 dark:border-rose-500/50 ring-1 ring-rose-500/20'
    default:
      return 'border-border'
  }
}
</script>

<template>
  <div
    class="fixed top-4 right-4 z-50 pointer-events-none flex flex-col gap-2.5 max-w-sm sm:max-w-md w-full px-3 sm:px-0"
    role="region"
    aria-label="Notifications"
  >
    <TransitionGroup
      enter-active-class="transform ease-out duration-300 transition"
      enter-from-class="translate-y-2 opacity-0 sm:translate-y-0 sm:translate-x-4"
      enter-to-class="translate-y-0 opacity-100 sm:translate-x-0"
      leave-active-class="transition ease-in duration-200"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0 scale-95"
    >
      <div
        v-for="toast in activeToasts"
        :key="toast.id"
        :class="[
          'pointer-events-auto rounded-xl p-3.5 shadow-lg border bg-card text-card-foreground backdrop-blur-md transition-all flex flex-col gap-2',
          getBorderClass(toast.type)
        ]"
      >
        <div class="flex items-start gap-3">
          <div :class="['p-1.5 rounded-lg border shrink-0', getIconColorClass(toast.type)]">
            <component :is="getIcon(toast.type)" class="w-4 h-4" />
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center justify-between gap-2">
              <h5 class="text-xs font-bold text-foreground truncate">
                {{ toast.title }}
              </h5>
              <button
                type="button"
                @click="dismissToast(toast.id)"
                class="text-muted-foreground hover:text-foreground p-1 rounded-md transition-colors shrink-0 cursor-pointer"
                aria-label="Dismiss toast"
              >
                <X class="w-3.5 h-3.5" />
              </button>
            </div>

            <p class="text-xs text-muted-foreground mt-0.5 leading-relaxed break-words">
              {{ toast.message }}
            </p>
          </div>
        </div>

        <!-- Action / Undo bar -->
        <div v-if="toast.undoAction || toast.link || toast.actionLabel" class="flex items-center justify-end gap-2 pt-1 border-t border-border/50">
          <button
            v-if="toast.undoAction"
            type="button"
            @click="handleUndo(toast)"
            class="text-[11px] font-semibold flex items-center gap-1 px-2.5 py-1 rounded-lg bg-muted text-foreground hover:bg-muted/80 transition-colors cursor-pointer"
          >
            <RotateCcw class="w-3 h-3 text-amber-500" />
            <span>{{ toast.actionLabel || $t('common.undo') }}</span>
          </button>

          <NuxtLink
            v-if="toast.link"
            :to="toast.link"
            @click="dismissToast(toast.id)"
            class="text-[11px] font-semibold flex items-center gap-1 px-2.5 py-1 rounded-lg bg-primary/10 text-primary hover:bg-primary/20 transition-colors cursor-pointer"
          >
            <span>{{ $t('common.viewDetails') }}</span>
            <ExternalLink class="w-3 h-3" />
          </NuxtLink>
        </div>
      </div>
    </TransitionGroup>
  </div>
</template>
