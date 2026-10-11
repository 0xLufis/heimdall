import { defineNuxtRouteMiddleware, navigateTo } from 'nuxt/app'
import { useFeatureFlags } from '~/composables/useFeatureFlags'
import { PLATFORM_MODULE_MANIFESTS, type PlatformModuleId } from '~/types/modules'

export default defineNuxtRouteMiddleware((to) => {
  const path = to.path
  const { isModuleEnabled } = useFeatureFlags()

  // Match routes against known module manifests
  for (const [id, manifest] of Object.entries(PLATFORM_MODULE_MANIFESTS) as [PlatformModuleId, typeof PLATFORM_MODULE_MANIFESTS[PlatformModuleId]][]) {
    if (manifest.isRequired) continue

    const matchesRoute = manifest.routes.some((prefix) => path === prefix || path.startsWith(`${prefix}/`))
    if (matchesRoute && !isModuleEnabled(id)) {
      return navigateTo({
        path: '/dashboard',
        query: { module_disabled: id }
      })
    }
  }
})
