import { describe, it, expect } from 'vitest'
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'
import { installDevtoolsPluginQueueSink, DEVTOOLS_PLUGIN_QUEUE_KEY } from '~~/server/utils/devtoolsPluginQueue'

// Mirrors @vue/devtools-api v6 setupDevtoolsPlugin() when no DevTools hook is present (the server):
// the registration, including `pluginDescriptor.app`, is pushed onto the global queue.
function queueLikeDevtoolsApi(target: any, app: object) {
  const list = target[DEVTOOLS_PLUGIN_QUEUE_KEY] = target[DEVTOOLS_PLUGIN_QUEUE_KEY] || []
  list.push({ pluginDescriptor: { id: 'vue-devtools-plugin-vue-i18n', app }, setupFn: () => {}, proxy: null })
}

describe('server sink for the Vue DevTools plugin queue (SSR memory leak)', () => {
  it('without the sink every registration (and its app) stays queued', () => {
    const target: any = {}
    for (let i = 0; i < 100; i++) queueLikeDevtoolsApi(target, { request: i })
    expect(target[DEVTOOLS_PLUGIN_QUEUE_KEY]).toHaveLength(100)
  })

  it('with the sink nothing is retained, however many renders register a plugin', () => {
    const target: any = {}
    installDevtoolsPluginQueueSink(target)
    for (let i = 0; i < 1000; i++) queueLikeDevtoolsApi(target, { request: i })
    expect(target[DEVTOOLS_PLUGIN_QUEUE_KEY]).toEqual([])
  })

  it('drops entries queued before the sink was installed and can be installed twice', () => {
    const target: any = {}
    queueLikeDevtoolsApi(target, { request: 'early' })
    const early = target[DEVTOOLS_PLUGIN_QUEUE_KEY]
    installDevtoolsPluginQueueSink(target)
    installDevtoolsPluginQueueSink(target)
    expect(early).toHaveLength(0)
    queueLikeDevtoolsApi(target, { request: 'late' })
    expect(target[DEVTOOLS_PLUGIN_QUEUE_KEY]).toHaveLength(0)
  })

  const devtoolsApiV6 = resolve(__dirname, '../../../src/frontend/web/node_modules/vue-i18n/node_modules/@vue/devtools-api/lib/esm/index.js')
  it.skipIf(!existsSync(devtoolsApiV6))('holds for the @vue/devtools-api build vue-i18n uses', async () => {
    const { setupDevtoolsPlugin } = await import(/* @vite-ignore */ devtoolsApiV6)
    const g = globalThis as any
    const before = Object.getOwnPropertyDescriptor(g, DEVTOOLS_PLUGIN_QUEUE_KEY)
    const hook = g.__VUE_DEVTOOLS_GLOBAL_HOOK__
    // jsdom provides `window`/`navigator`, so the library's target is the global object either way.
    delete g.__VUE_DEVTOOLS_GLOBAL_HOOK__
    try {
      delete g[DEVTOOLS_PLUGIN_QUEUE_KEY]
      setupDevtoolsPlugin({ id: 'probe', label: 'probe', app: { request: 1 } }, () => {})
      expect(g[DEVTOOLS_PLUGIN_QUEUE_KEY]).toHaveLength(1)

      installDevtoolsPluginQueueSink(g)
      for (let i = 0; i < 50; i++) setupDevtoolsPlugin({ id: 'probe', label: 'probe', app: { request: i } }, () => {})
      expect(g[DEVTOOLS_PLUGIN_QUEUE_KEY]).toEqual([])
    } finally {
      delete g[DEVTOOLS_PLUGIN_QUEUE_KEY]
      if (before) Object.defineProperty(g, DEVTOOLS_PLUGIN_QUEUE_KEY, before)
      if (hook) g.__VUE_DEVTOOLS_GLOBAL_HOOK__ = hook
    }
  })
})
