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
    Signup page layout using the Auth layout component.
    No specific prop for `LayoutAuth` here, so it uses its default alignment.
  -->
  <LayoutAuth>
    <div class="grid mx-auto max-w-sm gap-6">
      <div class="grid gap-2 text-center">
        <h1 class="text-3xl font-bold tracking-tight uppercase">
          Initialize
        </h1>
        <p class="text-balance text-xs font-bold uppercase tracking-widest text-muted-foreground opacity-70">
          Create your operator profile
        </p>
      </div>
      <!-- 
        The authentication sign-up form component.
        Handles name, username, email, and password input, as well as social sign-up options.
      -->
      <AuthSignUp />
    </div>
  </LayoutAuth>
</template>
