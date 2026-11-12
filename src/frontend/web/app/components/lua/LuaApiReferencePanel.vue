<script setup lang="ts">
import { ref, computed } from 'vue'
import { BookOpenIcon } from 'lucide-vue-next'
import { LUA_API_REFERENCE, LUA_CONTEXT_GLOBALS } from '~/utils/lua/luaApiReference'

/** Inline reference of the sandbox API; clicking a signature inserts it into the editor. */
const emit = defineEmits<{ (e: 'insert', snippet: string): void }>()

const filter = ref('')
const groups = computed(() => {
  const q = filter.value.trim().toLowerCase()
  if (!q) return LUA_API_REFERENCE
  return LUA_API_REFERENCE
    .map(g => ({ ...g, functions: g.functions.filter(f => `${f.signature} ${f.description}`.toLowerCase().includes(q)) }))
    .filter(g => g.functions.length > 0)
})
</script>

<template>
  <div class="space-y-2 text-xs" data-testid="lua-api-reference">
    <div class="flex items-center gap-2 font-semibold uppercase tracking-wider text-muted-foreground">
      <BookOpenIcon class="h-3.5 w-3.5" />
      Script API
    </div>
    <input
      v-model="filter"
      type="search"
      placeholder="Filter functions…"
      class="h-7 w-full rounded-md border border-input bg-background px-2 text-xs"
    >
    <div class="max-h-[420px] space-y-3 overflow-y-auto pr-1">
      <div v-for="g in groups" :key="g.global">
        <div class="font-mono font-semibold text-primary">{{ g.global }}</div>
        <p class="mb-1 text-[11px] text-muted-foreground">{{ g.description }}</p>
        <button
          v-for="f in g.functions"
          :key="f.name"
          type="button"
          class="block w-full rounded px-1.5 py-1 text-left hover:bg-muted/60"
          :title="`Insert ${f.signature}`"
          @click="emit('insert', f.signature)"
        >
          <code class="break-all font-mono text-[11px] text-foreground">{{ f.signature }}</code>
          <span v-if="f.mutates" class="ml-1 rounded bg-amber-500/15 px-1 text-[10px] text-amber-500">changes data</span>
          <span class="block text-[11px] text-muted-foreground">{{ f.description }}</span>
        </button>
      </div>
      <div>
        <div class="font-mono font-semibold text-primary">context</div>
        <p v-for="c in LUA_CONTEXT_GLOBALS" :key="c.name" class="text-[11px] text-muted-foreground">
          <code class="font-mono text-foreground">{{ c.name }}</code> – {{ c.description }}
        </p>
      </div>
    </div>
  </div>
</template>
