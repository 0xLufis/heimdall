<script setup lang="ts">
import { ref } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import {
  UserIcon,
  ShieldIcon,
  KeyIcon,
  PaletteIcon,
  UserCheckIcon
} from 'lucide-vue-next'
import SettingsProfileTab from '~/components/settings/SettingsProfileTab.vue'
import SettingsRolesTab from '~/components/settings/SettingsRolesTab.vue'
import SettingsSecurityTab from '~/components/settings/SettingsSecurityTab.vue'
import SettingsAppearanceTab from '~/components/settings/SettingsAppearanceTab.vue'
import SettingsPersonasTab from '~/components/settings/SettingsPersonasTab.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})

const { userRole, activeOrg, isPersonaSimulationAllowed } = useAuthSession()
const activeTab = ref('profile')
</script>

<template>
  <div class="space-y-6 max-w-6xl mx-auto pb-12">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-600 dark:text-indigo-400">
          <UserIcon class="size-6" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-foreground">
            User Settings &amp; Preferences
          </h1>
          <p class="text-sm text-muted-foreground mt-0.5">
            Manage your account profile, role capabilities, security credentials, and workspace appearance
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <RoleBadge :role="userRole" />
        <Badge variant="outline" class="font-mono text-xs bg-muted border-border text-foreground">
          Org: {{ activeOrg?.name || 'Heimdall Engineering' }}
        </Badge>
      </div>
    </div>

    <!-- Modular Tabs Container -->
    <Tabs v-model="activeTab" class="w-full space-y-6">
      <TabsList class="grid grid-cols-2 md:grid-cols-5 gap-2 bg-muted/60 p-1 rounded-xl border border-border h-auto">
        <TabsTrigger value="profile" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors cursor-pointer">
          <UserIcon class="h-4 w-4" />
          <span>Profile</span>
        </TabsTrigger>
        <TabsTrigger value="roles" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors cursor-pointer">
          <ShieldIcon class="h-4 w-4" />
          <span>Roles &amp; Scopes</span>
        </TabsTrigger>
        <TabsTrigger value="security" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors cursor-pointer">
          <KeyIcon class="h-4 w-4" />
          <span>Security &amp; API</span>
        </TabsTrigger>
        <TabsTrigger value="appearance" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors cursor-pointer">
          <PaletteIcon class="h-4 w-4" />
          <span>Appearance</span>
        </TabsTrigger>
        <TabsTrigger v-if="isPersonaSimulationAllowed" value="personas" class="flex items-center gap-2 text-xs font-medium py-2 rounded-lg data-[state=active]:bg-indigo-600 data-[state=active]:text-white transition-colors cursor-pointer">
          <UserCheckIcon class="h-4 w-4" />
          <span>Personas</span>
        </TabsTrigger>
      </TabsList>

      <TabsContent value="profile" class="space-y-6">
        <SettingsProfileTab />
      </TabsContent>

      <TabsContent value="roles" class="space-y-6">
        <SettingsRolesTab />
      </TabsContent>

      <TabsContent value="security" class="space-y-6">
        <SettingsSecurityTab />
      </TabsContent>

      <TabsContent value="appearance" class="space-y-6">
        <SettingsAppearanceTab />
      </TabsContent>

      <TabsContent v-if="isPersonaSimulationAllowed" value="personas" class="space-y-6">
        <SettingsPersonasTab />
      </TabsContent>
    </Tabs>
  </div>
</template>
