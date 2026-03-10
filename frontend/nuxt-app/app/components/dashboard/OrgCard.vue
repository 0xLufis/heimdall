<template>
  <div class="bg-white rounded-2xl border border-gray-100 shadow-sm hover:shadow-xl hover:-translate-y-1 transition-all duration-300 overflow-hidden group font-sans">
    <div class="p-6">
      <div class="flex items-start justify-between mb-4">
        <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-500 to-indigo-700 flex items-center justify-center text-xl font-black text-white shadow-lg shadow-indigo-100">
          {{ org.name.charAt(0).toUpperCase() }}
        </div>
        <div class="flex gap-1">
          <button class="p-2 text-gray-400 hover:text-indigo-600 transition-colors">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
            </svg>
          </button>
        </div>
      </div>
      <h5 class="text-lg font-black text-gray-900 group-hover:text-indigo-600 transition-colors truncate">{{ org.name }}</h5>
      <p class="text-xs text-gray-400 font-mono mt-1">ID: {{ org.id }}</p>
      
      <div class="mt-6 flex items-center gap-4 border-t border-gray-50 pt-4">
        <div class="flex -space-x-2">
          <div v-for="i in 3" :key="i" class="w-6 h-6 rounded-full border-2 border-white bg-gray-200 flex items-center justify-center text-[8px] font-bold text-gray-500">U</div>
        </div>
        <span class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Active Members</span>
      </div>
    </div>
    <div class="px-6 py-4 bg-gray-50/50 flex items-center justify-between">
      <button @click="$emit('manage-members', org.id)" class="text-xs font-black text-indigo-600 uppercase tracking-widest hover:text-indigo-800">Manage Members</button>
      <div class="text-[10px] bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded font-black truncate max-w-[100px]">
        {{ org.slug }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  org: any
}>()

defineEmits(['manage-members'])
</script>
