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
  <div class="min-h-screen bg-slate-950 flex flex-col justify-center items-center p-6 selection:bg-indigo-500 selection:text-white">
    <div class="w-full max-w-md bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl overflow-hidden p-8">
      
      <NuxtLink to="/auth/login" class="inline-flex items-center gap-2 text-xs font-black uppercase tracking-widest text-slate-500 hover:text-slate-300 transition-colors mb-6">
        <ArrowLeft class="w-4 h-4" />
        Return to Login
      </NuxtLink>

      <div class="flex items-center gap-3 mb-6">
        <div class="p-3 rounded-2xl bg-indigo-600/10 text-indigo-400 border border-indigo-500/20">
          <KeyRound class="w-6 h-6" />
        </div>
        <div>
          <h1 class="text-2xl font-black uppercase tracking-tight text-slate-100">
            Access Recovery
          </h1>
          <p class="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">
            Security Protocol Terminal Reset
          </p>
        </div>
      </div>

      <div v-if="isSubmitted" class="p-6 bg-emerald-950/20 border border-emerald-500/30 rounded-2xl text-center space-y-3 animate-in fade-in zoom-in-95 duration-200">
        <div class="w-12 h-12 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center mx-auto">
          <CheckCircle class="w-6 h-6" />
        </div>
        <h3 class="text-sm font-black uppercase tracking-tight text-emerald-200">
          Recovery Token Dispatched
        </h3>
        <p class="text-xs text-slate-400">
          If an identity matches the identifier, authorization reset instructions have been forwarded.
        </p>
        <Button @click="isSubmitted = false" variant="outline" class="w-full mt-4 text-xs font-bold uppercase tracking-widest border-emerald-500/30 text-emerald-300">
          Submit Another Request
        </Button>
      </div>

      <form v-else @submit="handleReset" class="space-y-6">
        <div class="p-1 bg-slate-950 rounded-xl border border-slate-800 flex gap-1">
          <Button 
            type="button" 
            variant="ghost" 
            size="sm" 
            @click="activeTab = 'email'"
            :class="activeTab === 'email' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
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
            :class="activeTab === 'key' ? 'bg-indigo-600 text-white shadow-lg' : 'text-slate-500 hover:text-slate-300'"
            class="flex-1 rounded-lg text-[10px] font-black uppercase"
          >
            <ShieldAlert class="w-3.5 h-3.5 mr-1.5" />
            Emergency Key
          </Button>
        </div>

        <div v-if="activeTab === 'email'" class="space-y-2">
          <Label class="text-xs uppercase font-bold tracking-widest text-slate-400">
            Registered Email or Username
          </Label>
          <Input 
            v-model="emailOrUsername" 
            type="text" 
            placeholder="operator@factory.domain"
            required
            class="bg-slate-950 border-slate-800 rounded-xl h-11 text-slate-100 placeholder:text-slate-600"
          />
        </div>

        <div v-else class="space-y-2">
          <Label class="text-xs uppercase font-bold tracking-widest text-slate-400">
            256-Bit Emergency Master Key
          </Label>
          <Input 
            v-model="recoveryKey" 
            type="password" 
            placeholder="XXXX-XXXX-XXXX-XXXX"
            required
            class="bg-slate-950 border-slate-800 rounded-xl h-11 text-slate-100 font-mono placeholder:text-slate-600"
          />
        </div>

        <Button 
          type="submit" 
          :disabled="isLoading"
          class="w-full bg-indigo-600 hover:bg-indigo-700 text-white font-black uppercase tracking-widest py-6 h-auto rounded-2xl shadow-xl shadow-indigo-600/20"
        >
          {{ isLoading ? 'Verifying Identity...' : 'Dispatch Reset Token' }}
        </Button>
      </form>
    </div>
  </div>
</template>
