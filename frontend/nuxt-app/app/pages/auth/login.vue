<script setup lang="ts">
/**
 * Defines the page metadata.
 * Sets the layout to `false` to use a custom layout provided by `LayoutAuth`.
 */
definePageMeta({
  layout: false,
})
</script>

<template>
  <!-- 
    Login page layout using the Auth layout component.
    The `reverse` prop is used to control the visual layout direction.
  -->
  <LayoutAuth reverse>
    <div class="grid mx-auto max-w-sm gap-6">
      <div class="grid gap-2 text-center">
        <h1 class="text-3xl font-bold tracking-tight uppercase">
          Welcome back
        </h1>
        <p class="text-balance text-xs font-bold uppercase tracking-widest text-muted-foreground opacity-70">
          Identify yourself to access the terminal
        </p>
      </div>
      <!-- 
        The authentication sign-in form component.
        Handles email/username and password input, as well as social logins.
      -->
      <AuthSignIn />
    </div>
  </LayoutAuth>
</template>
