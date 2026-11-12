import { calculateFuzzyScore } from './fuzzy'

/**
 * Record ranking and match highlighting shared by every Heimdall search surface.
 *
 * A query is split into whitespace tokens; a record matches only when every token matches at
 * least one of its fields (AND semantics, so "weld line2" narrows instead of widening). Each
 * token takes its best field score, weighted by field position (earlier fields are more
 * important), and the record score is the mean over tokens.
 */

export interface RankOptions {
  /** Allow typo-tolerant matching for digit-free tokens of at least 3 characters. Default true. */
  fuzzy?: boolean
  /** Minimum similarity a fuzzy (non-substring) token match needs. Default 0.7. */
  threshold?: number
}

/** Exact / prefix / word / substring matches score at or above this; pure typo matches are below. */
const SUBSTRING_FLOOR = 0.8
/** Relative weight lost per field position (field 0 = 1.0, field 1 = 0.97 ...), floored at 0.85. */
const FIELD_DECAY = 0.03

export function tokenizeQuery(query: string): string[] {
  return (query || '').toLowerCase().split(/\s+/).filter(Boolean)
}

/** Score of one token against one field, 0 when the field does not match. */
function scoreToken(token: string, field: string, fuzzy: boolean, threshold: number): number {
  const target = field.toLowerCase()
  if (target.includes(token)) {
    // calculateFuzzyScore grades exact > prefix > word > substring for contained tokens.
    return Math.max(SUBSTRING_FLOOR, calculateFuzzyScore(token, target))
  }
  // Identifiers differ by their digits (OP20 is not OP30, Line2 is not Line9): tokens that
  // contain a digit are matched literally only.
  if (!fuzzy || token.length < 3 || /\d/.test(token)) return 0
  const score = calculateFuzzyScore(token, target)
  // Within a field, a typo match always ranks below a literal match.
  return score >= threshold ? Math.min(score, SUBSTRING_FLOOR - 0.01) : 0
}

/**
 * Scores a record against a query. `fields` are ordered by importance. Returns 0 when any
 * token is unmatched, 1 for an empty query.
 */
export function scoreRecord(query: string, fields: Array<string | null | undefined>, options: RankOptions = {}): number {
  const tokens = tokenizeQuery(query)
  if (tokens.length === 0) return 1
  const fuzzy = options.fuzzy ?? true
  const threshold = options.threshold ?? 0.7
  const usable = fields.map(f => (f == null ? '' : String(f)))

  let total = 0
  for (const token of tokens) {
    let best = 0
    for (let i = 0; i < usable.length; i++) {
      const field = usable[i]
      if (!field) continue
      const weight = Math.max(0.85, 1 - i * FIELD_DECAY)
      const score = scoreToken(token, field, fuzzy, threshold) * weight
      if (score > best) best = score
    }
    if (best === 0) return 0
    total += best
  }
  return total / tokens.length
}

/**
 * Filters and orders records by relevance. Ties keep the input order (stable), so a
 * server-side ordering survives among equally relevant records.
 */
export function rankRecords<T>(
  query: string,
  records: T[],
  getFields: (record: T) => Array<string | null | undefined>,
  options: RankOptions = {}
): Array<{ item: T; score: number }> {
  if (tokenizeQuery(query).length === 0) return records.map(item => ({ item, score: 1 }))
  const scored: Array<{ item: T; score: number; index: number }> = []
  records.forEach((item, index) => {
    const score = scoreRecord(query, getFields(item), options)
    if (score > 0) scored.push({ item, score, index })
  })
  scored.sort((a, b) => b.score - a.score || a.index - b.index)
  return scored.map(({ item, score }) => ({ item, score }))
}

export interface HighlightSegment {
  text: string
  match: boolean
}

/**
 * Splits `text` into matched / unmatched segments for every query token (case-insensitive,
 * overlapping ranges merged). Rendering segments as text nodes keeps entity names inert:
 * they are backend data and must never be interpreted as HTML.
 */
export function highlightSegments(text: string, query: string): HighlightSegment[] {
  const source = String(text ?? '')
  if (!source) return []
  const tokens = tokenizeQuery(query)
  if (tokens.length === 0) return [{ text: source, match: false }]

  const lower = source.toLowerCase()
  const ranges: Array<[number, number]> = []
  for (const token of tokens) {
    let from = 0
    while (from <= lower.length - token.length) {
      const idx = lower.indexOf(token, from)
      if (idx === -1) break
      ranges.push([idx, idx + token.length])
      from = idx + token.length
    }
  }
  if (ranges.length === 0) return [{ text: source, match: false }]

  ranges.sort((a, b) => a[0] - b[0])
  const merged: Array<[number, number]> = []
  for (const range of ranges) {
    const last = merged[merged.length - 1]
    if (last && range[0] <= last[1]) last[1] = Math.max(last[1], range[1])
    else merged.push([range[0], range[1]])
  }

  const segments: HighlightSegment[] = []
  let cursor = 0
  for (const [start, end] of merged) {
    if (start > cursor) segments.push({ text: source.slice(cursor, start), match: false })
    segments.push({ text: source.slice(start, end), match: true })
    cursor = end
  }
  if (cursor < source.length) segments.push({ text: source.slice(cursor), match: false })
  return segments
}
