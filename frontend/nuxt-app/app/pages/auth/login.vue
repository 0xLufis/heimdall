<template>
  <div class="min-h-screen bg-gray-50 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
    <div class="sm:mx-auto sm:w-full sm:max-w-md">
      <div class="flex justify-center">
        <div class="w-12 h-12 bg-indigo-600 rounded-lg flex items-center justify-center text-white font-bold text-xl text-center">H</div>
      </div>
      <h2 class="mt-6 text-center text-3xl font-extrabold text-gray-900">Sign in to Heimdall</h2>
    </div>

    <div class="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
      <div class="bg-white py-8 px-4 shadow sm:rounded-lg sm:px-10">
        <form @submit.prevent="handleSignIn" class="space-y-6">
          <div>
            <label for="email" class="block text-sm font-medium text-gray-700">Login Identifier (Email or Username)</label>
            <div class="mt-1">
              <input v-model="email" id="email" name="email" type="text" required
                class="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm placeholder-gray-400 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm" />
            </div>
          </div>

          <div>
            <label for="password" class="block text-sm font-medium text-gray-700">Password</label>
            <div class="mt-1">
              <input v-model="password" id="password" name="password" type="password" autocomplete="current-password" required
                class="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm placeholder-gray-400 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm" />
            </div>
          </div>

          <div v-if="error" class="text-red-600 text-sm font-medium text-center">
            {{ error }}
          </div>

          <div>
            <button type="submit" :disabled="loading"
              class="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50">
              {{ loading ? 'Signing in...' : 'Sign in' }}
            </button>
          </div>
        </form>

        <div class="mt-6">
          <div class="relative">
            <div class="absolute inset-0 flex items-center">
              <div class="w-full border-t border-gray-300"></div>
            </div>
            <div class="relative flex justify-center text-sm">
              <span class="px-2 bg-white text-gray-500">Or</span>
            </div>
          </div>

          <div class="mt-6">
            <NuxtLink to="/auth/signup" class="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50">
              Create a new account
            </NuxtLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// Simple client for Better-Auth
import { authClient } from "~/utils/auth-client"

const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function handleSignIn() {
  loading.value = true
  error.value = ''
  try {
    const isEmail = email.value.includes('@')
    const { data, error: authError } = isEmail 
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
    loading.value = false
  }
}
</script>
