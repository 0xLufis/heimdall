<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  Bell,
  Check,
  CheckCheck,
  Smartphone,
  ExternalLink,
  Info,
  CheckCircle2,
  AlertTriangle,
  AlertOctagon,
  Clock
} from 'lucide-vue-next'
import { useHeimdallNotifications, type NotificationItem } from '~/composables/useHeimdallNotifications'

const router = useRouter()
const {
  notifications,
  unreadCount,
  isLoading,
  browserNotificationsGranted,
  requestBrowserNotificationPermission,
  fetchNotifications,
  markAsRead,
  markAllAsRead
} = useHeimdallNotifications()

const isOpen = ref(false)
const filterMode = ref<'all' | 'unread'>('all')

onMounted(() => {
  fetchNotifications()
})

const filteredNotifications = computed(() => {
  if (filterMode.value === 'unread') {
    return notifications.value.filter(n => !n.read)
  }
  return notifications.value
})

function toggleOpen() {
  isOpen.value = !isOpen.value
  if (isOpen.value) {
    fetchNotifications()
  }
}

function handleNotificationClick(item: NotificationItem) {
  if (!item.read) {
    markAsRead(item.id)
  }
  if (item.link) {
    isOpen.value = false
    router.push(item.link)
  }
}

async function handleEnableBrowserNotifications() {
  await requestBrowserNotificationPermission()
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
      return 'text-emerald-500 bg-emerald-500/10'
    case 'warning':
      return 'text-amber-500 bg-amber-500/10'
    case 'error':
    case 'critical':
      return 'text-rose-500 bg-rose-500/10'
    default:
      return 'text-sky-500 bg-sky-500/10'
  }
}

function formatRelativeTime(dateStr: string) {
  try {
    const diffSec = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000)
    if (diffSec < 60) return 'Just now'
    const diffMin = Math.floor(diffSec / 60)
    if (diffMin < 60) return `${diffMin}m ago`
    const diffHr = Math.floor(diffMin / 60)
    if (diffHr < 24) return `${diffHr}h ago`
    const diffDay = Math.floor(diffHr / 24)
    return `${diffDay}d ago`
  } catch {
    return dateStr
  }
}
</script>

<template>
  <div class="relative inline-block text-left">
    <!-- Bell Trigger Button -->
    <button
      type="button"
      @click="toggleOpen"
      class="relative p-2 rounded-xl text-muted-foreground hover:text-foreground hover:bg-muted/80 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-primary/40"
      aria-label="Notification Center"
      :aria-expanded="isOpen"
    >
      <Bell class="w-5 h-5" />
      <span
        v-if="unreadCount > 0"
        class="absolute top-1 right-1 flex items-center justify-center min-w-4 h-4 px-1 text-[10px] font-black text-white bg-rose-500 rounded-full shadow-xs ring-2 ring-background animate-pulse"
      >
        {{ unreadCount > 99 ? '99+' : unreadCount }}
      </span>
    </button>

    <!-- Backdrop when open -->
    <div
      v-if="isOpen"
      class="fixed inset-0 z-40"
      @click="isOpen = false"
    />

    <!-- Dropdown Popover Window -->
    <div
      v-if="isOpen"
      class="absolute right-0 mt-2 w-80 sm:w-96 rounded-2xl bg-card border border-border shadow-xl z-50 overflow-hidden flex flex-col text-foreground animate-in fade-in zoom-in-95 duration-150"
    >
      <!-- Popover Header -->
      <div class="p-3.5 border-b border-border flex items-center justify-between bg-muted/30">
        <div class="flex items-center gap-2">
          <h4 class="text-xs font-bold uppercase tracking-wider text-foreground">{{ $t('notifications.title') }}</h4>
          <span
            v-if="unreadCount > 0"
            class="px-2 py-0.5 text-[10px] font-bold rounded-full bg-primary/10 text-primary border border-primary/20"
          >
            {{ unreadCount }} {{ $t('notifications.unread') }}
          </span>
        </div>

        <button
          v-if="unreadCount > 0"
          type="button"
          @click="markAllAsRead"
          class="text-[11px] font-medium text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors cursor-pointer"
        >
          <CheckCheck class="w-3.5 h-3.5" />
          <span>{{ $t('notifications.markAllRead') }}</span>
        </button>
      </div>

      <!-- Android / Browser Notification Channel Prompt -->
      <div
        v-if="!browserNotificationsGranted"
        class="px-3.5 py-2.5 bg-primary/5 border-b border-border flex items-center justify-between gap-2"
      >
        <div class="flex items-center gap-2">
          <Smartphone class="w-4 h-4 text-primary shrink-0" />
          <span class="text-[11px] text-muted-foreground leading-tight">{{ $t('notifications.enableBrowserAlerts') }}</span>
        </div>
        <button
          type="button"
          @click="handleEnableBrowserNotifications"
          class="text-[10px] font-bold uppercase tracking-wider px-2.5 py-1 rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 transition-colors shrink-0 cursor-pointer"
        >
          {{ $t('common.enable') }}
        </button>
      </div>

      <!-- Filter Tabs -->
      <div class="flex items-center gap-1 p-2 border-b border-border bg-muted/10 text-xs">
        <button
          type="button"
          @click="filterMode = 'all'"
          :class="[
            'flex-1 py-1 rounded-lg text-center font-medium transition-colors cursor-pointer',
            filterMode === 'all' ? 'bg-background shadow-xs text-foreground font-bold border border-border' : 'text-muted-foreground hover:text-foreground'
          ]"
        >
          {{ $t('common.all') }} ({{ notifications.length }})
        </button>
        <button
          type="button"
          @click="filterMode = 'unread'"
          :class="[
            'flex-1 py-1 rounded-lg text-center font-medium transition-colors cursor-pointer',
            filterMode === 'unread' ? 'bg-background shadow-xs text-foreground font-bold border border-border' : 'text-muted-foreground hover:text-foreground'
          ]"
        >
          {{ $t('notifications.unread') }} ({{ unreadCount }})
        </button>
      </div>

      <!-- Notifications List -->
      <div class="max-h-80 overflow-y-auto divide-y divide-border/50">
        <div
          v-for="item in filteredNotifications"
          :key="item.id"
          @click="handleNotificationClick(item)"
          :class="[
            'p-3 flex items-start gap-3 transition-colors cursor-pointer hover:bg-muted/50 group',
            item.read ? 'opacity-70 bg-transparent' : 'bg-primary/5'
          ]"
        >
          <div :class="['p-1.5 rounded-lg shrink-0 mt-0.5', getIconColorClass(item.type)]">
            <component :is="getIcon(item.type)" class="w-3.5 h-3.5" />
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center justify-between gap-1">
              <span class="text-xs font-bold text-foreground truncate group-hover:text-primary transition-colors">
                {{ item.title }}
              </span>
              <span class="text-[10px] text-muted-foreground shrink-0 flex items-center gap-1">
                <Clock class="w-2.5 h-2.5" />
                {{ formatRelativeTime(item.timestamp) }}
              </span>
            </div>

            <p class="text-xs text-muted-foreground mt-0.5 line-clamp-2 leading-relaxed">
              {{ item.message }}
            </p>

            <div v-if="item.link" class="mt-1 flex items-center gap-1 text-[10px] font-semibold text-primary">
              <span>View context</span>
              <ExternalLink class="w-2.5 h-2.5" />
            </div>
          </div>

          <!-- Unread indicator -->
          <div v-if="!item.read" class="w-2 h-2 rounded-full bg-primary shrink-0 mt-1.5" />
        </div>

        <div v-if="filteredNotifications.length === 0" class="py-10 text-center text-xs text-muted-foreground">
          No notifications to display.
        </div>
      </div>
    </div>
  </div>
</template>
