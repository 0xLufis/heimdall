<script setup lang="ts">
definePageMeta({
  layout: 'shadcn-dashboard'
})
</script>

<template>
  <div class="px-8 pt-8">
    <h3 class="text-3xl font-black text-slate-100 tracking-tight uppercase">Help & Support</h3>
    <p class="text-xs font-bold text-slate-500 mt-1 uppercase tracking-widest">User guide and support resources</p>
    
    <div class="mt-8 grid grid-cols-1 md:grid-cols-2 gap-6">
      <div class="p-8 bg-slate-900 border border-slate-800 rounded-3xl">
        <h4 class="text-lg font-black text-slate-200 uppercase tracking-tight mb-4">Documentation</h4>
        <p class="text-sm text-slate-400">Refer to the wiki for detailed instructions on how to use the Heimdall dashboard and agent.</p>
      </div>
      <div class="p-8 bg-slate-900 border border-slate-800 rounded-3xl">
        <h4 class="text-lg font-black text-slate-200 uppercase tracking-tight mb-4">Contact Support</h4>
        <p class="text-sm text-slate-400">For technical assistance, please contact your local system administrator.</p>
      </div>
    </div>
  </div>
</template>
