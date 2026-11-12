import { shallowReactive, watch, onMounted, onBeforeUnmount, getCurrentInstance } from 'vue'

/** Every key is recomputed once per interval... */
export const STAGGERED_REFRESH_INTERVAL_MS = 500
/** ...in this many evenly spaced slots (index modulo slots), so redraws are spread over the interval. */
export const STAGGERED_REFRESH_SLOTS = 5

export interface StaggeredRefreshOptions<S> {
  /** Keys in a stable order; a key's slot is its index modulo `slots`. */
  keys: () => readonly string[]
  /**
   * Current value for a key. Called from timers and event handlers, outside any reactive effect,
   * so the published states change only when sampled, not whenever the inputs change.
   */
  compute: (key: string) => S
  equals: (a: S, b: S) => boolean
  intervalMs?: number
  slots?: number
}

/**
 * A sampled, staggered view of values whose inputs change faster than they should be redrawn
 * (plant-map pins under live telemetry): each key is recomputed once per `intervalMs`, one slot of
 * keys every `intervalMs / slots`, and only values that changed are written, so a view of one key
 * re-renders only when that key's value changes. `refreshNow` recomputes keys at once (urgent
 * events); keys that appear get a value immediately.
 */
export function useStaggeredRefresh<S>(options: StaggeredRefreshOptions<S>) {
  const intervalMs = options.intervalMs ?? STAGGERED_REFRESH_INTERVAL_MS
  const slots = Math.max(1, Math.floor(options.slots ?? STAGGERED_REFRESH_SLOTS))
  // Reactive per key: writing one key re-runs only the effects that read that key.
  const states = shallowReactive<Record<string, S>>({})
  let timer: ReturnType<typeof setInterval> | null = null
  let slot = 0

  const refreshKey = (key: string) => {
    const next = options.compute(key)
    const previous = states[key]
    if (previous === undefined || !options.equals(previous, next)) states[key] = next
  }

  /** Recomputes the given keys (all when omitted) now. */
  const refreshNow = (keys?: Iterable<string>) => {
    for (const key of keys ?? options.keys()) refreshKey(key)
  }

  /** Recomputes the next slot of keys; the timer calls it every `intervalMs / slots`. */
  const step = () => {
    const keys = options.keys()
    const current = slot
    slot = (slot + 1) % slots
    for (let i = current; i < keys.length; i += slots) refreshKey(keys[i]!)
  }

  const start = () => {
    if (timer || typeof window === 'undefined') return
    timer = setInterval(step, intervalMs / slots)
  }

  const stop = () => {
    if (timer) clearInterval(timer)
    timer = null
  }

  watch(() => options.keys(), (keys) => {
    const wanted = new Set(keys)
    for (const key of Object.keys(states)) {
      if (!wanted.has(key)) delete states[key]
    }
    refreshNow(keys.filter(key => states[key] === undefined))
  }, { immediate: true })

  if (getCurrentInstance()) {
    onMounted(start)
    onBeforeUnmount(stop)
  }

  return { states, refreshNow, step, start, stop }
}
