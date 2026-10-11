<script setup lang="ts">
import { ref, computed } from 'vue'
import type { SidebarMenuButtonVariants } from '~/components/ui/sidebar'
import type { NavGroup } from '~/types/nav'
import { useSidebar } from '~/components/ui/sidebar'
import { useAppI18n } from '~/composables/useAppI18n'

const props = withDefaults(defineProps<{
  item: NavGroup
  size?: SidebarMenuButtonVariants['size']
}>(), {
  size: 'default',
})

const { setOpenMobile } = useSidebar()
const { t } = useAppI18n()

const navKeyMap: Record<string, string> = {
  'Dashboard': 'nav.dashboard',
  'Inventory': 'nav.inventory',
  'Machines': 'nav.machines',
  'Machine Groups': 'nav.machineGroups',
  'Client PCs': 'nav.clientPcs',
  'Plant Map': 'nav.plantMap',
  'Live Telemetry': 'nav.telemetry',
  'Fleet Analytics': 'nav.analytics',
  'Telemetry Templates': 'nav.telemetryTemplates',
  'Telemetry Config': 'nav.telemetryConfig',
  'Tickets': 'nav.tickets',
  'Delegations': 'nav.delegations',
  'Users & Roles': 'nav.usersAndRoles',
  'Organizations': 'nav.organizations',
  'Security Groups': 'nav.securityGroups',
  'System Governance': 'nav.systemGovernance',
  'Identity Studio': 'nav.identityStudio',
  'User Settings': 'nav.userSettings',
  'Help & Support': 'nav.helpAndSupport'
}

const localizedTitle = computed(() => {
  const key = navKeyMap[props.item.title]
  return key ? t(key, props.item.title) : props.item.title
})

const getLocalizedSubTitle = (title: string) => {
  const key = navKeyMap[title]
  return key ? t(key, title) : title
}

const openCollapsible = ref(false)
</script>

<template>
  <SidebarMenu>
    <Collapsible
      :key="item.title"
      v-model:open="openCollapsible"
      as-child
      class="group/collapsible"
    >
      <SidebarMenuItem>
        <CollapsibleTrigger as-child>
          <SidebarMenuButton :tooltip="localizedTitle" :size="size">
            <Icon :name="item.icon || ''" mode="svg" />
            <span>{{ localizedTitle }}</span>
            <span v-if="item.new" class="rounded-md bg-[#adfa1d] px-1.5 py-0.5 text-xs text-black leading-none no-underline group-hover:no-underline">
              {{ t('common.new', 'New') }}
            </span>
            <Icon name="i-lucide-chevron-right" class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
          </SidebarMenuButton>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SidebarMenuSub>
            <SidebarMenuSubItem
              v-for="subItem in item.children"
              :key="subItem.title"
            >
              <SidebarMenuSubButton as-child :data-active="subItem.link === $route.path">
                <NuxtLink :to="subItem.link" @click="setOpenMobile(false)">
                  <span>{{ getLocalizedSubTitle(subItem.title) }}</span>
                  <span v-if="subItem.new" class="rounded-md bg-[#adfa1d] px-1.5 py-0.5 text-xs text-black leading-none no-underline group-hover:no-underline">
                    {{ t('common.new', 'New') }}
                  </span>
                </NuxtLink>
              </SidebarMenuSubButton>
            </SidebarMenuSubItem>
          </SidebarMenuSub>
        </CollapsibleContent>
      </SidebarMenuItem>
    </Collapsible>
  </SidebarMenu>
</template>

<style scoped>

</style>
