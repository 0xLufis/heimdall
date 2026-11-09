<script setup lang="ts">
import TemplateVisualEditor from '~/components/templates/TemplateVisualEditor.vue'

definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>

<template>
  <div class="h-[calc(100vh-3.5rem)] -m-4 sm:-m-6 overflow-hidden">
    <TemplateVisualEditor />
  </div>
</template>
