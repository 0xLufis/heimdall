<script setup lang="ts">
import { computed } from 'vue'
import type { SidebarMenuButtonVariants } from '~/components/ui/sidebar'
import type { NavLink } from '~/types/nav'
import { useSidebar } from '~/components/ui/sidebar'
import { useRbacPermission } from '~/composables/useRbacPermission'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { Lock } from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  item: NavLink
  size?: SidebarMenuButtonVariants['size']
}>(), {
  size: 'default',
})

const { setOpenMobile } = useSidebar()
const { checkCapability } = useRbacPermission()

const isPermitted = computed(() => {
  if (!props.item.requiredCapability) return true
  return checkCapability(props.item.requiredCapability)
})

const isHidden = computed(() => {
  return props.item.hideWhenUnauthorized && !isPermitted.value
})
</script>

<template>
  <SidebarMenu v-if="!isHidden">
    <SidebarMenuItem>
      <!-- When unauthorized and not hidden: render disabled with lock badge & tooltip -->
      <RbacTooltip
        v-if="!isPermitted"
        :disabled="true"
        :tooltip="item.requiredTooltip || 'Requires elevated privilege to access this section.'"
        side="right"
      >
        <div
          class="flex w-full items-center gap-2 overflow-hidden rounded-md p-2 text-left text-sm text-slate-500 opacity-60 cursor-not-allowed select-none transition-colors"
        >
          <Icon :name="item.icon || ''" class="size-4 shrink-0 text-slate-500" />
          <span class="truncate">{{ item.title }}</span>
          <Lock class="ml-auto size-3.5 text-amber-400 shrink-0" />
        </div>
      </RbacTooltip>

      <!-- When permitted: render standard interactive link -->
      <SidebarMenuButton v-else as-child :tooltip="item.title" :size="size" :data-active="item.link === $route.path">
        <NuxtLink 
          :to="item.link" 
          :external="item.external || (item.link && item.link.startsWith('/admin/studio'))"
          :target="item.target || (item.link && item.link.startsWith('/admin/studio') ? '_blank' : undefined)"
          @click="setOpenMobile(false)"
        >
          <Icon :name="item.icon || ''" />
          <span>{{ item.title }}</span>
          <span v-if="item.new" class="rounded-md bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 px-1.5 py-0.5 text-xs leading-none no-underline group-hover:no-underline">
            New
          </span>
        </NuxtLink>
      </SidebarMenuButton>
    </SidebarMenuItem>
  </SidebarMenu>
</template>

<style scoped>

</style>
