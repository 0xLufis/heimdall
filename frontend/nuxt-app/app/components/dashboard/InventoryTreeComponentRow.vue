<script setup lang="ts">
import { ChevronRight } from 'lucide-vue-next'
import { TableRow, TableCell } from '~/components/ui/table'
import { Button } from '~/components/ui/button'
import { ref, computed } from 'vue'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  component: any
  depth: number
  selectedDynamicColumns: string[]
  primaryKey: 'machine' | 'client'
  defaultColumns: any
  activeResponsibilityFilter: string
  searchQuery: string
}>()

const expanded = ref(true)

const combinedChildren = computed(() => {
  return props.component.children || []
})

const isVisible = computed(() => {
  // 1. Check responsibility filter
  const matchesResponsibility = props.activeResponsibilityFilter === 'all' || 
    props.component.responsibleTeams?.some((t: any) => t.id === props.activeResponsibilityFilter)

  // 2. Check search query
  const matchesSearch = !props.searchQuery || 
    props.component.name?.toLowerCase().includes(props.searchQuery.toLowerCase()) ||
    props.component.displayName?.toLowerCase().includes(props.searchQuery.toLowerCase())

  return matchesResponsibility && matchesSearch
})

const hasVisibleChildren = computed(() => {
  const checkChild = (child: any): boolean => {
    const matchesResp = props.activeResponsibilityFilter === 'all' || child.responsibleTeams?.some((t: any) => t.id === props.activeResponsibilityFilter)
    const matchesSearch = !props.searchQuery || child.name?.toLowerCase().includes(props.searchQuery.toLowerCase())
    if (matchesResp && matchesSearch) return true
    const subChildren = child.children || []
    return subChildren.some((c: any) => checkChild(c))
  }

  return combinedChildren.value.some((c: any) => checkChild(c))
})

const shouldRender = computed(() => isVisible.value || hasVisibleChildren.value)

const getNestedValue = (item: any, key: string) => {
  if (item[key] !== undefined) return item[key]
  if (item.metadata && item.metadata[key] !== undefined) return item.metadata[key]
  return null
}
</script>

<template>
  <template v-if="shouldRender">
    <TableRow 
      class="bg-slate-900/20 border-b border-slate-800/50 hover:bg-slate-900/40 transition-colors"
      :class="{'opacity-40': !isVisible && hasVisibleChildren}"
    >
      <TableCell class="p-0 text-center">
         <Button 
           v-if="combinedChildren.length > 0" 
           @click="expanded = !expanded" 
           variant="ghost" 
           size="icon" 
           class="h-6 w-6 text-slate-600 hover:bg-slate-800"
         >
           <ChevronRight class="h-3 w-3 transition-transform" :class="{'rotate-90': expanded}" />
         </Button>
         <div v-else class="w-1 h-1 rounded-full bg-slate-700 mx-auto"></div>
      </TableCell>
      
      <TableCell>
        <div class="flex items-center gap-2" :style="{ paddingLeft: `${depth * 1.5}rem` }">
          <div class="w-1.5 h-1.5 rounded-full" :class="isVisible ? 'bg-indigo-400 shadow-[0_0_8px_rgba(129,140,248,0.3)]' : 'bg-slate-700'"></div>
          <div class="flex flex-col">
            <span class="text-[10px] font-bold uppercase tracking-widest" :class="isVisible ? 'text-slate-300' : 'text-slate-600'">{{ component.name }}</span>
            <span v-if="component.itemType" class="text-[8px] text-slate-600 font-black uppercase">{{ component.itemType }}</span>
          </div>
          <Badge v-for="team in component.responsibleTeams" :key="team.id" variant="outline" class="text-[8px] font-black uppercase border-slate-800 text-slate-500 py-0 h-4">
            {{ team.name }}
          </Badge>
        </div>
      </TableCell>

      <TableCell v-if="primaryKey === 'client' && defaultColumns.lastOnline"></TableCell>

      <TableCell v-if="defaultColumns.owner">
        <span class="text-[9px] text-slate-600 font-mono">{{ component.organizationId || '-' }}</span>
      </TableCell>

      <TableCell v-if="defaultColumns.linkedAsset"></TableCell>

      <TableCell v-if="defaultColumns.teams">
        <div class="flex flex-wrap gap-1">
          <Badge v-for="team in component.responsibleTeams" :key="team.id" variant="outline" class="text-[7px] font-black uppercase border-slate-800 text-slate-600 px-1 py-0 h-3">
            {{ team.name }}
          </Badge>
        </div>
      </TableCell>

      <TableCell v-for="col in selectedDynamicColumns" :key="col" class="text-[10px] text-slate-500 font-mono">
        {{ getNestedValue(component, col) || '-' }}
      </TableCell>
    </TableRow>

    <!-- Recursive Children -->
    <template v-if="expanded && combinedChildren.length > 0">
      <DashboardInventoryTreeComponentRow 
        v-for="child in combinedChildren" 
        :key="child.id" 
        :component="child" 
        :depth="depth + 1"
        :selected-dynamic-columns="selectedDynamicColumns"
        :primary-key="primaryKey"
        :default-columns="defaultColumns"
        :active-responsibility-filter="activeResponsibilityFilter"
        :search-query="searchQuery"
      />
    </template>
  </template>
</template>
