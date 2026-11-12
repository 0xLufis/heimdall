import { defineNitroPlugin } from 'nitropack/runtime'
import { installDevtoolsPluginQueueSink } from '../utils/devtoolsPluginQueue'

// Dev only: production builds drop vue-i18n's devtools code, so nothing is queued there.
// Without this, every SSR render in `nuxt dev` stayed in memory (see utils/devtoolsPluginQueue.ts).
export default defineNitroPlugin(() => {
  if (import.meta.dev) {
    installDevtoolsPluginQueueSink(globalThis as any)
  }
})
