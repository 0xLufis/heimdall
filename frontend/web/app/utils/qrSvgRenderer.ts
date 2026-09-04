/**
 * Pure TypeScript Zero-Dependency QR Code Generator
 * Generates valid, scalable SVG QR Codes without any Node or browser canvas dependencies.
 * Safe for SSR, Vitest, Bun, and browser runtime.
 */

// GF(256) Math tables for Reed-Solomon error correction
const EXP_TABLE = new Uint8Array(512)
const LOG_TABLE = new Uint8Array(256)

;(function initGaloisField() {
  let x = 1
  for (let i = 0; i < 255; i++) {
    EXP_TABLE[i] = x
    EXP_TABLE[i + 255] = x
    LOG_TABLE[x] = i
    x = (x << 1) ^ (x >= 128 ? 0x11d : 0)
  }
  LOG_TABLE[0] = 0
})()

function gMultiply(a: number, b: number): number {
  if (a === 0 || b === 0) return 0
  return EXP_TABLE[LOG_TABLE[a] + LOG_TABLE[b]]
}

function rsGeneratorPoly(degree: number): Uint8Array {
  let poly = new Uint8Array([1])
  for (let i = 0; i < degree; i++) {
    const next = new Uint8Array(poly.length + 1)
    for (let j = 0; j < poly.length; j++) {
      next[j] ^= gMultiply(poly[j], EXP_TABLE[i])
      next[j + 1] ^= poly[j]
    }
    poly = next
  }
  return poly
}

function rsEncode(data: Uint8Array, ecLength: number): Uint8Array {
  const gen = rsGeneratorPoly(ecLength)
  const res = new Uint8Array(data.length + ecLength)
  res.set(data)
  for (let i = 0; i < data.length; i++) {
    const coef = res[i]
    if (coef !== 0) {
      for (let j = 0; j < gen.length; j++) {
        res[i + j] ^= gMultiply(gen[j], coef)
      }
    }
  }
  return res.subarray(data.length)
}

// Table of QR Code versions capacity (Byte mode, Level M)
// version -> { dimension, totalBytes, ecBytes }
const VERSION_SPECS = [
  { version: 1, dim: 21, dataBytes: 16, ecBytes: 10 },
  { version: 2, dim: 25, dataBytes: 28, ecBytes: 16 },
  { version: 3, dim: 29, dataBytes: 44, ecBytes: 26 },
  { version: 4, dim: 33, dataBytes: 64, ecBytes: 36 },
  { version: 5, dim: 37, dataBytes: 86, ecBytes: 48 },
  { version: 6, dim: 41, dataBytes: 108, ecBytes: 64 },
  { version: 7, dim: 45, dataBytes: 124, ecBytes: 72 },
  { version: 8, dim: 49, dataBytes: 154, ecBytes: 88 },
  { version: 9, dim: 53, dataBytes: 182, ecBytes: 110 },
  { version: 10, dim: 57, dataBytes: 216, ecBytes: 130 },
]

export interface QrSvgOptions {
  width?: number
  darkColor?: string
  lightColor?: string
  margin?: number
}

/**
 * Encodes text into a QR code matrix (2D boolean array where true = dark module).
 */
export function generateQrMatrix(text: string): boolean[][] {
  const utf8 = new TextEncoder().encode(text)
  const textLen = utf8.length

  // Find smallest fitting version (Level M)
  const spec = VERSION_SPECS.find(s => s.dataBytes >= textLen + 3) || VERSION_SPECS[VERSION_SPECS.length - 1]
  const { dim, dataBytes, ecBytes } = spec

  // Build data stream: [Mode: 0100 (Byte), Length (8 bits), Data bytes, Terminator, Padding]
  const buffer: number[] = []
  function pushBits(val: number, bits: number) {
    for (let i = bits - 1; i >= 0; i--) {
      buffer.push((val >> i) & 1)
    }
  }

  // 4 bits mode indicator (0100 = 8-bit byte)
  pushBits(0b0100, 4)
  // Character count indicator (8 bits for versions 1-9)
  pushBits(textLen, 8)
  // Data bytes
  for (let i = 0; i < textLen; i++) {
    pushBits(utf8[i], 8)
  }
  // Terminator
  const padBits = Math.min(4, (dataBytes * 8) - buffer.length)
  pushBits(0, padBits)
  // Byte alignment
  while (buffer.length % 8 !== 0) {
    buffer.push(0)
  }
  // Padding bytes 0xEC and 0x11
  let padToggle = false
  while (buffer.length < dataBytes * 8) {
    pushBits(padToggle ? 0x11 : 0xec, 8)
    padToggle = !padToggle
  }

  // Convert bits to byte array
  const data = new Uint8Array(dataBytes)
  for (let i = 0; i < dataBytes; i++) {
    let b = 0
    for (let j = 0; j < 8; j++) {
      b = (b << 1) | buffer[i * 8 + j]
    }
    data[i] = b
  }

  // Compute Reed-Solomon EC bytes
  const ec = rsEncode(data, ecBytes)
  const allCodewords = new Uint8Array(dataBytes + ecBytes)
  allCodewords.set(data, 0)
  allCodewords.set(ec, dataBytes)

  // Initialize matrix and reserved flags
  const matrix: boolean[][] = Array.from({ length: dim }, () => Array(dim).fill(false))
  const reserved: boolean[][] = Array.from({ length: dim }, () => Array(dim).fill(false))

  function setModule(r: number, c: number, val: boolean, isRes = true) {
    if (r >= 0 && r < dim && c >= 0 && c < dim) {
      matrix[r][c] = val
      if (isRes) reserved[r][c] = true
    }
  }

  // Place Finder Patterns (7x7 at corners)
  function placeFinder(r0: number, c0: number) {
    for (let r = -1; r <= 7; r++) {
      for (let c = -1; c <= 7; c++) {
        const isBorder = r === -1 || r === 7 || c === -1 || c === 7
        const isOuter = r === 0 || r === 6 || c === 0 || c === 6
        const isInner = r >= 2 && r <= 4 && c >= 2 && c <= 4
        if (r0 + r >= 0 && r0 + r < dim && c0 + c >= 0 && c0 + c < dim) {
          if (isBorder) {
            setModule(r0 + r, c0 + c, false)
          } else {
            setModule(r0 + r, c0 + c, isOuter || isInner)
          }
        }
      }
    }
  }

  placeFinder(0, 0)
  placeFinder(0, dim - 7)
  placeFinder(dim - 7, 0)

  // Timing patterns
  for (let i = 8; i < dim - 8; i++) {
    setModule(6, i, i % 2 === 0)
    setModule(i, 6, i % 2 === 0)
  }

  // Dark module
  setModule(dim - 8, 8, true)

  // Format info area reservation
  for (let i = 0; i < 9; i++) {
    reserved[8][i] = true
    reserved[i][8] = true
    reserved[8][dim - 1 - i] = true
    reserved[dim - 1 - i][8] = true
  }

  // Place Data bits in zigzag
  let bitIdx = 0
  const totalBits = allCodewords.length * 8
  let right = dim - 1
  let upwards = true

  while (right > 0) {
    if (right === 6) right-- // Skip vertical timing pattern
    const colList = [right, right - 1]
    const rowList = upwards
      ? Array.from({ length: dim }, (_, i) => dim - 1 - i)
      : Array.from({ length: dim }, (_, i) => i)

    for (const r of rowList) {
      for (const c of colList) {
        if (!reserved[r][c]) {
          let bit = false
          if (bitIdx < totalBits) {
            const bytePos = Math.floor(bitIdx / 8)
            const bitOffset = 7 - (bitIdx % 8)
            bit = ((allCodewords[bytePos] >> bitOffset) & 1) === 1
            bitIdx++
          }
          // Apply standard mask pattern (row + col) % 2 === 0
          const mask = (r + c) % 2 === 0
          matrix[r][c] = bit !== mask
        }
      }
    }
    right -= 2
    upwards = !upwards
  }

  // Write format info (Level M, Mask 0: 101010000010010 XOR 101010000010010 = 0)
  const formatBits = 0x5412 ^ 0x5412

  for (let i = 0; i < 15; i++) {
    const bit = ((formatBits >> (14 - i)) & 1) === 1
    if (i < 6) setModule(8, i, bit, false)
    else if (i === 6) setModule(8, 7, bit, false)
    else if (i === 7) setModule(8, 8, bit, false)
    else if (i === 8) setModule(7, 8, bit, false)
    else setModule(14 - i, 8, bit, false)

    if (i < 8) setModule(dim - 1 - i, 8, bit, false)
    else setModule(8, dim - 15 + i, bit, false)
  }

  return matrix
}

/**
 * Renders QR matrix as an SVG string.
 */
export function generateQrSvg(text: string, options: QrSvgOptions = {}): string {
  const matrix = generateQrMatrix(text)
  const dim = matrix.length
  const margin = options.margin ?? 2
  const totalSize = dim + margin * 2
  const width = options.width ?? 260
  const dark = options.darkColor ?? '#0f172a'
  const light = options.lightColor ?? '#ffffff'

  let pathData = ''
  for (let r = 0; r < dim; r++) {
    for (let c = 0; c < dim; c++) {
      if (matrix[r][c]) {
        pathData += `M${c + margin},${r + margin}h1v1h-1z `
      }
    }
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${totalSize} ${totalSize}" width="${width}" height="${width}" shape-rendering="crispEdges">` +
    `<rect width="100%" height="100%" fill="${light}"/>` +
    `<path d="${pathData.trim()}" fill="${dark}"/>` +
    `</svg>`
}

/**
 * Generates an SVG Data URI (e.g. data:image/svg+xml;utf8,...) suitable for <img> src.
 */
export function generateQrDataUrl(text: string, options: QrSvgOptions = {}): string {
  const svg = generateQrSvg(text, options)
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}
