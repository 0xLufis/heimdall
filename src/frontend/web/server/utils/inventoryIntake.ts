import type { H3Event } from 'h3'
import { backendFetch } from './backendFetch'
import { toBackendCreatePayload, toInventoryPart, type BackendInventoryItem } from './inventoryBackend'

type NamedRef = { id: string; name: string }

const findId = (refs: NamedRef[], value: unknown) => {
  const name = typeof value === 'object' && value ? (value as { name?: string }).name : value
  const wanted = String(name || '').trim().toLowerCase()
  return wanted ? refs.find(r => String(r.name || '').toLowerCase() === wanted)?.id : undefined
}

/** Creates one part through `POST /api/v1/inventory`; manufacturer / supplier are matched by name. */
export async function createBackendPart(event: H3Event, part: Record<string, any>, refs: { manufacturers: NamedRef[]; suppliers: NamedRef[] }) {
  const payload = toBackendCreatePayload(part, {
    manufacturerId: findId(refs.manufacturers, part.manufacturer),
    supplierId: findId(refs.suppliers, part.supplier)
  })
  const created = await backendFetch<BackendInventoryItem>(event, 'inventory', { method: 'POST', body: payload })
  return toInventoryPart(created)
}
