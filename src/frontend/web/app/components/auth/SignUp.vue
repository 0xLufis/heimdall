<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { authClient } from "~/utils/auth-client"

const name = ref('')
const email = ref('')
const username = ref('')
const password = ref('')
const isLoading = ref(false)
const error = ref('')

async function onSubmit(event: Event) {
  event.preventDefault()
  if (!email.value || !password.value || !name.value || !username.value)
    return

  isLoading.value = true
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
    <div class="grid grid-cols-2 gap-4">
      <div class="grid gap-2">
        <Label for="name" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">Name</Label>
        <Input id="name" v-model="name" placeholder="John Doe" required :disabled="isLoading" />
      </div>
      <div class="grid gap-2">
        <Label for="username" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">Username</Label>
        <Input id="username" v-model="username" placeholder="jdoe" required :disabled="isLoading" />
      </div>
    </div>
    
    <div class="grid gap-2">
      <Label for="email" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">Email</Label>
      <Input id="email" v-model="email" type="email" placeholder="name@example.com" required :disabled="isLoading" />
    </div>

    <div class="grid gap-2">
      <Label for="password" class="text-xs uppercase font-bold tracking-widest text-muted-foreground">Access Token</Label>
      <PasswordInput id="password" v-model="password" required :disabled="isLoading" />
    </div>

    <div v-if="error" class="bg-destructive/10 border border-destructive/20 text-destructive text-[10px] font-bold uppercase tracking-widest py-3 px-4 rounded-lg text-center">
      {{ error }}
    </div>

    <Button type="submit" class="w-full font-bold uppercase tracking-widest py-6 h-auto" :disabled="isLoading">
      <Loader2 v-if="isLoading" class="mr-2 h-4 w-4 animate-spin" />
      {{ isLoading ? 'Provisioning...' : 'Initialize Account' }}
    </Button>

    <div class="relative">
      <div class="absolute inset-0 flex items-center">
        <span class="w-full border-t" />
      </div>
      <div class="relative flex justify-center text-xs uppercase">
        <span class="bg-background px-2 text-muted-foreground font-bold tracking-widest">
          External Identity
        </span>
      </div>
    </div>

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
  </form>
  
  <div class="mt-4 text-center text-sm text-muted-foreground">
    Already registered?
    <NuxtLink to="/auth/login" class="underline underline-offset-4 font-bold text-foreground">
      Access Terminal
    </NuxtLink>
  </div>
</template>
