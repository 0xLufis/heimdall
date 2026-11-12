import type { ObjectDirective } from 'vue'
import { rowContext } from './contextMenuActions'
import type { ContextMenuContextData } from '~/types/contextMenu'

/**
 * `v-ctx` marks an element as an entity the global right-click menu can open for.
 *
 *   <div v-ctx="{ entityType: 'machine', entityId: m.machineId, entityName: m.machineName }">
 *   <TableRow v-ctx:ticket="tkt">          <!-- a known record: converted by rowContext() -->
 *   <div v-ctx:search="result">            <!-- kinds: ticket inventory controller user machine station search -->
 *
 * The data stays in memory (keyed by the element); the DOM only gets `data-ctx="<type>"`, which
 * is what the single delegated `contextmenu` listener looks for. A falsy value unbinds.
 */

export const CTX_ATTRIBUTE = 'data-ctx'

const contextByElement = new WeakMap<Element, ContextMenuContextData>()

type CtxBindingValue = ContextMenuContextData | Record<string, any> | null | undefined | false

const resolveBinding = (value: CtxBindingValue, arg?: string): ContextMenuContextData | null => {
  if (!value || typeof value !== 'object') return null
  return arg ? rowContext(arg, value) : (value as ContextMenuContextData)
}

/** Binds (or, with a falsy value, unbinds) context data to an element. */
export function bindContextData(el: Element, value: CtxBindingValue, arg?: string): void {
  const data = resolveBinding(value, arg)
  if (!data) {
    contextByElement.delete(el)
    if (el.hasAttribute(CTX_ATTRIBUTE)) el.removeAttribute(CTX_ATTRIBUTE)
    return
  }
  contextByElement.set(el, data)
  const type = data.entityType || 'general'
  if (el.getAttribute(CTX_ATTRIBUTE) !== type) el.setAttribute(CTX_ATTRIBUTE, type)
}

export function getContextData(el: Element | null | undefined): ContextMenuContextData | null {
  return (el && contextByElement.get(el)) || null
}

export interface ContextTarget {
  el: Element
  data: ContextMenuContextData
}

/** The innermost bound element at or above an event target. */
export function findContextTarget(target: EventTarget | null | undefined): ContextTarget | null {
  const node = target as (Node & { closest?: Element['closest'] }) | null | undefined
  if (!node || typeof node !== 'object') return null
  let el: Element | null = typeof node.closest === 'function' ? (node as unknown as Element) : (node.parentElement ?? null)
  while (el) {
    const bound: Element | null = el.closest(`[${CTX_ATTRIBUTE}]`)
    if (!bound) return null
    const data = contextByElement.get(bound)
    if (data) return { el: bound, data }
    // An attribute without data (e.g. server-rendered markup that never hydrated): keep looking up.
    el = bound.parentElement
  }
  return null
}

export const vCtx: ObjectDirective<Element, CtxBindingValue> = {
  beforeMount(el, binding) {
    bindContextData(el, binding.value, binding.arg)
  },
  updated(el, binding) {
    bindContextData(el, binding.value, binding.arg)
  },
  unmounted(el) {
    contextByElement.delete(el)
  },
  // Server render: the attribute only (the data is attached on hydration).
  getSSRProps(binding) {
    const data = resolveBinding(binding.value, binding.arg)
    return data ? { [CTX_ATTRIBUTE]: data.entityType || 'general' } : {}
  }
}
