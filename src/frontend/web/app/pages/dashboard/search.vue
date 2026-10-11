<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Search,
  Filter,
  ArrowUpDown,
  Cpu,
  Monitor,
  Wrench,
  Package,
  Users,
  ExternalLink,
  ChevronRight,
  Sparkles,
  Tag as TagIcon,
  X,
  SlidersHorizontal,
  Layers,
  CheckCircle2,
  AlertTriangle
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import SearchAndFilterBar from '~/components/common/SearchAndFilterBar.vue'
import MachinePictogram from '~/components/machines/MachinePictogram.vue'
import { getGroupColor } from '~/utils/machineGroupColors'

definePageMeta({
  layout: 'shadcn-dashboard'
})

export interface UnifiedSearchResult {
  id: string
  name: string
  subtitle?: string
  description?: string
  provider: 'machines' | 'clients' | 'tickets' | 'inventory' | 'users'
  providerLabel: string
  url: string
  matchRate: number
  matchedFields: string[]
  tags: string[]
  metadata?: Record<string, any>
  createdAt?: string
}

const route = useRoute()
const router = useRouter()

const searchQuery = ref<string>('')
const tagFilters = ref<string[]>([])
const activeProviderFilter = ref<string>('all')
const sortBy = ref<'matchRate' | 'name_asc' | 'name_desc' | 'provider'>('matchRate')
const isLoading = ref(false)

// Raw datasets
const rawMachines = ref<any[]>([])
const rawClients = ref<any[]>([])
const rawTickets = ref<any[]>([])
const rawInventory = ref<any[]>([])
const rawUsers = ref<any[]>([])

async function loadDatasets() {
  isLoading.value = true
  try {
    const [mRes, cRes, tRes, iRes, uRes] = await Promise.allSettled([
      $fetch<any[]>('/api/machines?includeDecommissioned=true').catch(() => []),
      $fetch<any[]>('/api/clients').catch(() => []),
      $fetch<any[]>('/api/tickets').catch(() => []),
      $fetch<any[]>('/api/inventory/parts').catch(() => []),
      $fetch<any[]>('/api/users').catch(() => [])
    ])

    if (mRes.status === 'fulfilled' && Array.isArray(mRes.value)) rawMachines.value = mRes.value
    if (cRes.status === 'fulfilled' && Array.isArray(cRes.value)) rawClients.value = cRes.value
    if (tRes.status === 'fulfilled' && Array.isArray(tRes.value)) rawTickets.value = tRes.value
    if (iRes.status === 'fulfilled' && Array.isArray(iRes.value)) rawInventory.value = iRes.value
    if (uRes.status === 'fulfilled' && Array.isArray(uRes.value)) rawUsers.value = uRes.value
  } finally {
    isLoading.value = false
  }
}

// Watch query string parameter in route
watch(
  () => route?.query?.q,
  (newQ) => {
    if (typeof newQ === 'string' && newQ !== searchQuery.value) {
      searchQuery.value = newQ
    }
  },
  { immediate: true }
)

// Sync input back to URL query
watch(searchQuery, (newVal) => {
  const currentQ = (route?.query?.q as string) || ''
  if (newVal.trim() !== currentQ && router?.replace) {
    router.replace({
      query: { ...(route?.query || {}), q: newVal.trim() || undefined }
    })
  }
})

onMounted(() => {
  loadDatasets()
})

// Parse tag query syntax: <! or empty><key>:<value> AND logic gate
interface ParsedQuery {
  positiveTags: Array<{ key: string; val: string }>
  negativeTags: Array<{ key: string; val: string }>
  freeWords: string[]
}

function parseSearchQuery(query: string, tags: string[] = []): ParsedQuery {
  const positiveTags: Array<{ key: string; val: string }> = []
  const negativeTags: Array<{ key: string; val: string }> = []
  
  // 1. Process explicit structured tags
  for (const rawTag of tags) {
    const trimmed = rawTag.trim()
    const isNeg = trimmed.startsWith('!')
    const clean = isNeg ? trimmed.slice(1).trim() : trimmed
    const colonIdx = clean.indexOf(':')
    if (colonIdx > 0) {
      const key = clean.slice(0, colonIdx).trim().toLowerCase()
      const val = clean.slice(colonIdx + 1).trim().toLowerCase()
      if (isNeg) {
        negativeTags.push({ key, val })
      } else {
        positiveTags.push({ key, val })
      }
    } else if (clean) {
      const val = clean.replace(/^#/, '').toLowerCase()
      if (isNeg) {
        negativeTags.push({ key: 'tag', val })
      } else {
        positiveTags.push({ key: 'tag', val })
      }
    }
  }

  // 2. Regex to extract `!key:value` or `key:value` or `key:"quoted value"` from query string
  const tagRegex = /(!)?(\w+):(?:"([^"]+)"|(\S+))/g
  let match
  let cleanText = query

  while ((match = tagRegex.exec(query)) !== null) {
    const isNeg = Boolean(match[1])
    const key = (match[2] || '').toLowerCase()
    const val = (match[3] || match[4] || '').toLowerCase()
    if (isNeg) {
      negativeTags.push({ key, val })
    } else {
      positiveTags.push({ key, val })
    }
  }

  cleanText = cleanText.replace(tagRegex, ' ').trim()
  const freeWords = cleanText ? cleanText.toLowerCase().split(/\s+/).filter(Boolean) : []

  return { positiveTags, negativeTags, freeWords }
}

// Compute match score and matching fields for an entity
function scoreEntity(
  item: any,
  provider: 'machines' | 'clients' | 'tickets' | 'inventory' | 'users',
  parsed: ParsedQuery
): { score: number; matchedFields: string[] } | null {
  const { positiveTags, negativeTags, freeWords } = parsed

  // 1. Evaluate Negative Tags (AND logic gate: if any negative tag matches, reject)
  for (const { key, val } of negativeTags) {
    if (key === 'status' && (item.status || '').toLowerCase().includes(val)) return null
    if (key === 'type' && (item.machineType || item.itemType || item.type || '').toLowerCase().includes(val)) return null
    if (key === 'line' && (item.groupId || item.line || '').toLowerCase().includes(val)) return null
    if (key === 'provider' && provider.toLowerCase().includes(val)) return null
    if (key === 'role' && (item.role || '').toLowerCase().includes(val)) return null
  }

  // 2. Evaluate Positive Tags (AND logic gate: all positive tags must match)
  const matchedFields: string[] = []
  let tagBonus = 0

  for (const { key, val } of positiveTags) {
    let matched = false
    if (key === 'provider' && (provider.toLowerCase() === val || provider.toLowerCase().includes(val))) {
      matched = true
      matchedFields.push('provider')
    } else if (key === 'status' && (item.status || '').toLowerCase().includes(val)) {
      matched = true
      matchedFields.push('status')
    } else if (key === 'type' && (item.machineType || item.itemType || item.type || item.category || '').toLowerCase().includes(val)) {
      matched = true
      matchedFields.push('type')
    } else if (key === 'line' && (item.groupId || item.line || '').toLowerCase().includes(val)) {
      matched = true
      matchedFields.push('line')
    } else if (key === 'role' && (item.role || '').toLowerCase().includes(val)) {
      matched = true
      matchedFields.push('role')
    } else if (key === 'priority' && (item.priority || '').toLowerCase().includes(val)) {
      matched = true
      matchedFields.push('priority')
    } else if (key === 'station' && ((item.stationName || item.machineId || item.name || '').toLowerCase().includes(val))) {
      matched = true
      matchedFields.push('station')
    }

    if (!matched) return null // Positive tag check failed!
    tagBonus += 25
  }

  // 3. Evaluate Free Words across all text blobs
  const idStr = (item.id || item.ticketNumber || item.customIdentifier || '').toLowerCase()
  const nameStr = (item.name || item.hostname || item.title || item.displayName || '').toLowerCase()
  const descStr = (item.description || item.decommissionReason || item.issueDescription || item.department || '').toLowerCase()
  const typeStr = (item.machineType || item.role || item.category || '').toLowerCase()

  if (freeWords.length > 0) {
    let totalWordScore = 0

    for (const w of freeWords) {
      let wordMatched = false
      if (nameStr === w || idStr === w) {
        totalWordScore += 50
        matchedFields.push('exact_name')
        wordMatched = true
      } else if (nameStr.startsWith(w) || idStr.startsWith(w)) {
        totalWordScore += 35
        matchedFields.push('prefix_name')
        wordMatched = true
      } else if (nameStr.includes(w) || idStr.includes(w)) {
        totalWordScore += 25
        matchedFields.push('name')
        wordMatched = true
      } else if (descStr.includes(w)) {
        totalWordScore += 15
        matchedFields.push('description')
        wordMatched = true
      } else if (typeStr.includes(w)) {
        totalWordScore += 10
        matchedFields.push('type')
        wordMatched = true
      }

      if (!wordMatched) return null // AND logic gate: all free words must hit something
    }

    const calculatedScore = Math.min(100, Math.round(totalWordScore + tagBonus))
    return { score: calculatedScore, matchedFields }
  }

  // If no free words, but tags were matched:
  const baseScore = positiveTags.length > 0 ? Math.min(100, 50 + tagBonus) : 100
  return { score: baseScore, matchedFields }
}

// All Computed Search Results
const allSearchResults = computed<UnifiedSearchResult[]>(() => {
  const query = searchQuery.value.trim()
  const parsed = parseSearchQuery(query, tagFilters.value)
  const results: UnifiedSearchResult[] = []

  // If query is empty and no tag filters active, return a featured/overview selection
  const isQueryEmpty = !query && tagFilters.value.length === 0

  // 1. Machines
  for (const m of rawMachines.value) {
    const scored = isQueryEmpty ? { score: 100, matchedFields: [] } : scoreEntity(m, 'machines', parsed)
    if (scored) {
      results.push({
        id: m.id,
        name: m.name || m.customIdentifier || m.id,
        subtitle: `${m.customIdentifier || 'CELL'} • ${m.machineType || 'Station'}`,
        description: m.displayName || m.decommissionReason || `Machine located on ${m.groupId || 'Line 1'}`,
        provider: 'machines',
        providerLabel: 'Machinery & Stations',
        url: `/dashboard/machines/${m.id}`,
        matchRate: scored.score,
        matchedFields: scored.matchedFields,
        tags: [m.groupId || 'Line 1', m.machineType || 'Station', m.status || 'active'],
        metadata: { status: m.status, groupId: m.groupId, machineType: m.machineType }
      })
    }
  }

  // 2. Clients / IPCs
  for (const c of rawClients.value) {
    const scored = isQueryEmpty ? { score: 100, matchedFields: [] } : scoreEntity(c, 'clients', parsed)
    if (scored) {
      results.push({
        id: c.id,
        name: c.hostname || c.name || c.id,
        subtitle: `IP: ${c.ipAddress || '192.168.1.100'} • OS: ${c.osVersion || 'Windows IoT'}`,
        description: `Industrial Controller PC attached to ${c.associatedMachineName || 'Automation Cell'}`,
        provider: 'clients',
        providerLabel: 'Host Controller IPCs',
        url: `/dashboard/clients?selected=${encodeURIComponent(c.id)}&hostname=${encodeURIComponent(c.hostname || c.name)}`,
        matchRate: scored.score,
        matchedFields: scored.matchedFields,
        tags: [c.ipAddress || 'Local', c.isOnline ? 'Online' : 'Offline', 'IPC'],
        metadata: { ip: c.ipAddress, isOnline: c.isOnline }
      })
    }
  }

  // 3. Maintenance Tickets
  for (const t of rawTickets.value) {
    const scored = isQueryEmpty ? { score: 100, matchedFields: [] } : scoreEntity(t, 'tickets', parsed)
    if (scored) {
      results.push({
        id: t.id,
        name: `${t.ticketNumber || 'TICK'}: ${t.title || 'Incident Report'}`,
        subtitle: `Station: ${t.stationName || t.machineId || 'Floor'} • Priority: ${t.priority || 'Normal'}`,
        description: t.issueDescription || t.escalationReason || 'Reported maintenance work order',
        provider: 'tickets',
        providerLabel: 'Maintenance Tickets',
        url: `/dashboard/tickets?ticketId=${encodeURIComponent(t.id)}`,
        matchRate: scored.score,
        matchedFields: scored.matchedFields,
        tags: [t.priority || 'Normal', t.status || 'open', t.isEscalated ? 'Escalated' : 'Standard'],
        metadata: { status: t.status, priority: t.priority, isEscalated: t.isEscalated }
      })
    }
  }

  // 4. Inventory Parts
  for (const p of rawInventory.value) {
    const scored = isQueryEmpty ? { score: 100, matchedFields: [] } : scoreEntity(p, 'inventory', parsed)
    if (scored) {
      results.push({
        id: p.id,
        name: p.name || p.partNumber || p.id,
        subtitle: `PN: ${p.partNumber || 'PART'} • Stock: ${p.quantityOnHand ?? 0} in ${p.locationBin || 'Warehouse'}`,
        description: p.description || p.category || 'Spare part item in stock',
        provider: 'inventory',
        providerLabel: 'Inventory & Parts',
        url: `/dashboard/inventory/${p.id}`,
        matchRate: scored.score,
        matchedFields: scored.matchedFields,
        tags: [p.category || 'Parts', `Qty: ${p.quantityOnHand ?? 0}`, p.locationBin || 'Bin'],
        metadata: { quantity: p.quantityOnHand, category: p.category }
      })
    }
  }

  // 5. Users & Personnel
  for (const u of rawUsers.value) {
    const scored = isQueryEmpty ? { score: 100, matchedFields: [] } : scoreEntity(u, 'users', parsed)
    if (scored) {
      results.push({
        id: u.id,
        name: u.name || u.displayName || u.id,
        subtitle: `Role: ${u.role || 'Staff'} • Dept: ${u.department || 'Maintenance'}`,
        description: u.email || `${u.specialization || 'Technical'} Specialist`,
        provider: 'users',
        providerLabel: 'Technicians & Personnel',
        url: `/dashboard/users?id=${encodeURIComponent(u.id)}`,
        matchRate: scored.score,
        matchedFields: scored.matchedFields,
        tags: [u.role || 'Staff', u.department || 'Plant', u.isOutOfOffice ? 'OOO' : 'On-Duty'],
        metadata: { role: u.role, isOutOfOffice: u.isOutOfOffice }
      })
    }
  }

  return results
})

// Provider Filter
const filteredByProvider = computed(() => {
  if (activeProviderFilter.value === 'all') return allSearchResults.value
  return allSearchResults.value.filter(r => r.provider === activeProviderFilter.value)
})

// Provider Counts
const providerCounts = computed(() => {
  const counts: Record<string, number> = {
    all: allSearchResults.value.length,
    machines: 0,
    clients: 0,
    tickets: 0,
    inventory: 0,
    users: 0
  }
  for (const r of allSearchResults.value) {
    if (counts[r.provider] !== undefined) {
      counts[r.provider]++
    }
  }
  return counts
})

// Sorted Results
const sortedResults = computed(() => {
  const list = [...filteredByProvider.value]

  if (sortBy.value === 'matchRate') {
    return list.sort((a, b) => b.matchRate - a.matchRate)
  }
  if (sortBy.value === 'name_asc') {
    return list.sort((a, b) => a.name.localeCompare(b.name))
  }
  if (sortBy.value === 'name_desc') {
    return list.sort((a, b) => b.name.localeCompare(a.name))
  }
  if (sortBy.value === 'provider') {
    return list.sort((a, b) => a.provider.localeCompare(b.provider) || b.matchRate - a.matchRate)
  }
  return list
})

// Grouped by provider view
const groupedResults = computed(() => {
  const groups: Record<string, { label: string; icon: any; items: UnifiedSearchResult[] }> = {
    machines: { label: 'Machinery & Stations', icon: Cpu, items: [] },
    clients: { label: 'Host Controller IPCs', icon: Monitor, items: [] },
    tickets: { label: 'Maintenance Tickets', icon: Wrench, items: [] },
    inventory: { label: 'Inventory & Parts', icon: Package, items: [] },
    users: { label: 'Technicians & Personnel', icon: Users, items: [] }
  }

  for (const r of sortedResults.value) {
    if (groups[r.provider]) {
      groups[r.provider].items.push(r)
    }
  }

  return Object.entries(groups)
    .filter(([_, g]) => g.items.length > 0)
    .map(([key, g]) => ({ key, ...g }))
})

// Helper to append a tag to search
function addTagToSearch(tagStr: string) {
  if (!tagFilters.value.includes(tagStr)) {
    tagFilters.value.push(tagStr)
  }
}

function handleResultClick(result: UnifiedSearchResult) {
  router.push(result.url)
}
</script>

<template>
  <div class="space-y-6 pb-12">
    <!-- Search Header & Hero Bar -->
    <div class="flex flex-col gap-4 border-b border-border pb-5">
      <div>
        <div class="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-primary mb-1">
          <NuxtLink to="/dashboard" class="hover:underline">Dashboard</NuxtLink>
          <span class="text-muted-foreground/50">/</span>
          <span class="text-muted-foreground">Universal Search</span>
        </div>
        <h1 class="text-2xl font-black tracking-tight text-foreground flex items-center gap-3">
          <div class="p-2 rounded-xl bg-primary/10 text-primary border border-primary/20">
            <Search class="size-6" />
          </div>
          <span>Omni Search Results</span>
        </h1>
        <p class="text-sm text-muted-foreground mt-1">
          Multi-provider query engine ranked by match score with structured tag filtering (<code class="text-xs bg-muted px-1 py-0.5 rounded font-mono">key:val</code>, <code class="text-xs bg-muted px-1 py-0.5 rounded font-mono">!key:val</code>).
        </p>
      </div>

      <!-- Split Search & Tag Filter Bar Component -->
      <div class="w-full max-w-3xl">
        <SearchAndFilterBar
          :search-query="searchQuery"
          :tag-filters="tagFilters"
          :total-count="rawMachines.length + rawClients.length + rawTickets.length + rawInventory.length + rawUsers.length"
          :match-count="sortedResults.length"
          placeholder="Search machines, tickets, IPCs, parts, users across all providers..."
          :show-facets="false"
          @update:search-query="(val) => searchQuery = val"
          @update:tag-filters="(val) => tagFilters = val"
          @clear-search="searchQuery = ''"
          @clear-filters="tagFilters = []"
          @reset-all="() => { searchQuery = ''; tagFilters = [] }"
        />
      </div>

      <!-- Quick Tag Suggestion Chips -->
      <div class="flex flex-wrap items-center gap-1.5 text-xs">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mr-1">Quick Filters:</span>
        <button
          type="button"
          @click="addTagToSearch('provider:machines')"
          class="px-2 py-0.5 rounded-md bg-muted/60 hover:bg-muted border border-border text-foreground text-[11px] font-medium transition-colors cursor-pointer"
        >
          provider:machines
        </button>
        <button
          type="button"
          @click="addTagToSearch('provider:tickets')"
          class="px-2 py-0.5 rounded-md bg-muted/60 hover:bg-muted border border-border text-foreground text-[11px] font-medium transition-colors cursor-pointer"
        >
          provider:tickets
        </button>
        <button
          type="button"
          @click="addTagToSearch('status:active')"
          class="px-2 py-0.5 rounded-md bg-muted/60 hover:bg-muted border border-border text-foreground text-[11px] font-medium transition-colors cursor-pointer"
        >
          status:active
        </button>
        <button
          type="button"
          @click="addTagToSearch('!status:decommissioned')"
          class="px-2 py-0.5 rounded-md bg-rose-500/10 hover:bg-rose-500/20 border border-rose-500/30 text-rose-400 text-[11px] font-medium transition-colors cursor-pointer"
        >
          !status:decommissioned
        </button>
        <button
          type="button"
          @click="addTagToSearch('priority:critical')"
          class="px-2 py-0.5 rounded-md bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-500 text-[11px] font-medium transition-colors cursor-pointer"
        >
          priority:critical
        </button>
      </div>
    </div>

    <!-- Provider Tabs & Sorting Engine Controls -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
      <!-- Providers Segmented Tabs -->
      <div class="flex items-center gap-1.5 overflow-x-auto pb-1 text-xs">
        <button
          type="button"
          @click="activeProviderFilter = 'all'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'all' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <span>All Providers</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'all' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.all }}
          </span>
        </button>

        <button
          type="button"
          @click="activeProviderFilter = 'machines'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'machines' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <Cpu class="size-3" />
          <span>Machines</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'machines' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.machines }}
          </span>
        </button>

        <button
          type="button"
          @click="activeProviderFilter = 'clients'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'clients' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <Monitor class="size-3" />
          <span>IPCs</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'clients' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.clients }}
          </span>
        </button>

        <button
          type="button"
          @click="activeProviderFilter = 'tickets'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'tickets' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <Wrench class="size-3" />
          <span>Tickets</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'tickets' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.tickets }}
          </span>
        </button>

        <button
          type="button"
          @click="activeProviderFilter = 'inventory'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'inventory' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <Package class="size-3" />
          <span>Parts</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'inventory' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.inventory }}
          </span>
        </button>

        <button
          type="button"
          @click="activeProviderFilter = 'users'"
          class="px-3 py-1.5 rounded-lg border font-medium transition-all shrink-0 cursor-pointer flex items-center gap-1.5"
          :class="activeProviderFilter === 'users' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground hover:bg-muted/50'"
        >
          <Users class="size-3" />
          <span>Personnel</span>
          <span class="px-1.5 py-0.2 rounded-full font-mono text-[10px]" :class="activeProviderFilter === 'users' ? 'bg-primary-foreground/20 text-primary-foreground' : 'bg-muted text-muted-foreground'">
            {{ providerCounts.users }}
          </span>
        </button>
      </div>

      <!-- Sorting Engine Dropdown -->
      <div class="flex items-center gap-2 shrink-0">
        <ArrowUpDown class="size-3.5 text-muted-foreground" />
        <span class="text-xs text-muted-foreground">Sort:</span>
        <select
          v-model="sortBy"
          class="h-8 text-xs bg-card border border-border rounded-lg px-2 text-foreground font-medium focus:ring-1 focus:ring-primary cursor-pointer"
        >
          <option value="matchRate">Best Match (Score)</option>
          <option value="name_asc">Name (A ➔ Z)</option>
          <option value="name_desc">Name (Z ➔ A)</option>
          <option value="provider">Group by Provider</option>
        </select>
      </div>
    </div>

    <!-- Search Results Content: Grouped by Provider -->
    <div v-if="groupedResults.length > 0" class="space-y-8">
      <div
        v-for="grp in groupedResults"
        :key="grp.key"
        class="space-y-3"
      >
        <!-- Provider Header Bar -->
        <div class="flex items-center justify-between border-b border-border/60 pb-2">
          <div class="flex items-center gap-2">
            <component :is="grp.icon" class="size-4 text-primary" />
            <h3 class="text-sm font-bold text-foreground">{{ grp.label }}</h3>
            <span class="text-xs font-mono text-muted-foreground">({{ grp.items.length }})</span>
          </div>
        </div>

        <!-- Provider Items Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="item in grp.items"
            :key="item.id"
            @click="handleResultClick(item)"
            class="p-4 rounded-xl border border-border bg-card shadow-2xs hover:border-primary/50 hover:bg-muted/10 transition-all flex flex-col justify-between gap-3 cursor-pointer group"
          >
            <div class="space-y-2">
              <div class="flex items-start justify-between gap-2">
                <div class="min-w-0">
                  <h4 class="text-sm font-bold text-foreground group-hover:text-primary transition-colors truncate">
                    {{ item.name }}
                  </h4>
                  <p v-if="item.subtitle" class="text-xs text-muted-foreground font-mono truncate">
                    {{ item.subtitle }}
                  </p>
                </div>

                <!-- Match Score Badge -->
                <div class="flex items-center gap-1 shrink-0">
                  <span
                    class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold border"
                    :class="item.matchRate >= 80 ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400' : 'bg-primary/10 border-primary/30 text-primary'"
                  >
                    {{ item.matchRate }}% Match
                  </span>
                </div>
              </div>

              <p v-if="item.description" class="text-xs text-muted-foreground line-clamp-2">
                {{ item.description }}
              </p>

              <!-- Tags list -->
              <div class="flex flex-wrap items-center gap-1 pt-1">
                <span
                  v-for="t in item.tags"
                  :key="t"
                  class="px-1.5 py-0.2 rounded text-[10px] font-mono bg-muted text-muted-foreground border border-border"
                >
                  {{ t }}
                </span>
              </div>
            </div>

            <!-- Card Bottom Redirect Link -->
            <div class="pt-2 border-t border-border/60 flex items-center justify-between text-xs text-muted-foreground group-hover:text-foreground">
              <span class="text-[11px] font-medium">{{ item.providerLabel }}</span>
              <div class="flex items-center gap-1 text-primary text-xs font-semibold">
                <span>View Details</span>
                <ChevronRight class="size-3 group-hover:translate-x-0.5 transition-transform" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="p-12 text-center rounded-2xl border border-dashed border-border bg-card/50 space-y-3">
      <div class="p-3 rounded-full bg-muted w-fit mx-auto text-muted-foreground">
        <Search class="size-6" />
      </div>
      <h3 class="text-base font-bold text-foreground">No matching results found</h3>
      <p class="text-xs text-muted-foreground max-w-md mx-auto">
        No records across Machines, IPCs, Tickets, Parts, or Users matched "{{ searchQuery }}". Try broadening your search or modifying tag parameters.
      </p>
      <Button size="sm" variant="outline" class="text-xs mt-2" @click="searchQuery = ''">
        Reset Search Query
      </Button>
    </div>
  </div>
</template>
