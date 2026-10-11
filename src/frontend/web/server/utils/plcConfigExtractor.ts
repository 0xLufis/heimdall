import { createHash } from 'node:crypto'

/**
 * Standard TwinCAT 3 file extensions and directories ignored according to
 * TWINCAT_COPIA_INTEGRATION.md to prevent volatile binaries and compiler state
 * from polluting industrial Git version control.
 */
export const TWINCAT_GITIGNORE_PATTERNS = [
  '_Boot/**',
  '_CompileInfo/**',
  '**/*.compileinfo',
  '**/*.tpy',
  '**/*.tszip',
  '**/*.tmc',
  '**/*.tcdel',
  '**/*.bak',
  '**/~*',
  '**/*.suo',
  '**/*.user',
  '**/bin/**',
  '**/obj/**'
]

export interface ExtractedPlcFile {
  path: string
  normalizedPath: string
  fileType: 'POU' | 'DUT' | 'GVL' | 'TSPROJ' | 'VISU' | 'CONFIG' | 'OTHER'
  sizeBytes: number
  sha256: string
  content?: string
}

export interface PlcExtractionResult {
  controllerHostname: string
  archiveName: string
  extractedFiles: ExtractedPlcFile[]
  ignoredArtifacts: string[]
  totalSourceFiles: number
  totalIgnoredCount: number
  projectChecksum: string
  normalizedXaeRoot: string
  timestampUtc: string
}

/**
 * Evaluates whether a file path matches any transient TwinCAT compiler or build artifacts.
 */
export function isTransientArtifact(filePath: string): boolean {
  const normalized = filePath.replace(/\\/g, '/')
  const lower = normalized.toLowerCase()

  if (lower.includes('_boot/') || lower.includes('_compileinfo/') || lower.includes('/bin/') || lower.includes('/obj/')) {
    return true
  }

  const transientExtensions = ['.compileinfo', '.tpy', '.tszip', '.tmc', '.tcdel', '.bak', '.suo', '.user']
  if (transientExtensions.some(ext => lower.endsWith(ext))) {
    return true
  }

  const filename = lower.split('/').pop() || ''
  if (filename.startsWith('~') || filename.startsWith('.')) {
    return true
  }

  return false
}

/**
 * Categorizes a normalized TwinCAT XAE project file.
 */
export function categorizePlcFile(filePath: string): ExtractedPlcFile['fileType'] {
  const lower = filePath.toLowerCase()
  if (lower.endsWith('.tcpou')) return 'POU'
  if (lower.endsWith('.tcdut')) return 'DUT'
  if (lower.endsWith('.tcgvl')) return 'GVL'
  if (lower.endsWith('.tsproj') || lower.endsWith('.xti')) return 'TSPROJ'
  if (lower.endsWith('.tcvis')) return 'VISU'
  if (lower.endsWith('.xml') || lower.endsWith('.json')) return 'CONFIG'
  return 'OTHER'
}

/**
 * Normalizes relative path into canonical TwinCAT XAE directory structure.
 */
export function normalizeXaePath(projectName: string, relativePath: string): string {
  let clean = relativePath.replace(/\\/g, '/').replace(/^\/+/, '')
  // Strip top-level redundant archive folders if present
  if (clean.toLowerCase().startsWith('currentconfig/')) {
    clean = clean.substring('currentconfig/'.length)
  }
  return `${projectName}/${clean}`
}

/**
 * Pulls/extracts and normalizes a TwinCAT runtime configuration package (CurrentConfig.tnzip).
 */
export function extractPlcConfig(
  controllerHostname: string,
  rawFiles: Array<{ path: string; content: string }>,
  options: { projectName?: string } = {}
): PlcExtractionResult {
  const projectName = options.projectName || `${controllerHostname}_XAE`
  const extractedFiles: ExtractedPlcFile[] = []
  const ignoredArtifacts: string[] = []

  const hasher = createHash('sha256')

  for (const file of rawFiles) {
    if (isTransientArtifact(file.path)) {
      ignoredArtifacts.push(file.path)
      continue
    }

    const normalizedPath = normalizeXaePath(projectName, file.path)
    const fileSha = createHash('sha256').update(file.content).digest('hex')
    hasher.update(normalizedPath + fileSha)

    extractedFiles.push({
      path: file.path,
      normalizedPath,
      fileType: categorizePlcFile(file.path),
      sizeBytes: Buffer.byteLength(file.content, 'utf-8'),
      sha256: fileSha,
      content: file.content
    })
  }

  const projectChecksum = hasher.digest('hex')

  return {
    controllerHostname,
    archiveName: 'CurrentConfig.tnzip',
    extractedFiles,
    ignoredArtifacts,
    totalSourceFiles: extractedFiles.length,
    totalIgnoredCount: ignoredArtifacts.length,
    projectChecksum,
    normalizedXaeRoot: projectName,
    timestampUtc: new Date().toISOString()
  }
}
