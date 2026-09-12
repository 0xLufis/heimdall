/**
 * Damerau-Levenshtein distance and normalized fuzzy similarity scoring
 * for OmniSearch entity matching and key-value autocomplete.
 */

export function calculateLevenshteinDistance(a: string, b: string): number {
  const al = a.length
  const bl = b.length
  if (al === 0) return bl
  if (bl === 0) return al

  const matrix: number[][] = []
  for (let i = 0; i <= al; i++) {
    matrix[i] = [i]
  }
  for (let j = 0; j <= bl; j++) {
    matrix[0][j] = j
  }

  for (let i = 1; i <= al; i++) {
    for (let j = 1; j <= bl; j++) {
      const cost = a[i - 1].toLowerCase() === b[j - 1].toLowerCase() ? 0 : 1
      matrix[i][j] = Math.min(
        matrix[i - 1][j] + 1, // deletion
        matrix[i][j - 1] + 1, // insertion
        matrix[i - 1][j - 1] + cost // substitution
      )
      if (
        i > 1 &&
        j > 1 &&
        a[i - 1].toLowerCase() === b[j - 2].toLowerCase() &&
        a[i - 2].toLowerCase() === b[j - 1].toLowerCase()
      ) {
        matrix[i][j] = Math.min(matrix[i][j], matrix[i - 2][j - 2] + cost) // transposition
      }
    }
  }
  return matrix[al][bl]
}

/**
 * Calculates a normalized similarity score between 0.0 and 1.0.
 */
export function calculateFuzzyScore(query: string, target: string): number {
  if (!query || !target) return 0.0

  const q = query.trim().toLowerCase()
  const t = target.trim().toLowerCase()

  if (q === t) return 1.0
  if (t.startsWith(q)) return 0.95
  
  // Word boundary match (e.g. "siemens" matching "Line 1 Siemens Drive")
  const words = t.split(/[\s_\-–/]+/)
  if (words.some(w => w === q)) return 0.93
  if (words.some(w => w.startsWith(q))) return 0.88

  // Substring match
  if (t.includes(q)) {
    const ratio = q.length / t.length
    return 0.8 + ratio * 0.1
  }

  const maxAllowedDist = q.length <= 3 ? 1 : q.length <= 6 ? 2 : 3

  // Token / word-level fuzzy match for multi-word targets (e.g. "siemns" matching "Siemens S7-1500")
  let bestTokenScore = 0.0
  for (const word of words) {
    if (word.length >= 3) {
      const wLower = word.toLowerCase()
      const wDist = calculateLevenshteinDistance(q, wLower)
      if (wDist <= maxAllowedDist) {
        const wScore = 1.0 - wDist / Math.max(q.length, wLower.length)
        if (wScore > bestTokenScore) {
          bestTokenScore = wScore
        }
      }
    }
  }

  if (bestTokenScore >= 0.7) {
    return Math.max(0.82, bestTokenScore * 0.95)
  }

  // Full-string edit distance calculation
  const maxLen = Math.max(q.length, t.length)
  const dist = calculateLevenshteinDistance(q, t)

  if (dist > maxAllowedDist) {
    return 0.0
  }

  const score = 1.0 - dist / maxLen
  return Math.max(0.0, Math.min(1.0, score))
}

/**
 * Checks if query matches target with score >= threshold.
 */
export function isFuzzyMatch(query: string, target: string, threshold = 0.7): boolean {
  return calculateFuzzyScore(query, target) >= threshold
}

/**
 * Ranks an array of items by fuzzy similarity score.
 */
export function rankByFuzzyScore<T>(
  query: string,
  items: T[],
  getFields: (item: T) => (string | undefined | null)[],
  threshold = 0.7
): Array<{ item: T; score: number }> {
  if (!query || !items.length) {
    return items.map(item => ({ item, score: 1.0 }))
  }

  const scored: Array<{ item: T; score: number }> = []

  for (const item of items) {
    const fields = getFields(item).filter((f): f is string => typeof f === 'string' && f.length > 0)
    let maxScore = 0.0

    for (const field of fields) {
      const score = calculateFuzzyScore(query, field)
      if (score > maxScore) {
        maxScore = score
      }
      if (maxScore === 1.0) break
    }

    if (maxScore >= threshold) {
      scored.push({ item, score: maxScore })
    }
  }

  return scored.sort((a, b) => b.score - a.score)
}
