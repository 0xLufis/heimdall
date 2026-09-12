<script setup lang="ts">
import { useSidebar } from '~/components/ui/sidebar'
import { useAuthSession } from '~/composables/useAuthSession'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'

defineProps<{
  user?: {
    name?: string
    email?: string
    avatar?: string
    role?: string
  }
}>()

const { isMobile, setOpenMobile } = useSidebar()
const {
  user: authUser,
  userRole,
  simulatedPersona,
  setSimulatedPersona,
  clearSimulatedPersona,
  signOut,
  DEMO_PERSONAS
} = useAuthSession()

const colorMode = useColorMode()

async function handleLogout() {
  await signOut()
}
</script>

<template>
  <SidebarMenu>
    <SidebarMenuItem>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <SidebarMenuButton
            size="lg"
            class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground h-14"
          >
            <Avatar class="h-8 w-8 rounded-lg shrink-0">
              <AvatarImage :src="user?.avatar || (authUser as any)?.image || ''" :alt="user?.name || authUser?.name || 'User'" />
              <AvatarFallback class="rounded-lg bg-primary text-primary-foreground text-xs font-bold">
                {{ (user?.name || authUser?.name || 'U').charAt(0).toUpperCase() }}
              </AvatarFallback>
            </Avatar>
            <div class="grid flex-1 text-left text-sm leading-tight min-w-0">
              <span class="truncate font-semibold text-foreground">{{ user?.name || authUser?.name || 'User' }}</span>
              <div class="flex items-center gap-1 mt-0.5">
                <RoleBadge :role="user?.role || userRole" class="scale-90 origin-left" />
              </div>
            </div>
            <Icon name="i-lucide-chevrons-up-down" class="ml-auto size-4 opacity-50 shrink-0" />
          </SidebarMenuButton>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          class="min-w-64 w-[--radix-dropdown-menu-trigger-width] rounded-lg p-1.5"
          :side="isMobile ? 'bottom' : 'right'"
          align="end"
        >
          <DropdownMenuLabel class="p-2 font-normal bg-muted/40 rounded-md mb-1 border border-border/40">
            <div class="flex items-start gap-2.5 text-left text-sm">
              <Avatar class="h-9 w-9 rounded-lg shrink-0 mt-0.5">
                <AvatarImage :src="user?.avatar || (authUser as any)?.image || ''" :alt="user?.name || authUser?.name || 'User'" />
                <AvatarFallback class="rounded-lg bg-primary text-primary-foreground font-bold">
                  {{ (user?.name || authUser?.name || 'U').charAt(0).toUpperCase() }}
                </AvatarFallback>
              </Avatar>
              <div class="grid flex-1 text-left text-xs leading-tight min-w-0">
                <span class="truncate font-bold text-foreground text-sm">{{ user?.name || authUser?.name || 'User' }}</span>
                <span class="truncate text-muted-foreground mt-0.5 text-[11px]">{{ user?.email || authUser?.email }}</span>
                <div class="flex items-center gap-1.5 mt-1.5">
                  <RoleBadge :role="user?.role || userRole" />
                </div>
              </div>
            </div>
          </DropdownMenuLabel>

          <DropdownMenuSeparator />

          <DropdownMenuGroup>
            <!-- User Settings link -->
            <DropdownMenuItem as-child>
              <NuxtLink to="/dashboard/settings" @click="setOpenMobile(false)" class="flex items-center gap-2 cursor-pointer">
                <Icon name="i-lucide-settings" class="h-4 w-4 text-primary" />
                <span class="font-medium">User Settings</span>
              </NuxtLink>
            </DropdownMenuItem>

            <!-- Theme Submenu -->
            <DropdownMenuSub>
              <DropdownMenuSubTrigger class="flex items-center gap-2 cursor-pointer">
                <Icon name="i-lucide-palette" class="h-4 w-4 text-purple-400" />
                <span>Theme: <strong class="capitalize font-semibold">{{ colorMode.preference }}</strong></span>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent class="min-w-36">
                <DropdownMenuItem @click="colorMode.preference = 'light'" class="flex items-center gap-2 cursor-pointer">
                  <Icon name="i-lucide-sun" class="h-4 w-4 text-amber-500" />
                  <span>Light</span>
                  <Icon v-if="colorMode.preference === 'light'" name="i-lucide-check" class="ml-auto h-3.5 w-3.5 text-primary" />
                </DropdownMenuItem>
                <DropdownMenuItem @click="colorMode.preference = 'dark'" class="flex items-center gap-2 cursor-pointer">
                  <Icon name="i-lucide-moon" class="h-4 w-4 text-indigo-400" />
                  <span>Dark</span>
                  <Icon v-if="colorMode.preference === 'dark'" name="i-lucide-check" class="ml-auto h-3.5 w-3.5 text-primary" />
                </DropdownMenuItem>
                <DropdownMenuItem @click="colorMode.preference = 'system'" class="flex items-center gap-2 cursor-pointer">
                  <Icon name="i-lucide-monitor" class="h-4 w-4 text-slate-400" />
                  <span>System</span>
                  <Icon v-if="colorMode.preference === 'system'" name="i-lucide-check" class="ml-auto h-3.5 w-3.5 text-primary" />
                </DropdownMenuItem>
              </DropdownMenuSubContent>
            </DropdownMenuSub>

            <!-- Quick Persona Switcher Submenu -->
            <DropdownMenuSub>
              <DropdownMenuSubTrigger class="flex items-center gap-2 cursor-pointer">
                <Icon name="i-lucide-user-check" class="h-4 w-4 text-cyan-400" />
                <span>Switch Persona</span>
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent class="w-72 max-h-80 overflow-y-auto p-1">
                <div class="px-2 py-1 text-[10px] uppercase font-bold text-muted-foreground tracking-wider">
                  Simulate Role Privileges
                </div>
                <DropdownMenuItem 
                  v-for="p in DEMO_PERSONAS" 
                  :key="p.id" 
                  @click="setSimulatedPersona(p)"
                  class="flex items-center justify-between gap-2 text-xs py-1.5 cursor-pointer"
                  :class="{ 'bg-primary/10 font-bold': simulatedPersona?.id === p.id }"
                >
                  <span class="truncate">{{ p.name }}</span>
                  <RoleBadge :role="p.role" class="scale-75 origin-right shrink-0" />
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem 
                  @click="clearSimulatedPersona"
                  class="flex items-center gap-2 text-xs text-muted-foreground cursor-pointer"
                  :disabled="!simulatedPersona"
                >
                  <Icon name="i-lucide-rotate-ccw" class="h-3.5 w-3.5" />
                  <span>Reset to Real User</span>
                </DropdownMenuItem>
              </DropdownMenuSubContent>
            </DropdownMenuSub>
          </DropdownMenuGroup>

          <DropdownMenuSeparator />

          <DropdownMenuItem @click="handleLogout" class="text-destructive focus:bg-destructive focus:text-destructive-foreground flex items-center gap-2 cursor-pointer">
            <Icon name="i-lucide-log-out" class="h-4 w-4" />
            <span>Log out</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </SidebarMenuItem>
  </SidebarMenu>
</template>

<style scoped>
</style>
