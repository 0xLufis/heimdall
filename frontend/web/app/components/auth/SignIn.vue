<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { authClient } from "~/utils/auth-client"

const email = ref('')
const password = ref('')
const showPassword = ref(false)
const isLoading = ref(false)
const error = ref('')

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

async function handleSocialSignIn(provider: 'github' | 'google' | 'microsoft') {
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
  <form class="grid gap-6" @submit.prevent="onSubmit">
    <div class="grid grid-cols-3 gap-3">
      <Button @click="handleSocialSignIn('github')" variant="outline" type="button" class="w-full">
        <Icon name="i-lucide-github" class="size-4" />
      </Button>
      <Button @click="handleSocialSignIn('google')" variant="outline" type="button" class="w-full">
        <Icon name="i-lucide-chrome" class="size-4" />
      </Button>
      <Button @click="handleSocialSignIn('microsoft')" variant="outline" type="button" class="w-full">
        <Icon name="i-lucide-laptop" class="size-4" />
      </Button>
    </div>
    
    <Separator label="Or continue with" />
    
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
