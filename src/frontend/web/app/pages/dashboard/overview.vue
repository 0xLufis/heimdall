<script setup lang="ts">
import CustomizableTwinGrid from '~/components/twin/CustomizableTwinGrid.vue'

definePageMeta({
  layout: 'dashboard',
  alias: ['/dashboard']
})
</script>

<template>
  <div class="w-full">
    <CustomizableTwinGrid />
  </div>
</template>
