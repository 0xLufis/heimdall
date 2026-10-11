import { useAssetReferenceCache } from './useAssetReferenceCache'

export const useInventoryProvisioning = () => {
  const cache = useAssetReferenceCache()

  const fetchReferenceData = async (force = false) => {
    await cache.fetchReferenceCache(force)
  }

  return {
    // Backwards-compatible aliases
    manufacturers: cache.oems,
    suppliers: cache.importers,
    machines: cache.stations,
    clientPcs: cache.parentPcs,
    components: cache.components,
    isLoading: cache.isLoading,

    // Extended cache features
    oems: cache.oems,
    importers: cache.importers,
    parentPcs: cache.parentPcs,
    stations: cache.stations,
    technologies: cache.technologies,
    modelNumbers: cache.modelNumbers,
    metadataKeys: cache.metadataKeys,
    metadataValuesByKey: cache.metadataValuesByKey,
    responsibleTeams: cache.responsibleTeams,

    // Methods
    fetchReferenceData,
    fetchReferenceCache: cache.fetchReferenceCache,
    registerAssetValues: cache.registerAssetValues,
    getSuggestionsForKey: cache.getSuggestionsForKey
  }
}
