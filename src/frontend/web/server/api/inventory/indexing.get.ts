import { defineEventHandler } from 'h3'
import { backendFetch } from '../../utils/backendFetch'
import { loadBackendParts, toInventoryPart } from '../../utils/inventoryBackend'

const unique = (values: Array<string | undefined | null>) =>
  [...new Set(values.map(v => (v || '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b))

/** Autocomplete values for the intake / use forms, from the backend inventory. */
export default defineEventHandler(async (event) => {
  const [raw, machines] = await Promise.all([
    loadBackendParts(event),
    backendFetch<Array<{ id: string; name?: string; customIdentifier?: string; groupId?: string }>>(event, 'inventory/machines').catch(() => [])
  ])
  const parts = raw.map(r => toInventoryPart(r))
  return {
    success: true,
    manufacturers: unique(parts.map(p => p.manufacturer.name).filter(n => n !== 'Unknown')),
    suppliers: unique(parts.map(p => p.supplier?.name)),
    categories: unique(parts.map(p => p.subCategory)),
    locations: unique(parts.map(p => p.location)),
    costCenters: unique(machines.map(m => m.groupId)),
    machines: machines.map(m => ({ id: m.id, name: m.customIdentifier || m.name || m.id }))
  }
})
