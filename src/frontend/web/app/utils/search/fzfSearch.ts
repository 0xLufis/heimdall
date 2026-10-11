/**
 * High-Performance FZF Fuzzy Matching & Scoring Algorithm
 * 
 * Implements fzf-style character-level fuzzy scoring with:
 * - Word boundary bonuses (delimiters: '/', '-', '_', '.', ':', ' ')
 * - CamelCase bonuses
 * - Consecutive character streak bonuses
 * - Gap penalties
 * - Matched indices extraction for UI highlight rendering
 */

export interface FzfMatchResult {
  score: number
  matches: boolean
  indices: number[]
}

const SCORE_MIN = -10000
const BONUS_CONSECUTIVE = 16
const BONUS_BOUNDARY = 8
const BONUS_CAMELCASE = 7
const BONUS_PREFIX = 32
const PENALTY_GAP_START = -3
const PENALTY_GAP_EXTENSION = -1

function isDelimiter(char: string): boolean {
  return char === '/' || char === '-' || char === '_' || char === '.' || char === ':' || char === ' ' || char === '\\'
}

function isUpperCase(char: string): boolean {
  return char >= 'A' && char <= 'Z'
}

/**
 * Calculates fzf match and character indices.
 */
export function fzfMatch(query: string, target: string): FzfMatchResult {
  if (!query) {
    return { score: 0, matches: true, indices: [] }
  }
  if (!target) {
    return { score: SCORE_MIN, matches: false, indices: [] }
  }

  const qLen = query.length
  const tLen = target.length

  if (qLen > tLen) {
    return { score: SCORE_MIN, matches: false, indices: [] }
  }

  // Fast lowercase equality check
  const qLower = query.toLowerCase()
  const tLower = target.toLowerCase()

  if (qLower === tLower) {
    return {
      score: 1000 + BONUS_PREFIX * 2,
      matches: true,
      indices: Array.from({ length: tLen }, (_, i) => i)
    }
  }

  // Check prefix match
  if (tLower.startsWith(qLower)) {
    return {
      score: 800 + BONUS_PREFIX + (qLen === tLen ? 200 : 0),
      matches: true,
      indices: Array.from({ length: qLen }, (_, i) => i)
    }
  }

  // Substring match
  const subIdx = tLower.indexOf(qLower)
  if (subIdx !== -1) {
    const isBoundary = subIdx === 0 || isDelimiter(target[subIdx - 1])
    const boundaryBonus = isBoundary ? BONUS_BOUNDARY * 2 : 0
    return {
      score: 600 + boundaryBonus + (BONUS_CONSECUTIVE * qLen),
      matches: true,
      indices: Array.from({ length: qLen }, (_, i) => subIdx + i)
    }
  }

  // Full FZF sequential alignment
  const indices: number[] = []
  let score = 0
  let tIdx = 0
  let consecutiveStreak = 0
  let prevMatchedIdx = -1

  for (let qIdx = 0; qIdx < qLen; qIdx++) {
    const qChar = qLower[qIdx]
    let found = false

    while (tIdx < tLen) {
      const tChar = tLower[tIdx]
      if (tChar === qChar) {
        found = true
        indices.push(tIdx)

        // Calculate character bonus
        let charBonus = 0

        // Prefix bonus
        if (tIdx === 0) {
          charBonus += BONUS_PREFIX
        } else {
          // Boundary bonus
          const prevChar = target[tIdx - 1]
          if (isDelimiter(prevChar)) {
            charBonus += BONUS_BOUNDARY
          } else if (!isUpperCase(prevChar) && isUpperCase(target[tIdx])) {
            // CamelCase bonus
            charBonus += BONUS_CAMELCASE
          }
        }

        // Consecutive match bonus
        if (prevMatchedIdx === tIdx - 1) {
          consecutiveStreak++
          charBonus += BONUS_CONSECUTIVE * consecutiveStreak
        } else {
          consecutiveStreak = 0
          // Gap penalty
          if (prevMatchedIdx !== -1) {
            const gap = tIdx - prevMatchedIdx - 1
            charBonus += PENALTY_GAP_START + (PENALTY_GAP_EXTENSION * gap)
          }
        }

        score += charBonus
        prevMatchedIdx = tIdx
        tIdx++
        break
      }
      tIdx++
    }

    if (!found) {
      return { score: SCORE_MIN, matches: false, indices: [] }
    }
  }

  return {
    score: Math.max(1, score),
    matches: true,
    indices
  }
}

/**
 * Renders target text with high-contrast highlighted spans based on fzf indices.
 */
export function highlightFzfMatches(target: string, indices: number[], highlightClass: string = 'text-indigo-400 font-bold underline'): string {
  if (!indices || indices.length === 0) return target

  const indexSet = new Set(indices)
  let html = ''
  let inHighlight = false

  for (let i = 0; i < target.length; i++) {
    const isMatch = indexSet.has(i)
    if (isMatch && !inHighlight) {
      html += `<span class="${highlightClass}">`
      inHighlight = true
    } else if (!isMatch && inHighlight) {
      html += '</span>'
      inHighlight = false
    }
    html += escapeHtml(target[i])
  }

  if (inHighlight) {
    html += '</span>'
  }

  return html
}

function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}
