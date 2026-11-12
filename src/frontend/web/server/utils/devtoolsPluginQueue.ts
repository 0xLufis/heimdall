/**
 * Server-side sink for the Vue DevTools plugin queue (dev-server memory leak fix).
 *
 * `@vue/devtools-api` v6 `setupDevtoolsPlugin()` queues every plugin registration in
 * `globalThis.__VUE_DEVTOOLS_PLUGINS__` until a Vue DevTools hook connects, and the queued entry
 * holds `pluginDescriptor.app`. In development vue-i18n (11.x, esm-bundler build) registers its
 * devtools plugin from `install()` on the server too, i.e. once for every SSR render. No DevTools
 * hook ever connects to the Nitro worker, so the queue grew by one complete Vue app per rendered page
 * (component tree, router, i18n, head; ~0.5 MB) until the worker died with "Worker terminated due to
 * reaching memory limit: JS heap out of memory".
 *
 * Nothing on the server reads that queue, so it is replaced by a property that hands out a fresh
 * array on every read and ignores writes: registrations land in a throwaway array and are collected
 * together with their request.
 */
export const DEVTOOLS_PLUGIN_QUEUE_KEY = '__VUE_DEVTOOLS_PLUGINS__'

export function installDevtoolsPluginQueueSink(target: Record<string, any> = globalThis as any): void {
  const existing = Object.getOwnPropertyDescriptor(target, DEVTOOLS_PLUGIN_QUEUE_KEY)
  if (existing && 'value' in existing && Array.isArray(existing.value)) {
    existing.value.length = 0
  }
  Object.defineProperty(target, DEVTOOLS_PLUGIN_QUEUE_KEY, {
    configurable: true,
    enumerable: false,
    get: () => [],
    set: () => { /* dropped: no Vue DevTools hook on the server */ }
  })
}
