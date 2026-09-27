<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { authClient } from "~/utils/auth-client"

const email = ref('')
const password = ref('')
const showPassword = ref(false)
const isLoading = ref(false)
const isSsoLoading = ref(false)
const error = ref('')

interface SsoStatus {
  enabled: boolean
  provider: string
  providerName: string
  tenantId: string
  isConfigured: boolean
  allowMockSimulation: boolean
}

const ssoStatus = ref<SsoStatus | null>(null)

onMounted(async () => {
  try {
    const res = await $fetch<SsoStatus>('/api/auth/sso/status')
    ssoStatus.value = res
  } catch (e) {
    // Fallback if status endpoint is offline
    ssoStatus.value = {
      enabled: true,
      provider: 'microsoft',
      providerName: 'Microsoft Entra ID (Azure SSO)',
      tenantId: '72f988bf-86f1-41af-91ab-2d7cd011db47',
      isConfigured: false,
      allowMockSimulation: true
    }
  }
})

async function onSubmit(event: Event) {
  event.preventDefault()
  console.log('[SignIn] onSubmit triggered with:', { email: email.value, hasPassword: !!password.value, passLen: password.value?.length })
  if (!email.value || !password.value) {
    console.warn('[SignIn] Missing email or password, aborting')
    return
  }

  isLoading.value = true
  error.value = ''

  try {
    const isEmail = email.value.includes('@')
    console.log('[SignIn] Submitting:', { email: email.value, isEmail })
    const res = isEmail 
      ? await authClient.signIn.email({
          email: email.value,
          password: password.value
        })
      : await authClient.signIn.username({
          username: email.value,
          password: password.value
        })
    
    console.log('[SignIn] Response:', res)
    if (res?.error) {
      error.value = res.error.message || 'Failed to sign in'
    } else {
      const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
      authSession.value = { authenticated: true, user: res?.data?.user }
      await navigateTo('/dashboard')
    }
  } catch (e: any) {
    console.error('[SignIn] Caught error:', e)
    error.value = 'An unexpected error occurred'
  } finally {
    isLoading.value = false
  }
}

async function handleAzureSso() {
  isSsoLoading.value = true
  error.value = ''

  try {
    if (ssoStatus.value?.isConfigured) {
      // Live Azure AD credentials configured - trigger Better-Auth OAuth flow
      await authClient.signIn.social({
        provider: 'microsoft',
        callbackURL: '/dashboard'
      })
    } else if (ssoStatus.value?.allowMockSimulation) {
      // Sandbox / dev environment - use simulated Entra ID claims
      const res = await $fetch<any>('/api/auth/sso/simulate', {
        method: 'POST',
        body: {
          email: email.value.trim() || undefined
        }
      })

      if (res?.success) {
        const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
        authSession.value = { authenticated: true, user: res.user }
        await navigateTo('/dashboard')
      } else {
        error.value = 'Microsoft Entra ID authentication failed'
      }
    } else {
      await authClient.signIn.social({
        provider: 'microsoft',
        callbackURL: '/dashboard'
      })
    }
  } catch (e: any) {
    console.error('[SignIn] Microsoft SSO error:', e)
    error.value = e?.message || 'Failed to authenticate via Microsoft Entra ID'
  } finally {
    isSsoLoading.value = false
  }
}

async function handleSocialSignIn(provider: 'github' | 'google') {
  try {
    await authClient.signIn.social({
      provider,
      callbackURL: "/dashboard"
    })
  } catch (e) {
    error.value = `Failed to sign in with ${provider}`
  }
}
</script>

<template>
  <form class="grid gap-5" @submit.prevent="onSubmit">
    <!-- Primary Enterprise SSO Option: Microsoft Entra ID -->
    <div class="space-y-2">
      <button
        id="btn-azure-sso"
        type="button"
        @click="handleAzureSso"
        :disabled="isSsoLoading || isLoading"
        class="w-full relative group flex items-center justify-between px-4 py-3 rounded-xl border border-border/80 bg-background/95 hover:bg-muted/60 hover:border-primary/50 text-foreground transition-all duration-200 shadow-sm disabled:opacity-60 disabled:pointer-events-none cursor-pointer"
      >
        <div class="flex items-center gap-3">
          <!-- Microsoft 4-Color Corporate Logo -->
          <svg class="size-5 shrink-0" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect x="1" y="1" width="9" height="9" fill="#F25022"/>
            <rect x="11" y="1" width="9" height="9" fill="#7FBA00"/>
            <rect x="1" y="11" width="9" height="9" fill="#00A4EF"/>
            <rect x="11" y="11" width="9" height="9" fill="#FFB900"/>
          </svg>
          <div class="text-left">
            <div class="text-xs font-bold tracking-tight">Sign in with Microsoft Entra ID</div>
            <div class="text-[10px] text-muted-foreground font-mono">
              {{ ssoStatus?.isConfigured ? 'Enterprise Azure Cloud SSO' : 'Plant Directory SSO (Sandbox)' }}
            </div>
          </div>
        </div>

        <div class="flex items-center gap-1.5">
          <Loader2 v-if="isSsoLoading" class="size-4 animate-spin text-primary" />
          <span 
            v-else 
            class="text-[9px] font-mono font-bold uppercase tracking-wider px-2 py-0.5 rounded-full border border-sky-500/30 bg-sky-500/10 text-sky-400"
          >
            SSO
          </span>
        </div>
      </button>

      <!-- Secondary Developer & Social Providers -->
      <div class="grid grid-cols-2 gap-2">
        <Button 
          @click="handleSocialSignIn('github')" 
          variant="outline" 
          type="button" 
          size="sm" 
          class="w-full text-xs font-medium gap-2 h-9"
        >
          <Icon name="i-lucide-github" class="size-3.5" />
          <span>GitHub</span>
        </Button>
        <Button 
          @click="handleSocialSignIn('google')" 
          variant="outline" 
          type="button" 
          size="sm" 
          class="w-full text-xs font-medium gap-2 h-9"
        >
          <Icon name="i-lucide-chrome" class="size-3.5" />
          <span>Google</span>
        </Button>
      </div>
    </div>
    
    <!-- Visual Divider -->
    <div class="relative my-1">
      <div class="absolute inset-0 flex items-center">
        <span class="w-full border-t border-border/70" />
      </div>
      <div class="relative flex justify-center text-xs uppercase">
        <span class="bg-card px-2.5 text-muted-foreground font-mono text-[10px] tracking-widest font-semibold">
          Or with operator token
        </span>
      </div>
    </div>
    
    <div class="grid gap-2">
      <Label for="email" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">
        Identity Identifier
      </Label>
      <Input
        id="email"
        v-model="email"
        type="text"
        placeholder="Email or Username"
        :disabled="isLoading"
        auto-capitalize="none"
        auto-complete="email"
        auto-correct="off"
        required
      />
    </div>
    <div class="grid gap-2">
      <div class="flex items-center">
        <Label for="password" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">
          Access Token
        </Label>
        <NuxtLink
          to="/auth/forgot-password"
          class="ml-auto inline-block text-xs underline underline-offset-4 opacity-70 hover:opacity-100"
        >
          Recovery?
        </NuxtLink>
      </div>
      <div class="relative">
        <Input
          id="password"
          v-model="password"
          :type="showPassword ? 'text' : 'password'"
          class="pr-10"
          placeholder="Enter your password"
          required
        />
        <Button
          type="button"
          variant="ghost"
          size="icon"
          class="absolute right-0 top-0 h-full px-2 py-2 hover:bg-transparent"
          @click="showPassword = !showPassword"
        >
          <Icon
            v-if="showPassword"
            name="i-lucide-eye"
            class="size-4"
            aria-hidden="true"
          />
          <Icon v-else name="i-lucide-eye-off" class="size-4" aria-hidden="true" />
          <span class="sr-only">
            {{ showPassword ? "Show password" : "Hide password" }}
          </span>
        </Button>
      </div>
    </div>

    <div v-if="error" class="bg-destructive/10 border border-destructive/20 text-destructive text-[10px] font-bold uppercase tracking-widest py-3 px-4 rounded-lg text-center">
      {{ error }}
    </div>

    <button
      id="btn-login-submit"
      type="submit"
      @click="onSubmit"
      class="w-full font-bold uppercase tracking-widest py-3 px-4 rounded-xl bg-primary text-primary-foreground hover:bg-primary/90 transition-all flex items-center justify-center gap-2 cursor-pointer shadow-md disabled:opacity-50 disabled:pointer-events-none"
      :disabled="isLoading"
    >
      <Loader2 v-if="isLoading" class="mr-2 h-4 w-4 animate-spin" />
      {{ isLoading ? 'Authenticating...' : 'Sign In' }}
    </button>
  </form>
  <div class="mt-4 text-center text-sm text-muted-foreground">
    New operator?
    <NuxtLink to="/auth/signup" class="underline underline-offset-4 font-bold text-foreground">
      Initialize Account
    </NuxtLink>
  </div>
</template>
