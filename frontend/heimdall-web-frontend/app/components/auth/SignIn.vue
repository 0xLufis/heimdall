<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { authClient } from "~/utils/auth-client"

const email = ref('')
const password = ref('')
const isLoading = ref(false)
const error = ref('')

async function onSubmit(event: Event) {
  event.preventDefault()
  if (!email.value || !password.value)
    return

  isLoading.value = true
  error.value = ''

  try {
    const isEmail = email.value.includes('@')
    const { error: authError } = isEmail 
      ? await authClient.signIn.email({
          email: email.value,
          password: password.value,
          callbackURL: "/dashboard"
        })
      : await authClient.signIn.username({
          username: email.value,
          password: password.value,
          callbackURL: "/dashboard"
        })
    
    if (authError) {
      error.value = authError.message || 'Failed to sign in'
    } else {
      navigateTo('/dashboard')
    }
  } catch (e: any) {
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
  <form class="grid gap-6" @submit="onSubmit">
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
      <PasswordInput id="password" v-model="password" required />
    </div>

    <div v-if="error" class="bg-destructive/10 border border-destructive/20 text-destructive text-[10px] font-bold uppercase tracking-widest py-3 px-4 rounded-lg text-center">
      {{ error }}
    </div>

    <Button type="submit" class="w-full font-bold uppercase tracking-widest py-6 h-auto" :disabled="isLoading">
      <Loader2 v-if="isLoading" class="mr-2 h-4 w-4 animate-spin" />
      {{ isLoading ? 'Authenticating...' : 'Sign In' }}
    </Button>
  </form>
  <div class="mt-4 text-center text-sm text-muted-foreground">
    New operator?
    <NuxtLink to="/auth/signup" class="underline underline-offset-4 font-bold text-foreground">
      Initialize Account
    </NuxtLink>
  </div>
</template>
