<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { PaletteIcon, SunIcon, MoonIcon, MonitorIcon } from 'lucide-vue-next'

const colorMode = useColorMode()
</script>

<template>
  <div class="space-y-6">
    <Card class="border-border/80">
      <CardHeader>
        <CardTitle class="text-lg flex items-center gap-2">
          <PaletteIcon class="h-5 w-5 text-purple-400" />
          <span>Theme Appearance & Interface Preferences</span>
        </CardTitle>
        <CardDescription>
          Select your color mode and layout behavior across the Heimdall dashboard.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <div>
          <div class="text-xs font-semibold text-muted-foreground uppercase mb-3">Color Scheme</div>
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <button
              type="button"
              @click="colorMode.preference = 'light'"
              class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
              :class="colorMode.preference === 'light' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
            >
              <SunIcon class="h-8 w-8 text-amber-500" />
              <div class="font-bold text-sm">Light Mode</div>
            </button>

            <button
              type="button"
              @click="colorMode.preference = 'dark'"
              class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
              :class="colorMode.preference === 'dark' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
            >
              <MoonIcon class="h-8 w-8 text-indigo-500 dark:text-indigo-400" />
              <div class="font-bold text-sm">Dark Mode (Default)</div>
            </button>

            <button
              type="button"
              @click="colorMode.preference = 'system'"
              class="p-4 rounded-xl border-2 text-left transition-all flex flex-col items-center justify-center gap-3 cursor-pointer"
              :class="colorMode.preference === 'system' ? 'border-primary bg-primary/5 text-primary shadow-sm' : 'border-border bg-card hover:bg-muted/40'"
            >
              <MonitorIcon class="h-8 w-8 text-muted-foreground" />
              <div class="font-bold text-sm">System Default</div>
            </button>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
