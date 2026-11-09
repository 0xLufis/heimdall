import QRCode from 'qrcode'

/**
 * Standard QR Code Generator & Renderer
 * Uses battle-tested, ISO/IEC 18004 compliant QRCode encoder.
 * Guarantees standard Reed-Solomon error correction, alignment patterns,
 * valid format bits, and required quiet zones (margin >= 4) so that
 * any smartphone camera (iOS, Android, Google Lens) and handheld barcode gun
 * can immediately scan and decode it.
 */

export interface QrSvgOptions {
  width?: number
  darkColor?: string
  lightColor?: string
  margin?: number
}

/**
 * Encodes text into a standard QR code matrix (2D boolean array where true = dark module).
 */
export function generateQrMatrix(text: string): boolean[][] {
  const qr = QRCode.create(text, { errorCorrectionLevel: 'M' })
  const dim = qr.modules.size
  const matrix: boolean[][] = []

  for (let r = 0; r < dim; r++) {
    const row: boolean[] = []
    for (let c = 0; c < dim; c++) {
      row.push(Boolean(qr.modules.get(r, c)))
    }
    matrix.push(row)
  }

  return matrix
}

/**
 * Renders standard, scalable SVG QR Code with high-contrast modules and 4-module quiet zone.
 */
export function generateQrSvg(text: string, options: QrSvgOptions = {}): string {
  const qr = QRCode.create(text, { errorCorrectionLevel: 'M' })
  const dim = qr.modules.size
  // Standard quiet zone is 4 modules minimum for phone camera optical recognition
  const margin = options.margin ?? 4
  const totalSize = dim + margin * 2
  const width = options.width ?? 280
  const dark = options.darkColor ?? '#000000'
  const light = options.lightColor ?? '#ffffff'

  let pathData = ''
  for (let r = 0; r < dim; r++) {
    for (let c = 0; c < dim; c++) {
      if (qr.modules.get(r, c)) {
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
 * Generates an SVG Data URI (data:image/svg+xml;utf8,...) suitable for <img> src.
 */
export function generateQrDataUrl(text: string, options: QrSvgOptions = {}): string {
  const svg = generateQrSvg(text, options)
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}

/**
 * Generates an actual PNG Base64 Data URL (data:image/png;base64,...)
 * Perfect for saving directly as .png or printing.
 */
export async function generateQrPngDataUrl(text: string, options: QrSvgOptions = {}): Promise<string> {
  return await QRCode.toDataURL(text, {
    errorCorrectionLevel: 'M',
    margin: options.margin ?? 4,
    width: options.width ?? 280,
    color: {
      dark: options.darkColor ?? '#000000',
      light: options.lightColor ?? '#ffffff'
    }
  })
}
