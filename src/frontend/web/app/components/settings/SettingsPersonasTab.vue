<script setup lang="ts">
import { useAuthSession } from '~/composables/useAuthSession'
import RoleBadge from '~/components/dashboard/RoleBadge.vue'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { UserCheckIcon, CheckCircle2Icon } from 'lucide-vue-next'

const {
  simulatedPersona,
  setSimulatedPersona,
  clearSimulatedPersona,
  DEMO_PERSONAS
} = useAuthSession()
</script>

<template>
  <div class="space-y-6">
    <Card class="border-border/80">
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle class="text-lg flex items-center gap-2">
              <UserCheckIcon class="h-5 w-5 text-cyan-400" />
              <span>Developer &amp; QA Persona Simulation Sandbox</span>
            </CardTitle>
            <CardDescription>
              Instantly switch between role personas to test and verify RBAC permissions and UI features live.
            </CardDescription>
          </div>
          <Button 
            variant="outline" 
            size="sm" 
            @click="clearSimulatedPersona" 
            :disabled="!simulatedPersona"
          >
            Reset to Real Session
          </Button>
        </div>
      </CardHeader>
      <CardContent class="space-y-4">
        <div v-if="simulatedPersona" class="p-3 rounded-lg bg-cyan-500/15 border border-cyan-500/30 text-cyan-400 text-xs flex items-center justify-between">
          <div class="flex items-center gap-2">
            <CheckCircle2Icon class="h-4 w-4 shrink-0" />
            <span>Currently Simulating: <strong>{{ simulatedPersona.name }}</strong></span>
          </div>
          <RoleBadge :role="simulatedPersona.role" />
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          <div
            v-for="p in DEMO_PERSONAS"
            :key="p.id"
            class="p-3.5 rounded-lg border transition-all flex flex-col justify-between gap-3 bg-card"
            :class="simulatedPersona?.id === p.id ? 'border-primary ring-1 ring-primary/30' : 'border-border/60 hover:border-border'"
          >
            <div>
              <div class="flex items-center justify-between gap-2">
                <span class="font-bold text-sm">{{ p.name }}</span>
                <RoleBadge :role="p.role" class="scale-90 origin-right" />
              </div>
              <div class="font-mono text-[11px] text-muted-foreground mt-0.5">{{ p.email }}</div>
              <p class="text-xs text-muted-foreground mt-2 line-clamp-2">{{ p.description }}</p>
            </div>
            <Button 
              size="sm" 
              variant="outline"
              class="w-full text-xs cursor-pointer"
              :class="simulatedPersona?.id === p.id ? 'bg-primary text-primary-foreground hover:bg-primary/90' : ''"
              @click="setSimulatedPersona(p)"
            >
              {{ simulatedPersona?.id === p.id ? 'Active Simulation' : 'Simulate Role' }}
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
