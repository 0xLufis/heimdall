<script setup lang="ts">
const color = useColorMode()
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button variant="ghost" size="icon" title="Toggle Color Scheme" aria-label="button dark toggle">
        <Icon name="i-lucide-sun" class="h-[1.2rem] w-[1.2rem] rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
        <Icon name="i-lucide-moon" class="absolute h-[1.2rem] w-[1.2rem] rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
        <span class="sr-only">Toggle theme</span>
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end">
      <DropdownMenuItem @click="color.preference = 'light'">
        <Icon name="i-lucide-sun" class="mr-2 h-4 w-4" />
        <span>Light</span>
      </DropdownMenuItem>
      <DropdownMenuItem @click="color.preference = 'dark'">
        <Icon name="i-lucide-moon" class="mr-2 h-4 w-4" />
        <span>Dark</span>
      </DropdownMenuItem>
      <DropdownMenuItem @click="color.preference = 'system'">
        <Icon name="i-lucide-monitor" class="mr-2 h-4 w-4" />
        <span>System</span>
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
