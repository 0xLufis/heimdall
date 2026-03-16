<script setup lang="ts">
import { ref } from 'vue'
import { authClient } from "~/utils/auth-client"
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { GithubIcon, ChromeIcon, LaptopIcon } from 'lucide-vue-next'

definePageMeta({
  layout: false
})

const name = ref('')
const email = ref('')
const username = ref('')
const password = ref('')
const loading = ref(false)
const socialLoading = ref<string | null>(null)
const error = ref('')

async function handleSignUp() {
  loading.value = true
  error.value = ''
  try {
    const { error: authError } = await authClient.signUp.email({
      email: email.value,
      password: password.value,
      name: name.value,
      username: username.value,
      callbackURL: "/dashboard"
    })

    if (authError) {
      error.value = authError.message || 'Failed to sign up'
    } else {
      navigateTo('/dashboard')
    }
  } catch (e) {
    error.value = 'An unexpected error occurred'
  } finally {
    loading.value = false
  }
}

async function handleSocialSignIn(provider: 'github' | 'google' | 'microsoft') {
  socialLoading.value = provider
  try {
    await authClient.signIn.social({
      provider,
      callbackURL: "/dashboard"
    })
  } catch (e) {
    error.value = `Failed to sign in with ${provider}`
  } finally {
    socialLoading.value = null
  }
}
</script>

<template>
  <div class="min-h-screen bg-indigo-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8 relative overflow-hidden">
    <!-- Background Accents -->
    <div class="absolute -top-24 -left-24 w-96 h-96 bg-indigo-600 rounded-full blur-[120px] opacity-20 animate-pulse"></div>
    <div class="absolute -bottom-24 -right-24 w-96 h-96 bg-purple-600 rounded-full blur-[120px] opacity-20 animate-pulse"></div>

    <div class="sm:mx-auto sm:w-full sm:max-w-md relative z-10">
      <div class="flex justify-center">
        <div class="w-16 h-16 bg-white/10 backdrop-blur-md rounded-2xl flex items-center justify-center text-white font-black text-3xl border border-white/20 shadow-2xl">H</div>
      </div>
      <h2 class="mt-8 text-center text-4xl font-black tracking-tight text-white uppercase">Initialize</h2>
      <p class="mt-2 text-center text-xs font-bold text-indigo-300 uppercase tracking-[0.2em] opacity-70">Create your operator profile</p>
    </div>

    <div class="mt-10 sm:mx-auto sm:w-full sm:max-w-md relative z-10 px-4">
      <div class="bg-white rounded-3xl shadow-2xl overflow-hidden border border-indigo-100/10">
        <div class="px-8 pt-10 pb-8">
          <form @submit.prevent="handleSignUp" class="space-y-4">
            <div class="grid grid-cols-2 gap-4">
              <div class="space-y-1.5">
                <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Full Name</Label>
                <Input v-model="name" type="text" placeholder="John Doe" required class="rounded-xl py-5 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold text-xs" />
              </div>
              <div class="space-y-1.5">
                <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Username</Label>
                <Input v-model="username" type="text" placeholder="jdoe" required class="rounded-xl py-5 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold text-xs" />
              </div>
            </div>

            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Email Address</Label>
              <Input v-model="email" type="email" placeholder="john@example.com" required class="rounded-xl py-5 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold text-xs" />
            </div>

            <div class="space-y-1.5">
              <Label class="text-[10px] font-black text-gray-400 uppercase tracking-widest ml-1">Access Token (Password)</Label>
              <Input v-model="password" type="password" required class="rounded-xl py-5 border-gray-100 bg-gray-50 focus:bg-white transition-all font-bold text-xs" />
            </div>

            <div v-if="error" class="bg-rose-50 border border-rose-100 text-rose-600 text-[10px] font-black uppercase tracking-widest py-3 px-4 rounded-xl text-center">
              {{ error }}
            </div>

            <Button type="submit" :disabled="loading" class="w-full bg-indigo-600 hover:bg-indigo-700 text-white rounded-2xl py-6 h-auto shadow-xl shadow-indigo-100 transition-all font-black uppercase tracking-widest text-xs mt-4">
              {{ loading ? 'Provisioning...' : 'Create Account' }}
            </Button>
          </form>

          <div class="mt-8">
            <div class="relative">
              <div class="absolute inset-0 flex items-center">
                <div class="w-full border-t border-gray-100"></div>
              </div>
              <div class="relative flex justify-center text-[10px]">
                <span class="px-4 bg-white text-gray-400 font-black uppercase tracking-widest">External Identity</span>
              </div>
            </div>

            <div class="mt-6 grid grid-cols-3 gap-3">
              <Button @click="handleSocialSignIn('github')" variant="outline" class="rounded-2xl py-6 h-auto border-gray-100 hover:bg-gray-50 group">
                <GithubIcon class="h-5 w-5 text-gray-600 group-hover:scale-110 transition-transform" />
              </Button>
              <Button @click="handleSocialSignIn('google')" variant="outline" class="rounded-2xl py-6 h-auto border-gray-100 hover:bg-gray-50 group">
                <ChromeIcon class="h-5 w-5 text-gray-600 group-hover:scale-110 transition-transform" />
              </Button>
              <Button @click="handleSocialSignIn('microsoft')" variant="outline" class="rounded-2xl py-6 h-auto border-gray-100 hover:bg-gray-50 group">
                <LaptopIcon class="h-5 w-5 text-gray-600 group-hover:scale-110 transition-transform" />
              </Button>
            </div>
          </div>
        </div>
        
        <div class="bg-gray-50/50 px-8 py-6 border-t border-gray-100 text-center">
          <p class="text-[10px] font-black text-gray-400 uppercase tracking-widest">
            Already registered? 
            <NuxtLink to="/auth/login" class="text-indigo-600 hover:text-indigo-800 ml-1">Access Terminal</NuxtLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
