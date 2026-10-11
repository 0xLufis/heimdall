<script setup lang="ts">
import { ref } from 'vue'
import { KeyRound, ArrowLeft, Mail, ShieldAlert, CheckCircle } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'

definePageMeta({
  layout: false
})

const emailOrUsername = ref('')
const recoveryKey = ref('')
const isSubmitted = ref(false)
const isLoading = ref(false)
const activeTab = ref<'email' | 'key'>('email')

async function handleReset(e: Event) {
  e.preventDefault()
  if (!emailOrUsername.value && !recoveryKey.value) return

  isLoading.value = true
  // Simulate dispatch of token reset
  setTimeout(() => {
    isLoading.value = false
    isSubmitted.value = true
  }, 600)
}
</script>

<template>
  <div class="min-h-screen bg-background flex flex-col justify-center items-center p-6 selection:bg-primary selection:text-primary-foreground">
    <div class="w-full max-w-md bg-card border border-border rounded-3xl shadow-2xl overflow-hidden p-8">
      
      <NuxtLink to="/auth/login" class="inline-flex items-center gap-2 text-xs font-black uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors mb-6">
        <ArrowLeft class="w-4 h-4" />
        Return to Login
      </NuxtLink>

      <div class="flex items-center gap-3 mb-6">
        <div class="p-3 rounded-2xl bg-muted text-foreground border border-border">
          <KeyRound class="w-6 h-6" />
        </div>
        <div>
          <h1 class="text-2xl font-black uppercase tracking-tight text-foreground">
            Access Recovery
          </h1>
          <p class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest mt-0.5">
            Security Protocol Terminal Reset
          </p>
        </div>
      </div>

      <div v-if="isSubmitted" class="p-6 bg-emerald-500/10 border border-emerald-500/30 rounded-2xl text-center space-y-3 animate-in fade-in zoom-in-95 duration-200">
        <div class="w-12 h-12 rounded-full bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto">
          <CheckCircle class="w-6 h-6" />
        </div>
        <h3 class="text-sm font-black uppercase tracking-tight text-emerald-700 dark:text-emerald-300">
          Recovery Token Dispatched
        </h3>
        <p class="text-xs text-muted-foreground">
          If an identity matches the identifier, authorization reset instructions have been forwarded.
        </p>
        <Button @click="isSubmitted = false" variant="outline" class="w-full mt-4 text-xs font-bold uppercase tracking-widest border-emerald-500/30 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-500/10">
          Submit Another Request
        </Button>
      </div>

      <form v-else @submit="handleReset" class="space-y-6">
        <div class="p-1 bg-muted rounded-xl border border-border flex gap-1">
          <Button 
            type="button" 
            variant="ghost" 
            size="sm" 
            @click="activeTab = 'email'"
            :class="activeTab === 'email' ? 'bg-card text-foreground shadow-sm border border-border' : 'text-muted-foreground hover:text-foreground'"
            class="flex-1 rounded-lg text-[10px] font-black uppercase"
          >
            <Mail class="w-3.5 h-3.5 mr-1.5" />
            Email Dispatch
          </Button>
          <Button 
            type="button" 
            variant="ghost" 
            size="sm" 
            @click="activeTab = 'key'"
            :class="activeTab === 'key' ? 'bg-card text-foreground shadow-sm border border-border' : 'text-muted-foreground hover:text-foreground'"
            class="flex-1 rounded-lg text-[10px] font-black uppercase"
          >
            <ShieldAlert class="w-3.5 h-3.5 mr-1.5" />
            Emergency Key
          </Button>
        </div>

        <div v-if="activeTab === 'email'" class="space-y-2">
          <Label class="text-xs uppercase font-bold tracking-widest text-muted-foreground">
            Registered Email or Username
          </Label>
          <Input 
            v-model="emailOrUsername" 
            type="text" 
            placeholder="operator@factory.domain"
            required
            class="bg-background border-border rounded-xl h-11 text-foreground placeholder:text-muted-foreground"
          />
        </div>

        <div v-else class="space-y-2">
          <Label class="text-xs uppercase font-bold tracking-widest text-muted-foreground">
            256-Bit Emergency Master Key
          </Label>
          <Input 
            v-model="recoveryKey" 
            type="password" 
            placeholder="XXXX-XXXX-XXXX-XXXX"
            required
            class="bg-background border-border rounded-xl h-11 text-foreground font-mono placeholder:text-muted-foreground"
          />
        </div>

        <Button 
          type="submit" 
          :disabled="isLoading"
          class="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-black uppercase tracking-widest py-6 h-auto rounded-2xl shadow-md border border-primary/20"
        >
          {{ isLoading ? 'Verifying Identity...' : 'Dispatch Reset Token' }}
        </Button>
      </form>
    </div>
  </div>
</template>
