import { vCtx } from '~/utils/contextMenuDirective'

/**
 * Registers `v-ctx` (entity binding of the global right-click menu). Universal on purpose: the
 * server renderer has to resolve the directive too, it emits the `data-ctx` attribute.
 */
export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('ctx', vCtx)
})
