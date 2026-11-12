/**
 * Recent search queries, kept per browser in localStorage (never sent anywhere).
 * Storage failures (private mode, quota, SSR) degrade to an empty list.
 */

const STORAGE_KEY = 'heimdall:search:recent'
export const MAX_RECENT_SEARCHES = 6

const storage = (): Storage | null => {
  try {
    return typeof window !== 'undefined' && window.localStorage ? window.localStorage : null
  } catch {
    return null
  }
}

export function loadRecentSearches(): string[] {
  try {
    const raw = storage()?.getItem(STORAGE_KEY)
    const parsed = raw ? JSON.parse(raw) : []
    return Array.isArray(parsed) ? parsed.filter((q): q is string => typeof q === 'string' && q.trim().length > 0).slice(0, MAX_RECENT_SEARCHES) : []
  } catch {
    return []
  }
}

/** Puts `query` first, de-duplicated case-insensitively, capped at MAX_RECENT_SEARCHES. */
export function addRecentSearch(list: string[], query: string): string[] {
  const clean = (query || '').trim()
  if (clean.length < 2) return list
  const lower = clean.toLowerCase()
  return [clean, ...list.filter(q => q.toLowerCase() !== lower)].slice(0, MAX_RECENT_SEARCHES)
}

export function saveRecentSearches(list: string[]) {
  try {
    storage()?.setItem(STORAGE_KEY, JSON.stringify(list.slice(0, MAX_RECENT_SEARCHES)))
  } catch {
    // Persisting recents is best-effort.
  }
}
