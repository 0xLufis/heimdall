<script setup lang="ts">
import { computed, resolveComponent } from 'vue'
import type { NavGroup, NavLink, NavSectionTitle } from '~/types/nav'
import { navMenu, navMenuBottom } from '~/constants/menus'
import { authClient } from "~/utils/auth-client"
import { useAppSettings } from '~/composables/useAppSettings'

/**
 * Resolves the appropriate component for a given navigation item.
 * @param {NavLink | NavGroup | NavSectionTitle} item - The navigation item to resolve.
 * @returns {any} The resolved component.
 */
function resolveNavItemComponent(item: NavLink | NavGroup | NavSectionTitle): any {
  if ('children' in item)
    return resolveComponent('LayoutSidebarNavGroup')

  return resolveComponent('LayoutSidebarNavLink')
}

const authSession = authClient.useSession()

/**
 * Computed property for the user's email. Defaults to '...' if not available.
 * @type {ComputedRef<string>}
 */
const userEmail = computed(() => authSession?.data?.value?.user?.email ?? '...')
/**
 * Computed property for the user's name. Defaults to 'User' if not available.
 * @type {ComputedRef<string>}
 */
const userName = computed(() => authSession?.data?.value?.user?.name ?? 'User')
/**
 * Computed property for the user's avatar image URL. Defaults to a placeholder if not available.
 * @type {ComputedRef<string>}
 */
const userAvatar = computed(() => authSession?.data?.value?.user?.image || '')

/**
 * Static array of teams or organizations to display in the sidebar header.
 */
const teams: {
  name: string
  logo: string
  plan: string
}[] = [
  {
    name: 'Heimdall',
    logo: 'i-lucide-shield',
    plan: 'Monitoring System',
  }
]

/**
 * Computed property representing the current authenticated user's details.
 * @type {ComputedRef<{ name: string; email: string; avatar: string }>}
 */
const user = computed(() => ({
  name: userName.value,
  email: userEmail.value,
  avatar: userAvatar.value,
}))

const { sidebar } = useAppSettings()
</script>

<template>
  <Sidebar :collapsible="sidebar?.collapsible" :side="sidebar?.side" :variant="sidebar?.variant">
    <SidebarHeader>
      <LayoutSidebarNavHeader :teams="teams" />
      <Search />
    </SidebarHeader>
    <SidebarContent>
      <SidebarGroup v-for="(nav, indexGroup) in navMenu" :key="indexGroup">
        <SidebarGroupLabel v-if="nav.heading">
          {{ nav.heading }}
        </SidebarGroupLabel>
        <component :is="resolveNavItemComponent(item)" v-for="(item, index) in nav.items" :key="index" :item="item" />
      </SidebarGroup>
      <SidebarGroup class="mt-auto">
        <component :is="resolveNavItemComponent(item)" v-for="(item, index) in navMenuBottom" :key="index" :item="item" size="sm" />
      </SidebarGroup>
    </SidebarContent>
    <SidebarFooter>
      <LayoutSidebarNavFooter :user="user" />
    </SidebarFooter>
    <SidebarRail />
  </Sidebar>
</template>

<style scoped>

</style>
